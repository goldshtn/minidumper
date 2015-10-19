using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

namespace MiniDumper
{
    enum ExceptionTypes : uint
    {
        AV = 0xC0000005,
        StackOverflow = 0xC00000FD,
        Cpp = 0xe06d7363,
        Clr = 0xe0434352
    }

    class DebuggerListener : IDebugOutputCallbacks, IDebugEventCallbacks
    {
        readonly DEBUG_OUTPUT _outputMask;
        readonly TextWriter _output;

        #region Events
        public delegate void ExceptionEventHandler(DebuggerListener dbg, EXCEPTION_RECORD ex);
        public event ExceptionEventHandler FirstChanceExceptionEvent;
        public event ExceptionEventHandler SecondChanceExceptionEvent;

        public delegate void CreateProcessEventHandler(DebuggerListener dbg, CreateProcessArgs args);
        public event CreateProcessEventHandler CreateProcessEvent;

        public delegate void ExitProcessEventHandler(DebuggerListener dbg, int exitCode);
        public event ExitProcessEventHandler ExitProcessEvent;
        #endregion

        public DebuggerListener(TextWriter output, DEBUG_OUTPUT mask = DEBUG_OUTPUT.NORMAL) {
            _outputMask = mask;
            _output = output ?? TextWriter.Null;
        }

        public void StartListening(IDebugClient5 client)
        {
            client.SetOutputCallbacks(this);
            client.SetEventCallbacks(this);
        }

        public void StopListening(IDebugClient5 client)
        {
            client.SetEventCallbacks(null);
            client.SetOutputCallbacks(null);
        }

        #region IDebugOutputCallbacks
        public int Output(DEBUG_OUTPUT Mask, string Text)
        {
            if (_output != null && (_outputMask & Mask) != 0)
                _output.WriteLine(Text);

            return 0;
        }
        #endregion

        #region IDebugEventCallbacks
        public int GetInterestMask(out DEBUG_EVENT Mask)
        {
            Mask = DEBUG_EVENT.BREAKPOINT | DEBUG_EVENT.CREATE_PROCESS
                | DEBUG_EVENT.EXCEPTION | DEBUG_EVENT.EXIT_PROCESS
                | DEBUG_EVENT.CREATE_THREAD | DEBUG_EVENT.EXIT_THREAD
                | DEBUG_EVENT.LOAD_MODULE | DEBUG_EVENT.UNLOAD_MODULE;
            return 0;
        }


        public int Breakpoint(IDebugBreakpoint Bp)
        {
            return (int)DEBUG_STATUS.BREAK;
        }

        public int CreateProcess(ulong ImageFileHandle, ulong Handle, ulong BaseOffset, uint ModuleSize, string ModuleName, string ImageName,
                                 uint CheckSum, uint TimeDateStamp, ulong InitialThreadHandle, ulong ThreadDataOffset, ulong StartOffset)
        {
            CreateProcessEventHandler evt = CreateProcessEvent;
            if (evt != null)
                evt(this, new CreateProcessArgs(ImageFileHandle, Handle, BaseOffset, ModuleSize, ModuleName, ImageName, CheckSum, TimeDateStamp, InitialThreadHandle, ThreadDataOffset, StartOffset));

            return (int)DEBUG_STATUS.BREAK;
        }

        public int ExitProcess(uint ExitCode)
        {
            ExitProcessEventHandler evt = ExitProcessEvent;
            if (evt != null)
                evt(this, (int)ExitCode);

            return (int)DEBUG_STATUS.BREAK;
        }

        public int Exception(ref EXCEPTION_RECORD Exception, uint FirstChance)
        {
            ExceptionEventHandler evt = (FirstChance == 1) ? FirstChanceExceptionEvent : SecondChanceExceptionEvent;
            if (evt != null)
                evt(this, Exception);

            return (int)DEBUG_STATUS.BREAK;
        }

        public int SessionStatus(DEBUG_SESSION Status)
        {
            return (int)DEBUG_STATUS.GO;
        }

        public int SystemError(uint Error, uint Level)
        {
            return (int)DEBUG_STATUS.GO;
        }

        public int ChangeDebuggeeState(DEBUG_CDS Flags, ulong Argument)
        {
            return (int)DEBUG_STATUS.GO;
        }

        public int ChangeEngineState(DEBUG_CES Flags, ulong Argument)
        {
            return (int)DEBUG_STATUS.GO;
        }

        public int ChangeSymbolState(DEBUG_CSS Flags, ulong Argument)
        {
            return (int)DEBUG_STATUS.GO;
        }

        public int CreateThread([In] ulong Handle, [In] ulong DataOffset, [In] ulong StartOffset)
        {
            return (int)DEBUG_STATUS.GO;
        }

        public int ExitThread([In] uint ExitCode)
        {
            return (int)DEBUG_STATUS.GO;
        }

        public int LoadModule([In] ulong ImageFileHandle, [In] ulong BaseOffset, [In] uint ModuleSize, [In, MarshalAs(UnmanagedType.LPStr)] string ModuleName, [In, MarshalAs(UnmanagedType.LPStr)] string ImageName, [In] uint CheckSum, [In] uint TimeDateStamp)
        {
            return (int)DEBUG_STATUS.GO;
        }

        public int UnloadModule([In, MarshalAs(UnmanagedType.LPStr)] string ImageBaseName, [In] ulong BaseOffset)
        {
            return (int)DEBUG_STATUS.GO;
        }
        #endregion
    }

    class CreateProcessArgs
    {
        public ulong ImageFileHandle;
        public ulong Handle;
        public ulong BaseOffset;
        public uint ModuleSize;
        public string ModuleName;
        public string ImageName;
        public uint CheckSum;
        public uint TimeDateStamp;
        public ulong InitialThreadHandle;
        public ulong ThreadDataOffset;
        public ulong StartOffset;

        public CreateProcessArgs(ulong ImageFileHandle, ulong Handle, ulong BaseOffset, uint ModuleSize, string ModuleName, string ImageName,
                                 uint CheckSum, uint TimeDateStamp, ulong InitialThreadHandle, ulong ThreadDataOffset, ulong StartOffset)
        {
            this.ImageFileHandle = ImageFileHandle;
            this.Handle = Handle;
            this.BaseOffset = BaseOffset;
            this.ModuleSize = ModuleSize;
            this.ModuleName = ModuleName;
            this.ImageName = ImageName;
            this.CheckSum = CheckSum;
            this.TimeDateStamp = TimeDateStamp;
            this.InitialThreadHandle = InitialThreadHandle;
            this.ThreadDataOffset = ThreadDataOffset;
            this.StartOffset = StartOffset;
        }
    }
}
