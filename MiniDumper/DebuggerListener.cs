using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Text;

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
        #region Fields
        // FIXME output should be something configurable, such as TextWriter
        DEBUG_OUTPUT _outputMask;
        StringBuilder _output = new StringBuilder();
        #endregion

        #region Events
        public delegate void ModuleEventHandler(DebuggerListener dbg, ModuleEventArgs args);
        public event ModuleEventHandler ModuleLoadEvent;
        public event ModuleEventHandler ModuleUnloadEvent;

        public delegate void CreateThreadEventHandler(DebuggerListener dbg, CreateThreadArgs args);
        public event CreateThreadEventHandler ThreadCreateEvent;

        public delegate void ExitThreadEventHandler(DebuggerListener dbg, int exitCode);
        public event ExitThreadEventHandler ExitThreadEvent;

        public delegate void ExceptionEventHandler(DebuggerListener dbg, EXCEPTION_RECORD64 ex);
        public event ExceptionEventHandler FirstChanceExceptionEvent;
        public event ExceptionEventHandler SecondChanceExceptionEvent;

        public delegate void CreateProcessEventHandler(DebuggerListener dbg, CreateProcessArgs args);
        public event CreateProcessEventHandler CreateProcessEvent;

        public delegate void ExitProcessEventHandler(DebuggerListener dbg, int exitCode);
        public event ExitProcessEventHandler ExitProcessEvent;
        #endregion

        public DebuggerListener() { 
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
                _output.Append(Text);

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
            return (int)DEBUG_STATUS.GO;
        }

        public int CreateProcess(ulong ImageFileHandle, ulong Handle, ulong BaseOffset, uint ModuleSize, string ModuleName, string ImageName,
                                 uint CheckSum, uint TimeDateStamp, ulong InitialThreadHandle, ulong ThreadDataOffset, ulong StartOffset)
        {
            CreateProcessEventHandler evt = CreateProcessEvent;
            if (evt != null)
                evt(this, new CreateProcessArgs(ImageFileHandle, Handle, BaseOffset, ModuleSize, ModuleName, ImageName, CheckSum, TimeDateStamp, InitialThreadHandle, ThreadDataOffset, StartOffset));

            return 0;
        }

        public int ExitProcess(uint ExitCode)
        {
            ExitProcessEventHandler evt = ExitProcessEvent;
            if (evt != null)
                evt(this, (int)ExitCode);

            return (int)DEBUG_STATUS.BREAK;
        }

        public int CreateThread(ulong Handle, ulong DataOffset, ulong StartOffset)
        {
            CreateThreadEventHandler evt = ThreadCreateEvent;
            if (evt != null)
                evt(this, new CreateThreadArgs(Handle, DataOffset, StartOffset));

            return 0;
        }

        public int ExitThread(uint ExitCode)
        {
            ExitThreadEventHandler evt = ExitThreadEvent;
            if (evt != null)
                evt(this, (int)ExitCode);

            return 0;
        }

        public int Exception(ref EXCEPTION_RECORD64 Exception, uint FirstChance)
        {
            ExceptionEventHandler evt = (FirstChance == 1) ? FirstChanceExceptionEvent : SecondChanceExceptionEvent;
            if (evt != null)
                evt(this, Exception);

            return (int)DEBUG_STATUS.BREAK;
        }

        public int LoadModule(ulong ImageFileHandle, ulong BaseOffset, uint ModuleSize, string ModuleName, string ImageName, uint CheckSum, uint TimeDateStamp)
        {
            ModuleEventHandler evt = ModuleLoadEvent;
            if (evt != null)
                evt(this, new ModuleEventArgs(ImageFileHandle, BaseOffset, ModuleSize, ModuleName, ImageName, CheckSum, TimeDateStamp));

            return 0;
        }

        public int UnloadModule(string ImageBaseName, ulong BaseOffset)
        {
            ModuleEventHandler evt = ModuleUnloadEvent;
            if (evt != null)
                evt(this, new ModuleEventArgs(ImageBaseName, BaseOffset));

            return 0;
        }

        public int SessionStatus(DEBUG_SESSION Status)
        {
            throw new NotImplementedException();
        }

        public int SystemError(uint Error, uint Level)
        {
            throw new NotImplementedException();
        }

        public int ChangeDebuggeeState(DEBUG_CDS Flags, ulong Argument)
        {
            throw new NotImplementedException();
        }

        public int ChangeEngineState(DEBUG_CES Flags, ulong Argument)
        {
            throw new NotImplementedException();
        }

        public int ChangeSymbolState(DEBUG_CSS Flags, ulong Argument)
        {
            throw new NotImplementedException();
        }
        #endregion
    }

    class ModuleEventArgs
    {
        public ulong ImageFileHandle;
        public ulong BaseOffset;
        public uint ModuleSize;
        public string ModuleName;
        public string ImageName;
        public uint CheckSum;
        public uint TimeDateStamp;

        public ModuleEventArgs(string imageBaseName, ulong baseOffset)
        {
            ImageName = imageBaseName;
            BaseOffset = baseOffset;
        }

        public ModuleEventArgs(ulong ImageFileHandle, ulong BaseOffset, uint ModuleSize, string ModuleName, string ImageName, uint CheckSum, uint TimeDateStamp)
        {
            this.ImageFileHandle = ImageFileHandle;
            this.BaseOffset = BaseOffset;
            this.ModuleSize = ModuleSize;
            this.ModuleName = ModuleName;
            this.ImageName = ImageName;
            this.CheckSum = CheckSum;
            this.TimeDateStamp = TimeDateStamp;
        }
    }

    class CreateThreadArgs
    {
        public ulong Handle;
        public ulong DataOffset;
        public ulong StartOffset;

        public CreateThreadArgs(ulong handle, ulong data, ulong start)
        {
            Handle = handle;
            DataOffset = data;
            StartOffset = start;
        }
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
