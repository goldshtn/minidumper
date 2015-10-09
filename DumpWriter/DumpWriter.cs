using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DumpWriter
{
    public enum DumpType
    {
        Minimal,
        MinimalWithFullCLRHeap,
        FullMemory,
        FullMemoryExcludingSafeRegions
    }

    public class DumpWriter
    {
        struct DumpedSegment
        {
            public ulong Offset;
            public byte[] Data;
        }

        private TextWriter _logger;
        private C5.TreeDictionary<ulong, ulong> _majorClrRegions = new C5.TreeDictionary<ulong, ulong>();
        private C5.TreeDictionary<ulong, ulong> _otherClrRegions = new C5.TreeDictionary<ulong, ulong>();
        private DumpType _dumpType;
        private IDataReader _dbgEngine;
        private int _pid;
        private IntPtr _hProcess;
        private IEnumerator<C5.KeyValuePair<ulong, ulong>> _regionEnumerator = null;
        private BlockingCollection<DumpedSegment> _dumpedSegments = new BlockingCollection<DumpedSegment>();
        private FileStream _dumpFileStream;
        private Task _segmentSpillingTask;
        private bool _spillSegmentsAsynchronously;
        private bool _needMemoryCallbacks;

        private IEnumerable<C5.KeyValuePair<ulong, ulong>> EnumerateAllNeededRegions()
        {
            foreach (var region in _majorClrRegions)
                yield return region;
            foreach (var region in _otherClrRegions)
                yield return region;
        }

        private void DetermineNeededRegions()
        {
            if (_dumpType == DumpType.Minimal)
                return;

            var readerLogger = new DumpReaderLogger(_dbgEngine);
            var target = DataTarget.CreateFromDataReader(readerLogger);

            foreach (var clrVersion in target.ClrVersions)
            {
                var runtime = clrVersion.CreateRuntime();

                AddCLRRegions(runtime);

                TouchOtherRegions(readerLogger, runtime);

                _logger.WriteLine("{0} ranges requested by the DAC, total size {1:N0}",
                    _otherClrRegions.Count,
                    _otherClrRegions.Sum(r => (long)(r.Key - r.Value))
                    );
                _logger.WriteLine("{0} CLR regions, total size {1:N0}",
                    _majorClrRegions.Count,
                    _majorClrRegions.Sum(r => (long)(r.Key - r.Value))
                    );
            }
        }

        private void TouchOtherRegions(DumpReaderLogger readerLogger, ClrRuntime runtime)
        {
            // Touch all threads, stacks, frames
            foreach (var t in runtime.Threads)
            {
                foreach (var f in t.StackTrace)
                {
                    try { f.GetFileAndLineNumber(); }
                    catch (Exception) { }
                }
            }

            // Touch all modules
            runtime.EnumerateModules().Count();

            // Touch all heap regions, roots, types
            var heap = runtime.GetHeap();
            heap.EnumerateRoots(enumerateStatics: false).Count();
            heap.EnumerateTypes().Count();

            // TODO Check if it's faster to construct sorted inside ReaderWrapper
            foreach (var kvp in readerLogger.Ranges)
                _otherClrRegions.Add(kvp.Key, kvp.Value);
        }

        private void AddCLRRegions(ClrRuntime runtime)
        {
            foreach (var region in runtime.EnumerateMemoryRegions())
            {
                // We don't need reserved memory in our dump
                if (region.Type == ClrMemoryRegionType.ReservedGCSegment)
                    continue;

                ulong address = region.Address;
                ulong endAddress = region.Address + region.Size;
                ulong existingEndAddress;
                if (_majorClrRegions.Find(ref address, out existingEndAddress))
                {
                    _majorClrRegions.Update(region.Address, Math.Max(existingEndAddress, endAddress));
                }
                else
                {
                    _majorClrRegions.Add(region.Address, endAddress);
                }
            }
        }

        private static bool HasIntervalThatIsSubsetOfInterval(
            C5.TreeDictionary<ulong, ulong> regions,
            ulong regionStart,
            ulong regionEnd)
        {
            // Is there any region in whose [start, end] interval is a sub-interval of
            // [regionStart, regionEnd]?
            // TryWeakSuccessor gives us the first region whose start is >= regionStart,
            // but there could be more regions with start >= regionStart and end <= regionEnd.
            // Need to traverse successors until start > regionEnd, making containment impossible.
            C5.KeyValuePair<ulong, ulong> range;
            if (regions.TryWeakSuccessor(regionStart, out range))
            {
                if (range.Key >= regionStart && range.Value <= regionEnd)
                    return true;
            }
            while (regions.TrySuccessor(range.Key, out range))
            {
                if (range.Key > regionEnd)
                    break;

                if (range.Key >= regionStart && range.Value <= regionEnd)
                    return true;
            }
            return false;
        }

        private bool IsNeededRegion(ulong regionStart, ulong regionEnd)
        {
            return
                HasIntervalThatIsSubsetOfInterval(_majorClrRegions, regionStart, regionEnd) ||
                HasIntervalThatIsSubsetOfInterval(_otherClrRegions, regionStart, regionEnd);
        }

        private bool CallbackRoutine(
            IntPtr CallbackParam,
            ref MINIDUMP_CALLBACK_INPUT CallbackInput,
            ref MINIDUMP_CALLBACK_OUTPUT CallbackOutput
            )
        {
            _logger.WriteLine("Callback type: " + CallbackInput.CallbackType);

            if ((int)CallbackInput.CallbackType < 0 ||
                (int)CallbackInput.CallbackType > (int)MINIDUMP_CALLBACK_TYPE.SecondaryFlagsCallback)
            {
                // We don't know what these numbers mean. They aren't in the SDK headers,
                // but we are getting them when the dump generation begins (16, 17).
                _logger.WriteLine("\tThis callback type is not recognized");
                return false;
            }

            switch (CallbackInput.CallbackType)
            {
                // I/O callbacks
                case MINIDUMP_CALLBACK_TYPE.IoWriteAllCallback:
                    _logger.WriteLine("\tIOWriteAll: offset = {0:x8} buffer = {1:x16} size = {2:x8}",
                        CallbackInput.Io.Offset, CallbackInput.Io.Buffer, CallbackInput.Io.BufferBytes);
                    var segment = new DumpedSegment
                    {
                        Offset = CallbackInput.Io.Offset,
                        Data = new byte[CallbackInput.Io.BufferBytes]
                    };
                    Marshal.Copy(CallbackInput.Io.Buffer, segment.Data, 0, segment.Data.Length);
                    _dumpedSegments.Add(segment);
                    CallbackOutput.Status = 0;
                    break;
                case MINIDUMP_CALLBACK_TYPE.IoFinishCallback:
                    _logger.WriteLine("\tIOFinish");
                    _dumpedSegments.CompleteAdding();
                    CallbackOutput.Status = 0;
                    break;
                case MINIDUMP_CALLBACK_TYPE.IoStartCallback:
                    _logger.WriteLine("\tIOStart: handle = {0}", CallbackInput.Io.Handle);
                    if (_spillSegmentsAsynchronously)
                    {
                        // Providing S_FALSE (1) as the status here instructs dbghelp to send all
                        // I/O through the callback (IoWriteAllCallback).
                        CallbackOutput.Status = 1;
                        _segmentSpillingTask = Task.Factory.StartNew(SpillDumpSegmentsToDisk, TaskCreationOptions.LongRunning);
                    }
                    else
                    {
                        CallbackOutput.Status = 0;
                    }
                    if (_needMemoryCallbacks)
                    {
                        DetermineNeededRegions();
                    }
                    break;

                // Cancel callback
                case MINIDUMP_CALLBACK_TYPE.CancelCallback:
                    _logger.WriteLine("\tCancel callback invoked, asking for no further cancel checks");
                    CallbackOutput.Cancel.CheckCancel = false;
                    CallbackOutput.Cancel.Cancel = false;
                    break;

                // Thread callbacks
                case MINIDUMP_CALLBACK_TYPE.IncludeThreadCallback:
                    _logger.WriteLine("\tDefault include thread flags for thread {0} = {1}",
                        CallbackInput.IncludeThread.ThreadId,
                        CallbackOutput.ThreadWriteFlags);
                    break;
                case MINIDUMP_CALLBACK_TYPE.ThreadCallback:
                case MINIDUMP_CALLBACK_TYPE.ThreadExCallback:
                    _logger.WriteLine("\tWriting thread {0} handle {1:x} stack {2:x16} - {3:x16}",
                        CallbackInput.Thread.ThreadId, CallbackInput.Thread.ThreadHandle,
                        CallbackInput.Thread.StackBase, CallbackInput.Thread.StackEnd);
                    break;
                default:
                    break;

                // Module callbacks
                case MINIDUMP_CALLBACK_TYPE.IncludeModuleCallback:
                    _logger.WriteLine("\tDefault include module flags for module @ {0:x16} = {1}",
                        CallbackInput.IncludeModule.BaseOfImage,
                        CallbackOutput.ModuleWriteFlags);
                    break;
                case MINIDUMP_CALLBACK_TYPE.ModuleCallback:
                    string moduleName = Marshal.PtrToStringUni(CallbackInput.Module.FullPath);
                    _logger.WriteLine("\tWriting module @ {0:x16} {1}",
                        CallbackInput.Module.BaseOfImage,
                        moduleName);
                    break;

                // Memory callbacks
                case MINIDUMP_CALLBACK_TYPE.MemoryCallback:
                    if (!_needMemoryCallbacks)
                        break;
                    if (_regionEnumerator == null)
                    {
                        _regionEnumerator = EnumerateAllNeededRegions().GetEnumerator();
                    }
                    if (_regionEnumerator.MoveNext())
                    {
                        var region = _regionEnumerator.Current;
                        CallbackOutput.Memory.MemoryBase = region.Key;
                        CallbackOutput.Memory.MemorySize = (uint)(region.Value - region.Key);
                    }
                    _logger.WriteLine("\tRequesting memory region @ {0:x16} size {1}",
                        CallbackOutput.Memory.MemoryBase,
                        CallbackOutput.Memory.MemorySize);
                    // If this callback produces a non-zero value, it will be included in the 
                    // dump and the callback will be invoked again. This is only relevant when
                    // not capturing a full memory dump.
                    break;
                case MINIDUMP_CALLBACK_TYPE.IncludeVmRegionCallback:
                    if (!_needMemoryCallbacks)
                        break;
                    FilterVMRegion(ref CallbackOutput);
                    break;

                // Other callbacks
                case MINIDUMP_CALLBACK_TYPE.KernelMinidumpStatusCallback:
                case MINIDUMP_CALLBACK_TYPE.WriteKernelMinidumpCallback:
                case MINIDUMP_CALLBACK_TYPE.SecondaryFlagsCallback:
                case MINIDUMP_CALLBACK_TYPE.RemoveMemoryCallback:
                    break;

                // Memory error callback
                case MINIDUMP_CALLBACK_TYPE.ReadMemoryFailureCallback:
                    _logger.WriteLine("\tFailed to read memory @ {0} size {1} error {2:x8}",
                        CallbackInput.ReadMemoryFailure.Offset,
                        CallbackInput.ReadMemoryFailure.Bytes,
                        CallbackInput.ReadMemoryFailure.FailureStatus);
                    break;
            }

            return true;
        }

        private void FilterVMRegion(ref MINIDUMP_CALLBACK_OUTPUT CallbackOutput)
        {
            if (_dumpType != DumpType.FullMemoryExcludingSafeRegions)
            {
                // No further callbacks of this type are required.
                CallbackOutput.MemoryInfo.Continue = false;
                return;
            }
            _logger.WriteLine("\tWrite VM region @ {0:x16} size {1} {2} {3} {4}",
                CallbackOutput.MemoryInfo.VmRegion.BaseAddress,
                CallbackOutput.MemoryInfo.VmRegion.RegionSize,
                CallbackOutput.MemoryInfo.VmRegion.State,
                CallbackOutput.MemoryInfo.VmRegion.Type,
                CallbackOutput.MemoryInfo.VmRegion.Protect);

            // NOTE We can further filter because we don't necessarily need the whole
            // region. We can find the maximum overlapping needed region that is contained
            // within this region, and change the BaseAddress and RegionSize fields to
            // capture only that sub-region.
            if (!IsNeededRegion(
                CallbackOutput.MemoryInfo.VmRegion.BaseAddress,
                CallbackOutput.MemoryInfo.VmRegion.BaseAddress + CallbackOutput.MemoryInfo.VmRegion.RegionSize)
                )
            {
                // We do not need this region.
                _logger.WriteLine("\tRegion EXCLUDED from dump");
                CallbackOutput.MemoryInfo.VmRegion.RegionSize = 0;
            }

            // If the region size or base address are modified (as long as they remain a 
            // subset of the original region), that's what the dump will contain.
            CallbackOutput.MemoryInfo.Continue = true;
        }

        public DumpWriter(IDataReader dbgEngine, IntPtr hProcess, int pid, TextWriter logger = null)
        {
            _dbgEngine = dbgEngine;
            _pid = pid;
            _hProcess = hProcess;
            _logger = logger ?? TextWriter.Null;
        }

        public void Dump(DumpType dumpType, string fileName, bool writeAsync = false, string dumpComment = null)
        {
            _dumpType = dumpType;
            _spillSegmentsAsynchronously = writeAsync;
            dumpComment = dumpComment ?? ("DumpWriter: " + _dumpType.ToString());

            _dumpFileStream = new FileStream(fileName, FileMode.Create);

            var exceptionParam = new MINIDUMP_EXCEPTION_INFORMATION();
            var userStreamParam = PrepareUserStream(dumpComment);
            var callbackParam = new MINIDUMP_CALLBACK_INFORMATION();
            _needMemoryCallbacks = (
                _dumpType == DumpType.FullMemoryExcludingSafeRegions ||
                _dumpType == DumpType.MinimalWithFullCLRHeap
                );
            if (_needMemoryCallbacks || _spillSegmentsAsynchronously) {
                callbackParam.CallbackRoutine = CallbackRoutine;
            }

            MINIDUMP_TYPE nativeDumpType =
                (_dumpType == DumpType.FullMemory || _dumpType == DumpType.FullMemoryExcludingSafeRegions) ?
                MINIDUMP_TYPE.MiniDumpWithFullMemory | MINIDUMP_TYPE.MiniDumpWithHandleData | MINIDUMP_TYPE.MiniDumpWithFullMemoryInfo :
                MINIDUMP_TYPE.MiniDumpWithHandleData | MINIDUMP_TYPE.MiniDumpWithFullMemoryInfo;
            Stopwatch sw = Stopwatch.StartNew();
            bool success = DumpNativeMethods.MiniDumpWriteDump(
                _hProcess,
                (uint)_pid,
                _dumpFileStream.SafeFileHandle.DangerousGetHandle(),
                nativeDumpType,
                ref exceptionParam,
                ref userStreamParam,
                ref callbackParam
                );
            if (!success)
                throw new ApplicationException(String.Format("Error writing dump, error {0:x8}", Marshal.GetLastWin32Error()));

            _logger.WriteLine("Process was suspended for {0:N2}ms", sw.Elapsed.TotalMilliseconds);

            if (_spillSegmentsAsynchronously)
            {
                // We are asynchronously spilling dump segments to disk, need to wait
                // for this process to complete before returning to the caller.
                _segmentSpillingTask.Wait();
                _logger.WriteLine(
                    "Total dump writing time including async flush was {0:N2}ms",
                    sw.Elapsed.TotalMilliseconds);
            }

            userStreamParam.Delete();
            _dumpFileStream.Close();
        }

        private void SpillDumpSegmentsToDisk()
        {
            foreach (var segment in _dumpedSegments.GetConsumingEnumerable())
            {
                _dumpFileStream.Seek((long)segment.Offset, SeekOrigin.Begin);
                _dumpFileStream.Write(segment.Data, 0, segment.Data.Length);
            }
        }

        private static MINIDUMP_USER_STREAM_INFORMATION PrepareUserStream(string dumpComment)
        {
            MINIDUMP_USER_STREAM userStream = new MINIDUMP_USER_STREAM();
            userStream.Type = MINIDUMP_STREAM_TYPE.CommentStreamW;
            userStream.Buffer = Marshal.StringToHGlobalUni(dumpComment);
            userStream.BufferSize = (uint)(dumpComment.Length + 1) * 2;
            return new MINIDUMP_USER_STREAM_INFORMATION(userStream);
        }
    }
}
