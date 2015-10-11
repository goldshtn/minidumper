using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DumpWriter
{
#pragma warning disable 0649
    [Flags]
    enum MINIDUMP_TYPE : int
    {
        MiniDumpNormal = 0x00000000,
        MiniDumpWithDataSegs = 0x00000001,
        MiniDumpWithFullMemory = 0x00000002,
        MiniDumpWithHandleData = 0x00000004,
        MiniDumpFilterMemory = 0x00000008,
        MiniDumpScanMemory = 0x00000010,
        MiniDumpWithUnloadedModules = 0x00000020,
        MiniDumpWithIndirectlyReferencedMemory = 0x00000040,
        MiniDumpFilterModulePaths = 0x00000080,
        MiniDumpWithProcessThreadData = 0x00000100,
        MiniDumpWithPrivateReadWriteMemory = 0x00000200,
        MiniDumpWithoutOptionalData = 0x00000400,
        MiniDumpWithFullMemoryInfo = 0x00000800,
        MiniDumpWithThreadInfo = 0x00001000,
        MiniDumpWithCodeSegs = 0x00002000,
        MiniDumpWithoutAuxiliaryState = 0x00004000,
        MiniDumpWithFullAuxiliaryState = 0x00008000,
        MiniDumpWithPrivateWriteCopyMemory = 0x00010000,
        MiniDumpIgnoreInaccessibleMemory = 0x00020000,
        MiniDumpWithTokenInformation = 0x00040000,
        MiniDumpWithModuleHeaders = 0x00080000,
        MiniDumpFilterTriage = 0x00100000,
        MiniDumpValidTypeFlags = 0x001fffff
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct EXCEPTION_POINTERS
    {
        public IntPtr ExceptionRecord;
        public IntPtr ContextRecord;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct MINIDUMP_EXCEPTION_INFORMATION
    {
        public uint ThreadId;
        public IntPtr ExceptionPointers;
        [MarshalAs(UnmanagedType.Bool)]
        public bool ClientPointers;
    }

    enum MINIDUMP_STREAM_TYPE : uint
    {
        UnusedStream = 0,
        ReservedStream0 = 1,
        ReservedStream1 = 2,
        ThreadListStream = 3,
        ModuleListStream = 4,
        MemoryListStream = 5,
        ExceptionStream = 6,
        SystemInfoStream = 7,
        ThreadExListStream = 8,
        Memory64ListStream = 9,
        CommentStreamA = 10,
        CommentStreamW = 11,
        HandleDataStream = 12,
        FunctionTableStream = 13,
        UnloadedModuleListStream = 14,
        MiscInfoStream = 15,
        MemoryInfoListStream = 16,
        ThreadInfoListStream = 17,
        HandleOperationListStream = 18,
        LastReservedStream = 0xffff
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct MINIDUMP_USER_STREAM
    {
        public MINIDUMP_STREAM_TYPE Type;
        public uint BufferSize;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct MINIDUMP_USER_STREAM_INFORMATION
    {
        public MINIDUMP_USER_STREAM_INFORMATION(params MINIDUMP_USER_STREAM[] streams)
        {
            UserStreamCount = (uint)streams.Length;
            int sizeOfStream = Marshal.SizeOf(typeof(MINIDUMP_USER_STREAM));
            UserStreamArray = Marshal.AllocHGlobal((int)(UserStreamCount * sizeOfStream));
            for (int i = 0; i < streams.Length; ++i)
            {
                Marshal.StructureToPtr(streams[i], UserStreamArray + (i * sizeOfStream), false);
            }
        }

        public void Delete()
        {
            Marshal.FreeHGlobal(UserStreamArray);
            UserStreamCount = 0;
            UserStreamArray = IntPtr.Zero;
        }

        public uint UserStreamCount;
        public IntPtr UserStreamArray;
    }

    enum MINIDUMP_CALLBACK_TYPE : uint
    {
        ModuleCallback,
        ThreadCallback,
        ThreadExCallback,
        IncludeThreadCallback,
        IncludeModuleCallback,
        MemoryCallback,
        CancelCallback,
        WriteKernelMinidumpCallback,
        KernelMinidumpStatusCallback,
        RemoveMemoryCallback,
        IncludeVmRegionCallback,
        IoStartCallback,
        IoWriteAllCallback,
        IoFinishCallback,
        ReadMemoryFailureCallback,
        SecondaryFlagsCallback
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    unsafe struct MINIDUMP_THREAD_CALLBACK
    {
        public uint ThreadId;
        public IntPtr ThreadHandle;
        public fixed byte Context[DumpNativeMethods.CONTEXT_SIZE];
        public uint SizeOfContext;
        public ulong StackBase;
        public ulong StackEnd;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct MINIDUMP_THREAD_EX_CALLBACK
    {
        public MINIDUMP_THREAD_CALLBACK BasePart;
        public ulong BackingStoreBase;
        public ulong BackingStoreEnd;
    }

    enum VS_FIXEDFILEINFO_FileFlags : uint
    {
        VS_FF_DEBUG = 0x00000001,
        VS_FF_INFOINFERRED = 0x00000010,
        VS_FF_PATCHED = 0x00000004,
        VS_FF_PRERELEASE = 0x00000002,
        VS_FF_PRIVATEBUILD = 0x00000008,
        VS_FF_SPECIALBUILD = 0x00000020
    }

    enum VS_FIXEDFILEINFO_FileOSFlags : uint
    {
        VOS_DOS = 0x00010000,
        VOS_NT = 0x00040000,
        VOS__WINDOWS16 = 0x00000001,
        VOS__WINDOWS32 = 0x00000004,
        VOS_OS216 = 0x00020000,
        VOS_OS232 = 0x00030000,
        VOS__PM16 = 0x00000002,
        VOS__PM32 = 0x00000003,
        VOS_UNKNOWN = 0x00000000
    }

    enum VS_FIXEDFILEINFO_FileTypeFlags : uint
    {
        VFT_APP = 0x00000001,
        VFT_DLL = 0x00000002,
        VFT_DRV = 0x00000003,
        VFT_FONT = 0x00000004,
        VFT_STATIC_LIB = 0x00000007,
        VFT_UNKNOWN = 0x00000000,
        VFT_VXD = 0x00000005
    }

    enum VS_FIXEFILEINFO_FileSubTypeFlags : uint
    {
        // If the FileType is VFT_DRV
        VFT2_DRV_COMM = 0x0000000A,
        VFT2_DRV_DISPLAY = 0x00000004,
        VFT2_DRV_INSTALLABLE = 0x00000008,
        VFT2_DRV_KEYBOARD = 0x00000002,
        VFT2_DRV_LANGUAGE = 0x00000003,
        VFT2_DRV_MOUSE = 0x00000005,
        VFT2_DRV_NETWORK = 0x00000006,
        VFT2_DRV_PRINTER = 0x00000001,
        VFT2_DRV_SOUND = 0x00000009,
        VFT2_DRV_SYSTEM = 0x00000007,
        VFT2_DRV_VERSIONED_PRINTER = 0x0000000C,

        // If the FileType is VFT_FONT
        VFT2_FONT_RASTER = 0x00000001,
        VFT2_FONT_TRUETYPE = 0x00000003,
        VFT2_FONT_VECTOR = 0x00000002,

        VFT2_UNKNOWN = 0x00000000
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct VS_FIXEDFILEINFO
    {
        public uint dwSignature;
        public uint dwStrucVersion;
        public uint dwFileVersionMS;
        public uint dwFileVersionLS;
        public uint dwProductVersionMS;
        public uint dwProductVersionLS;
        public uint dwFileFlagsMask;
        public uint dwFileFlags;
        public uint dwFileOS;
        public uint dwFileType;
        public uint dwFileSubtype;
        public uint dwFileDateMS;
        public uint dwFileDateLS;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct MINIDUMP_MODULE_CALLBACK
    {
        public IntPtr FullPath; // This is a PCWSTR
        public ulong BaseOfImage;
        public uint SizeOfImage;
        public uint CheckSum;
        public uint TimeDateStamp;
        public VS_FIXEDFILEINFO VersionInfo;
        public IntPtr CvRecord;
        public uint SizeOfCvRecord;
        public IntPtr MiscRecord;
        public uint SizeOfMiscRecord;
    }

    struct MINIDUMP_INCLUDE_THREAD_CALLBACK
    {
        public uint ThreadId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct MINIDUMP_INCLUDE_MODULE_CALLBACK
    {
        public ulong BaseOfImage;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct MINIDUMP_IO_CALLBACK
    {
        public IntPtr Handle;
        public ulong Offset;
        public IntPtr Buffer;
        public uint BufferBytes;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct MINIDUMP_READ_MEMORY_FAILURE_CALLBACK
    {
        public ulong Offset;
        public uint Bytes;
        public int FailureStatus; // HRESULT
    }

    [Flags]
    enum MINIDUMP_SECONDARY_FLAGS : uint
    {
        MiniSecondaryWithoutPowerInfo = 0x00000001
    }

    [StructLayout(LayoutKind.Explicit)]
    struct MINIDUMP_CALLBACK_INPUT
    {
#if X64
        const int CallbackTypeOffset = 4 + 8;
#else
        const int CallbackTypeOffset = 4 + 4;
#endif
        const int UnionOffset = CallbackTypeOffset + 4;

        [FieldOffset(0)]
        public uint ProcessId;
        [FieldOffset(4)]
        public IntPtr ProcessHandle;
        [FieldOffset(CallbackTypeOffset)]
        public MINIDUMP_CALLBACK_TYPE CallbackType;

        [FieldOffset(UnionOffset)]
        public int Status; // HRESULT
        [FieldOffset(UnionOffset)]
        public MINIDUMP_THREAD_CALLBACK Thread;
        [FieldOffset(UnionOffset)]
        public MINIDUMP_THREAD_EX_CALLBACK ThreadEx;
        [FieldOffset(UnionOffset)]
        public MINIDUMP_MODULE_CALLBACK Module;
        [FieldOffset(UnionOffset)]
        public MINIDUMP_INCLUDE_THREAD_CALLBACK IncludeThread;
        [FieldOffset(UnionOffset)]
        public MINIDUMP_INCLUDE_MODULE_CALLBACK IncludeModule;
        [FieldOffset(UnionOffset)]
        public MINIDUMP_IO_CALLBACK Io;
        [FieldOffset(UnionOffset)]
        public MINIDUMP_READ_MEMORY_FAILURE_CALLBACK ReadMemoryFailure;
        [FieldOffset(UnionOffset)]
        public MINIDUMP_SECONDARY_FLAGS SecondaryFlags;
    }

    enum STATE : uint
    {
        MEM_COMMIT = 0x1000,
        MEM_FREE = 0x10000,
        MEM_RESERVE = 0x2000
    }

    enum TYPE : uint
    {
        MEM_IMAGE = 0x1000000,
        MEM_MAPPED = 0x40000,
        MEM_PRIVATE = 0x20000
    }

    [Flags]
    enum PROTECT : uint
    {
        PAGE_EXECUTE = 0x10,
        PAGE_EXECUTE_READ = 0x20,
        PAGE_EXECUTE_READWRITE = 0x40,
        PAGE_EXECUTE_WRITECOPY = 0x80,
        PAGE_NOACCESS = 0x01,
        PAGE_READONLY = 0x02,
        PAGE_READWRITE = 0x04,
        PAGE_WRITECOPY = 0x08,
        PAGE_TARGETS_INVALID = 0x40000000,
        PAGE_TARGETS_NO_UPDATE = 0x40000000,

        PAGE_GUARD = 0x100,
        PAGE_NOCACHE = 0x200,
        PAGE_WRITECOMBINE = 0x400
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct MINIDUMP_MEMORY_INFO
    {
        public ulong BaseAddress;
        public ulong AllocationBase;
        public uint AllocationProtect;
        public uint __alignment1;
        public ulong RegionSize;
        public STATE State;
        public PROTECT Protect;
        public TYPE Type;
        public uint __alignment2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct MemoryCallbackOutput
    {
        public ulong MemoryBase;
        public uint MemorySize;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct CancelCallbackOutput
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool CheckCancel;
        [MarshalAs(UnmanagedType.Bool)]
        public bool Cancel;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct MemoryInfoCallbackOutput
    {
        public MINIDUMP_MEMORY_INFO VmRegion;
        [MarshalAs(UnmanagedType.Bool)]
        public bool Continue;
    }

    [Flags]
    enum THREAD_WRITE_FLAGS : uint
    {
        ThreadWriteThread = 0x0001,
        ThreadWriteStack = 0x0002,
        ThreadWriteContext = 0x0004,
        ThreadWriteBackingStore = 0x0008,
        ThreadWriteInstructionWindow = 0x0010,
        ThreadWriteThreadData = 0x0020,
        ThreadWriteThreadInfo = 0x0040
    }

    [Flags]
    enum MODULE_WRITE_FLAGS : uint
    {
        ModuleWriteModule = 0x0001,
        ModuleWriteDataSeg = 0x0002,
        ModuleWriteMiscRecord = 0x0004,
        ModuleWriteCvRecord = 0x0008,
        ModuleReferencedByMemory = 0x0010,
        ModuleWriteTlsData = 0x0020,
        ModuleWriteCodeSegs = 0x0040
    }

    [StructLayout(LayoutKind.Explicit, Pack = 4)]
    struct MINIDUMP_CALLBACK_OUTPUT
    {
        [FieldOffset(0)]
        public MODULE_WRITE_FLAGS ModuleWriteFlags;
        [FieldOffset(0)]
        public THREAD_WRITE_FLAGS ThreadWriteFlags;
        [FieldOffset(0)]
        public uint SecondaryFlags;
        [FieldOffset(0)]
        public MemoryCallbackOutput Memory;
        [FieldOffset(0)]
        public CancelCallbackOutput Cancel;
        [FieldOffset(0)]
        public IntPtr Handle;
        [FieldOffset(0)]
        public MemoryInfoCallbackOutput MemoryInfo;
        [FieldOffset(0)]
        public int Status; // HRESULT
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    delegate bool MINIDUMP_CALLBACK_ROUTINE(
        [In] IntPtr CallbackParam,
        [In] ref MINIDUMP_CALLBACK_INPUT CallbackInput,
        [In, Out] ref MINIDUMP_CALLBACK_OUTPUT CallbackOutput
        );

    struct MINIDUMP_CALLBACK_INFORMATION
    {
        public MINIDUMP_CALLBACK_ROUTINE CallbackRoutine;
        public IntPtr CallbackParam;
    }

    [Flags]
    enum ProcessAccessFlags : uint
    {
        All = 0x001F0FFF,
        Terminate = 0x00000001,
        CreateThread = 0x00000002,
        VirtualMemoryOperation = 0x00000008,
        VirtualMemoryRead = 0x00000010,
        VirtualMemoryWrite = 0x00000020,
        DuplicateHandle = 0x00000040,
        CreateProcess = 0x000000080,
        SetQuota = 0x00000100,
        SetInformation = 0x00000200,
        QueryInformation = 0x00000400,
        QueryLimitedInformation = 0x00001000,
        Synchronize = 0x00100000
    }

    class DumpNativeMethods
    {
#if X64
        public const int CONTEXT_SIZE = 1232;
#else
        public const int CONTEXT_SIZE = 716;
#endif

        [DllImport("dbghelp.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MiniDumpWriteDump(
            IntPtr hProcess,
            uint ProcessId,
            IntPtr hFile,
            MINIDUMP_TYPE DumpType,
            [In] ref MINIDUMP_EXCEPTION_INFORMATION ExceptionParam,
            [In] ref MINIDUMP_USER_STREAM_INFORMATION UserStreamParam,
            [In] ref MINIDUMP_CALLBACK_INFORMATION CallbackParam
            );

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        public static extern IntPtr OpenProcess(
            ProcessAccessFlags dwDesiredAccess,
            bool bInheritHandle,
            uint dwProcessId
            );

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr Handle);
    }
#pragma warning restore 0649
}
