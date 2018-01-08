﻿using DumpWriter;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using VsChromium.Core.Win32.Debugging;
using VsChromium.Core.Win32.Processes;
using ProcessNativeMethod = VsChromium.Core.Win32.Processes.NativeMethods;

namespace MiniDumper
{
    [Flags]
    public enum ThreadAccess : int
    {
        TERMINATE = (0x0001),
        SUSPEND_RESUME = (0x0002),
        GET_CONTEXT = (0x0008),
        SET_CONTEXT = (0x0010),
        SET_INFORMATION = (0x0020),
        QUERY_INFORMATION = (0x0040),
        SET_THREAD_TOKEN = (0x0080),
        IMPERSONATE = (0x0100),
        DIRECT_IMPERSONATION = (0x0200)
    }

    class Native
    {
#if X64
        public const int CONTEXT_SIZE = 1232;
#else
        public const int CONTEXT_SIZE = 716;
#endif
    }

    class MiniDumper : IDisposable
    {
        const uint CLRDBG_NOTIFICATION_EXCEPTION_CODE = 0x04242420;
        const uint BREAKPOINT_CODE = 0x80000003;
        const uint CTRL_C_EXCEPTION_CODE = 0x40010005;
        const string dumpComment = "Generated by MiniDumper";

        private readonly string dumpFolder;
        private readonly int pid;
        private readonly string processName;
        private readonly TextWriter logger;
        private readonly DumpType dumpType;
        private readonly bool writeAsync;
        private readonly Regex rgxFilter;
        private readonly DataTarget target;
        private int numberOfDumpsTaken;

        public MiniDumper(string dumpFolder, int pid, string processName,
            TextWriter logger, DumpType dumpType, bool writeAsync, string filter)
        {
            this.dumpFolder = dumpFolder;
            this.pid = pid;
            this.processName = processName;
            this.logger = logger;
            this.dumpType = dumpType;
            this.writeAsync = writeAsync;
            this.rgxFilter = new Regex((filter ?? "*").Replace("*", ".*").Replace('?', '.'),
                RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
            this.target = DataTarget.AttachToProcess(pid, 1000, AttachFlag.Passive);
        }

        public void DumpWithoutReason()
        {
            PrintTrace("Taking a dump.");
            MakeActualDump(IntPtr.Zero);
        }

        public void DumpOnException(uint threadId, EXCEPTION_RECORD ev)
        {
            if (ev.ExceptionCode == BREAKPOINT_CODE)
            {
                return;
            }
            if (ev.ExceptionCode == CLRDBG_NOTIFICATION_EXCEPTION_CODE)
            {
                // based on https://social.msdn.microsoft.com/Forums/vstudio/en-US/bca092d4-d2b5-49ef-8bbc-cbce2c67aa89/net-40-firstchance-exception-0x04242420?forum=clr
                // it's a "notification exception" and can be safely ignored
                return;
            }
            if (ev.ExceptionCode == CTRL_C_EXCEPTION_CODE)
            {
                // we will also ignore CTRL+C events
                return;
            }
            // print information about the exception (decode it)
            ClrException managedException = null;
            foreach (var clrver in target.ClrVersions)
            {
                var runtime = clrver.CreateRuntime();
                var thr = runtime.Threads.FirstOrDefault(t => t.OSThreadId == threadId);
                if (thr != null)
                {
                    managedException = thr.CurrentException;
                    break;
                }
            }
            var exceptionInfo = string.Format("{0:X}.{1} (\"{2}\")", ev.ExceptionCode,
                managedException != null ? managedException.Type.Name : "Native",
                managedException != null ? managedException.Message : "N/A");

            PrintTrace("Exception: " + exceptionInfo);

            if (rgxFilter.IsMatch(exceptionInfo))
            {
                byte[] threadContext = new byte[Native.CONTEXT_SIZE];
                target.DataReader.GetThreadContext(threadId, 0, Native.CONTEXT_SIZE, threadContext);
                IntPtr pev = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(EXCEPTION_RECORD)));
                Marshal.StructureToPtr(new EXCEPTION_RECORD
                {
                    ExceptionAddress = ev.ExceptionAddress,
                    ExceptionFlags = ev.ExceptionFlags,
                    ExceptionCode = ev.ExceptionCode,
                    ExceptionRecord = IntPtr.Zero,
                    NumberParameters = ev.NumberParameters,
                    ExceptionInformation = ev.ExceptionInformation
                }, pev, false);
                var excpointers = new EXCEPTION_POINTERS
                {
                    ExceptionRecord = pev,
                    ContextRecord = threadContext
                };
                IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(excpointers));
                Marshal.StructureToPtr(excpointers, ptr, false);
                var excinfo = new MINIDUMP_EXCEPTION_INFORMATION()
                {
                    ThreadId = threadId,
                    ClientPointers = false,
                    ExceptionPointers = ptr
                };
                var pexcinfo = Marshal.AllocHGlobal(Marshal.SizeOf(excinfo));
                Marshal.StructureToPtr(excinfo, pexcinfo, false);

