using CommandLine;
using DumpWriter;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using VsChromium.Core.Win32;
using VsChromium.Core.Win32.Debugging;
using VsChromium.Core.Win32.Processes;
using DebuggingNativeMethods = VsChromium.Core.Win32.Debugging.NativeMethods;
using ProcessNativeMethods = VsChromium.Core.Win32.Processes.NativeMethods;

namespace MiniDumper
{
    class Debugger
    {
        static void Main(string[] args)
        {
            try {
                var result = Parser.Default.ParseArguments<CommandLineOptions>(args);
                result.WithParsed(options => new Debugger(options).TakeDumps());
            } catch (Exception ex) {
                Console.Error.WriteLine("ERROR: {0}", ex.Message);
            }
        }

        private readonly CommandLineOptions _options;
        private readonly TextWriter _logger;
        private readonly string _dumpFolder;
        private bool _detached;
        private bool _shouldDeatch;
        private int _pid;
        private string _processName;

        public Debugger(CommandLineOptions options)
        {
            ValidateOptions(options);
            _options = options;
            _logger = options.Verbose ? Console.Out : TextWriter.Null;
            _dumpFolder = options.DumpFolderForNewlyStartedProcess ?? Path.GetDirectoryName(
                Process.GetCurrentProcess().MainModule.FileName);
        }

        void TakeDumps()
        {
            CreateProcess();

            ShowBanner();

            // setup Ctrl+C listener
            Console.CancelKeyPress += (o, ev) => {
                Console.WriteLine("Ctrl + C received - detaching from a process");
                ev.Cancel = true;
                _shouldDeatch = true; 
            };
            WaitForDebugEvents();
        }

        private void WaitForDebugEvents()
        {
            using (var miniDumper = CreateMiniDumper()) {
                while (!_detached) {
                    var debugEvent = WaitForDebugEvent(1000);
                    if (_shouldDeatch) {
                        DetachProcess();
                        return;
                    }
                    if (debugEvent.HasValue) {
                        switch (debugEvent.Value.dwDebugEventCode) {
                            case DEBUG_EVENT_CODE.EXIT_PROCESS_DEBUG_EVENT:
                                if (_options.DumpOnProcessTerminate) {
                                    miniDumper.DumpOnProcessExit(debugEvent.Value.ExitProcess.dwExitCode);
                                }
                                _shouldDeatch = true;
                                break;
                            case DEBUG_EVENT_CODE.EXCEPTION_DEBUG_EVENT:
                                var exception = debugEvent.Value.Exception;
                                if (_options.DumpOnException == 1 && exception.dwFirstChance == 1 ||
                                    _options.DumpOnException == 2 && exception.dwFirstChance == 0) {
                                    miniDumper.DumpOnException((uint)debugEvent.Value.dwThreadId, exception.ExceptionRecord);
                                }
                                break;
                            default:
                                break;
                        }
                        if (!_shouldDeatch && miniDumper.NumberOfDumpsTaken >= _options.NumberOfDumps) {
                            Console.WriteLine("Number of dumps exceeded the specified limit - detaching.");
                            _shouldDeatch = true;
                        }
                        if (_shouldDeatch) {
                            DetachProcess();
                            return;
                        }
                        if (_detached) {
                            return;
                        }
                        var continueStatus = HandleDebugEvent(debugEvent.Value);
                        if (!DebuggingNativeMethods.ContinueDebugEvent(debugEvent.Value.dwProcessId,
                            debugEvent.Value.dwThreadId, continueStatus)) {
                            throw new LastWin32ErrorException("Error in ContinueDebugEvent");
                        }
                    }
                }
            }
        }

        private static DEBUG_EVENT? WaitForDebugEvent(uint timeout)
        {
            DEBUG_EVENT debugEvent;
            var success = DebuggingNativeMethods.WaitForDebugEvent(out debugEvent, timeout);
            if (!success) {
                int hr = Marshal.GetHRForLastWin32Error();
                if (hr == HResults.HR_ERROR_SEM_TIMEOUT)
                    return null;

                Marshal.ThrowExceptionForHR(hr);
            }
            return debugEvent;
        }

        private static CONTINUE_STATUS HandleDebugEvent(DEBUG_EVENT value)
        {
            if (value.dwDebugEventCode == DEBUG_EVENT_CODE.EXCEPTION_DEBUG_EVENT) {
                return CONTINUE_STATUS.DBG_EXCEPTION_NOT_HANDLED;
            }
            return CONTINUE_STATUS.DBG_CONTINUE;
        }

