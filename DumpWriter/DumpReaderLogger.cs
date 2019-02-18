using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DumpWriter
{
    class DumpReaderLogger : IDataReader
    {
        const int PAGE_SIZE = 4096;
        private IDataReader _impl;
        private Dictionary<ulong, ulong> _ranges = new Dictionary<ulong, ulong>();

        public DumpReaderLogger(IDataReader impl)
        {
            _impl = impl;

            // TODO Think about whether it's worth the time to eliminate overlaps, e.g.
            // reading from 0x2000 to 0x4000 and then reading from 0x1000 to 0x3000 could
            // be joined into a single 0x1000-0x3000 range. In practice, not sure there will
            // be many overlaps like that.
        }

        public IDictionary<ulong, ulong> Ranges { get { return _ranges; } }

        private void AddRange(ulong start, ulong end)
        {
            // Round to start of page
            start -= start % PAGE_SIZE;

            // Round to end of page
            if (end % PAGE_SIZE != 0)
                end += PAGE_SIZE - (end % PAGE_SIZE);

            ulong prevEnd;
            if (_ranges.TryGetValue(start, out prevEnd))
                _ranges[start] = Math.Max(end, prevEnd);
            else
                _ranges.Add(start, end);
        }

        public bool ReadMemory(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead)
        {
            bool success = _impl.ReadMemory(address, buffer, bytesRequested, out bytesRead);
            if (success)
            {
                AddRange(address, address + (ulong)bytesRead);
            }
            return success;
        }

        public bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            bool success = _impl.ReadMemory(address, buffer, bytesRequested, out bytesRead);
            if (success)
            {
                AddRange(address, address + (ulong)bytesRead);
            }
            return success;
        }

        #region Boring

        public bool IsMinidump
        {
            get
            {
                return _impl.IsMinidump;
            }
        }

        public void Close()
        {
            _impl.Close();
        }

        public IEnumerable<uint> EnumerateAllThreads()
        {
            return _impl.EnumerateAllThreads();
        }

        public IList<ModuleInfo> EnumerateModules()
        {
            return _impl.EnumerateModules();
        }

        public void Flush()
        {
            _impl.Flush();
        }

        public Architecture GetArchitecture()
        {
            return _impl.GetArchitecture();
        }

        public uint GetPointerSize()
        {
            return _impl.GetPointerSize();
        }

        public bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, byte[] context)
        {
            return _impl.GetThreadContext(threadID, contextFlags, contextSize, context);
        }

        public bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, IntPtr context)
        {
            return _impl.GetThreadContext(threadID, contextFlags, contextSize, context);
        }

        public ulong GetThreadTeb(uint thread)
        {
            return _impl.GetThreadTeb(thread);
        }

        public void GetVersionInfo(ulong baseAddress, out VersionInfo version)
        {
            _impl.GetVersionInfo(baseAddress, out version);
        }

        public uint ReadDwordUnsafe(ulong addr)
        {
            return _impl.ReadDwordUnsafe(addr);
        }

        public ulong ReadPointerUnsafe(ulong addr)
        {
            return _impl.ReadPointerUnsafe(addr);
        }

        public bool VirtualQuery(ulong addr, out VirtualQueryData vq)
        {
            return _impl.VirtualQuery(addr, out vq);
        }

        #endregion
    }
}