                MakeActualDump(pexcinfo);

                Marshal.FreeHGlobal(pev);
                Marshal.FreeHGlobal(pexcinfo);
                Marshal.FreeHGlobal(ptr);
            }
        }

        public void DumpOnProcessExit(uint exitCode)
        {
            PrintTrace(string.Format("Process has terminated."));
            MakeActualDump(IntPtr.Zero);
        }

        public void PrintDebugString(OUTPUT_DEBUG_STRING_INFO outputDbgStrInfo)
        {
            using (SafeProcessHandle hProcess = ProcessNativeMethod.OpenProcess(ProcessAccessFlags.VmRead, false, pid))
            {
                if (hProcess.IsInvalid)
                    throw new ArgumentException(String.Format("Unable to open process {0}, error {x:8}", pid, Marshal.GetLastWin32Error()));

                var dbgString = new byte[outputDbgStrInfo.nDebugStringLength];

                uint numberOfBytesRead;
                var result = ProcessNativeMethod.ReadProcessMemory(hProcess, outputDbgStrInfo.lpDebugStringData, dbgString, outputDbgStrInfo.nDebugStringLength, out numberOfBytesRead);

                if (result)
                {
                    if (outputDbgStrInfo.fUnicode == 0)
                    {
                        Console.WriteLine("Debug String: {0}", Encoding.ASCII.GetString(dbgString));
                    }
                    else
                    {
                        Console.WriteLine("Debug String: {0}", Encoding.Unicode.GetString(dbgString));
                    }
                }
            }
        }

        public void MemoryCommitThreshold(int commitThreshold)
        {
            System.Timers.Timer aTimer = new System.Timers.Timer();
            aTimer.Interval = 1000;
            aTimer.Elapsed += (sender, e) => OnTimedEvent(sender, e, commitThreshold); ;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
        }

        private void OnTimedEvent(Object source, ElapsedEventArgs e, int commitThreshold)
        {
           var process = Process.GetProcessById(pid);
            double privateMemory = (process.PrivateMemorySize64 / 1024) / 1024;
            if (privateMemory >= commitThreshold)
            {
                var timer = source as System.Timers.Timer;
                timer.Stop();
                timer.Dispose();
                PrintTrace($"Commit:\t{(int)privateMemory}MB");
                MakeActualDump(IntPtr.Zero);
            }
        }
        private void MakeActualDump(IntPtr excinfo)
        {
            var dumper = new DumpWriter.DumpWriter(logger);

            var filename = GetDumpFileName();
            PrintTrace(string.Format("Dumping process memory to file: {0}", filename));

            Interlocked.Increment(ref numberOfDumpsTaken);
            dumper.Dump(pid, dumpType, excinfo, filename, writeAsync, dumpComment);
        }

        string GetDumpFileName()
        {
            var bfilename = Path.Combine(dumpFolder, string.Format("{0}_{1:yyMMdd_HHmmss}", processName, DateTime.Now));
            int cnt = 0;

            var filename = bfilename;
            while (true)
            {
                if (!File.Exists(filename + ".dmp"))
                {
                    break;
                }
                cnt++;
                filename = bfilename + "_" + cnt;
            }
            return filename + ".dmp";
        }

        void PrintTrace(string message)
        {
            Console.WriteLine("[{0:HH:mm.ss}] {1}", DateTime.Now, message);
        }

        public int NumberOfDumpsTaken
        {
            get { return numberOfDumpsTaken; }
        }

        public void Dispose()
        {
            target.Dispose();
        }
    }
}