        static void ValidateOptions(CommandLineOptions options)
        {
            if (string.IsNullOrEmpty(options.ProcessInfo)) {
                throw new ArgumentException("Either a process id or process name is required");
            }
            // file name and dump folder
            if (options.DumpFolderForNewlyStartedProcess != null &&
                !Directory.Exists(options.DumpFolderForNewlyStartedProcess)) {
                throw new ArgumentException("The specified dump folder does not exist.");
            }
        }

        void CreateProcess()
        {
            bool spawnNew = !string.IsNullOrEmpty(_options.DumpFolderForNewlyStartedProcess);
            _processName = null;

            int pid;
            if (int.TryParse(_options.ProcessInfo, out pid)) {
                _pid = pid;
            } else {
                // not numeric - let's try to find it by name
                var procs = Process.GetProcesses();
                foreach (var proc in procs) {
                    try {
                        if (_options.ProcessInfo.Equals(proc.MainModule.ModuleName, StringComparison.OrdinalIgnoreCase)) {
                            _pid = proc.Id;
                            _processName = proc.MainModule.ModuleName;
                            break;
                        }
                    } catch {
                        // just ignore it
                    }
                }
            }
            if (_pid > 0) {
                // process found - let's attach to it
                if (!DebuggingNativeMethods.DebugActiveProcess(_pid)) {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }
                return;
            }
            if (spawnNew) {
                // final try - let's try creating it (but only if -x option is set)
                var commandLine = _options.ProcessInfo + " " + string.Join(" ", _options.Args ?? new string[0]);

                var startupInfo = new STARTUPINFO();
                var processInformation = new PROCESS_INFORMATION();
                var processCreationFlags = ProcessCreationFlags.DEBUG_ONLY_THIS_PROCESS;
                if (_options.StartProcessInNewConsoleWindow) {
                    processCreationFlags |= ProcessCreationFlags.CREATE_NEW_CONSOLE;
                }
                bool res = ProcessNativeMethods.CreateProcess(null, new StringBuilder(commandLine),
                    null, null, false, processCreationFlags, IntPtr.Zero, null, 
                    startupInfo, processInformation);
                if (!res) {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }

                if (!DebuggingNativeMethods.DebugSetProcessKillOnExit(false)) {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }
                _pid = processInformation.dwProcessId;
                _processName = GetProcessName(_pid);
                return;
            }
            throw new ArgumentException("Something is wrong with the arguments - couldn't find or create a requested process.");
        }

        void DetachProcess()
        {
            if (_detached) {
                return;
            }
            if (!DebuggingNativeMethods.DebugActiveProcessStop(_pid)) {
                _logger.Write("Exception occured when detaching from the process: {0}",
                    Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
            }
            _detached = true;
        }

        void ShowBanner()
        {
            Console.WriteLine("MiniDumper - writes .NET process dump files");
            Console.WriteLine("Copyright (C) 2015 Sasha Goldstein (@goldshtn)");
            Console.WriteLine();
            Console.WriteLine("With contributions from Sebastian Solnica (@lowleveldesign)");
            Console.WriteLine();

            Console.WriteLine("Process:             {0} ({1})", _processName, _pid);
            Console.WriteLine("Exception monitor:   {0}", _options.DumpOnException == 1 ? "First Chance+Unhandled" :
                (_options.DumpOnException == 2 ? "Unhandled" : "Disabled"));
            Console.WriteLine("Exception filter:    {0}", _options.ExceptionFilter ?? "*");
            Console.WriteLine("Dump folder:         {0}", _dumpFolder);
            Console.WriteLine("Number of dumps:     {0}", _options.NumberOfDumps);
            Console.WriteLine("Dump filename/mask:  PROCESSNAME_YYMMDD_HHMMSS");
            Console.WriteLine("Terminal monitor:    {0}", _options.DumpOnProcessTerminate ? "Enabled" : "Disabled");
            Console.WriteLine("Debug output:        {0}", _options.Verbose ? "Enabled" : "Disabled");
            Console.WriteLine();
            Console.WriteLine("Press Ctrl-C to end monitoring without terminating the process.");
            Console.WriteLine();
        }

        private MiniDumper CreateMiniDumper()
        {
            return new MiniDumper(_dumpFolder, _pid, _processName, _logger,
                OptionToDumpType(_options), _options.Async, _options.ExceptionFilter);
        }

        static string GetProcessName(int processId)
        {
            return Process.GetProcessById(processId).ProcessName;
        }

        static DumpType OptionToDumpType(CommandLineOptions options)
        {
            if (options.DumpType == 'm')
                return DumpType.Minimal;
            if (options.DumpType == 'h')
                return DumpType.MinimalWithFullCLRHeap;
            if (options.DumpType == 'a')
                return DumpType.FullMemory;

            // Should never get here
            return DumpType.MinimalWithFullCLRHeap;
        }
    }
}
