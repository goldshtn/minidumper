using CommandLine;
using DumpWriter;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Desktop;
using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace MiniDumper
{
    static class Program
    {
        static void Main(string[] args)
        {
            try {
                var result = Parser.Default.ParseArguments<CommandLineOptions>(args);
                result.WithParsed(TakeDumps);
            } catch (Exception ex) {
                Console.Error.WriteLine("ERROR: {0}", ex.Message);
            }
        }

        static void TakeDumps(CommandLineOptions options)
        {
            ValidateOptions(options);

            // parse options
            var logger = options.Verbose ? Console.Out : TextWriter.Null;
            var dumpFolder = options.DumpFolderForNewlyStartedProcess ?? Path.GetDirectoryName(
                Process.GetCurrentProcess().MainModule.FileName);

            // create client
            Guid guid = new Guid("27fe5639-8407-4f47-8364-ee118fb08ac8");
            object obj;
            NativeMethods.DebugCreate(ref guid, out obj);
            var client = (IDebugClient5)obj;

            // FIXME gather information about the process, extract 64bit version and dbghelp library


            // process description
            int pid;
            IntPtr hProcess = IntPtr.Zero;
            string processName;

            // setup listener
            bool detached = false;
            var listener = new DebuggerListener(logger);
            listener.CreateProcessEvent += (d, ev) => {
                hProcess = (IntPtr)ev.Handle;
                pid = Native.GetProcessId(hProcess);
            };
            listener.ExitProcessEvent += (d, ev) => {
                detached = true;
            };
            client.SetEventCallbacks(listener);
            client.SetOutputCallbacks(listener);

            // create process
            CreateProcess(client, options.ProcessInfo, options.Args, !String.IsNullOrEmpty(
                options.DumpFolderForNewlyStartedProcess), out pid, out hProcess, out processName);

            // banner
            ShowBanner(options, pid, processName, dumpFolder);

            // create minidumper and attach its events
            var minidumper = CreateAndAttachMiniDumper(client, listener, 
                dumpFolder, pid, hProcess, processName, logger, options);

            // setup Ctrl+C listener
            Console.CancelKeyPress += (o, ev) => {
                Console.WriteLine("Ctrl + C received - detaching from a process");
                if (!detached) {
                    //detached = true;
                    //DetachProcess(minidumper, client);
                    minidumper.NumberOfDumpsTaken = options.NumberOfDumps;
                    ev.Cancel = true;
                }
            };

            // main debug event loop
            var control = (IDebugControl2)client;
            Debug.Assert(control != null);
            DEBUG_STATUS status;
            do {
                int hr = control.WaitForEvent(0, 1000);
                if (detached) {
                    // we are done
                    return;
                }
                if (minidumper.NumberOfDumpsTaken >= options.NumberOfDumps) {
                    DetachProcess(minidumper, client);
                    return;
                }
                if (hr < 0 && (uint)hr != 0x8000000A) {
                    throw new ClrDiagnosticsException(String.Format("IDebugControl::WaitForEvent: 0x{0:x8}", hr), ClrDiagnosticsException.HR.DebuggerError);
                }

                hr = control.GetExecutionStatus(out status);
                if (hr < 0)
                    throw new ClrDiagnosticsException(String.Format("IDebugControl::GetExecutionStatus: 0x{0:x8}", hr), ClrDiagnosticsException.HR.DebuggerError);
            } while (status != DEBUG_STATUS.NO_DEBUGGEE && !detached);
        }

        static void ValidateOptions(CommandLineOptions options)
        {
            if (String.IsNullOrEmpty(options.ProcessInfo)) {
                throw new ArgumentException("Either a process id or process name is required");
            }
            // file name and dump folder
            if (options.DumpFolderForNewlyStartedProcess != null &&
                !Directory.Exists(options.DumpFolderForNewlyStartedProcess)) {
                throw new ArgumentException("The specified dump folder does not exist.");
            }
        }

        static void CreateProcess(IDebugClient5 client, string processInfo, IList<string> args, bool spawnNew, out int pid,
            out IntPtr hProcess, out string processName)
        {
            hProcess = IntPtr.Zero;
            processName = null;

            if (Int32.TryParse(processInfo, out pid)) {
                hProcess = Process.GetProcessById(pid).Handle;
            } else {
                // not numeric - let's try to find it by name
                var procs = Process.GetProcesses();
                foreach (var proc in procs) {
                    try {
                        if (processInfo.Equals(proc.MainModule.ModuleName, StringComparison.OrdinalIgnoreCase)) {
                            pid = proc.Id;
                            hProcess = proc.Handle;
                            processName = proc.MainModule.ModuleName;
                            break;
                        }
                    } catch {
                        // just ignore it
                    }
                }
            }
            if (pid > 0) {
                Debug.Assert(hProcess != IntPtr.Zero);
                int hr;
                // process found - let's attach to it
                hr = client.AttachProcess(0, (uint)pid, DEBUG_ATTACH.DEFAULT);
                if (hr < 0) {
                    Marshal.ThrowExceptionForHR(hr);
                }
                hr = ((IDebugControl2)client).WaitForEvent(0, 10000);
                if (hr < 0 && (uint)hr != 0x8000000A) {
                    Marshal.ThrowExceptionForHR(hr);
                }
                return;
            }
            if (spawnNew) {
                // final try - let's try creating it (but only if -x option is set)
                var commandLine = processInfo + " " + String.Join(" ", args ?? new string[0]);
                var createProcessOptions = new DEBUG_CREATE_PROCESS_OPTIONS();
                createProcessOptions.CreateFlags = DEBUG_CREATE_PROCESS.DEBUG_ONLY_THIS_PROCESS;
                int hr = client.CreateProcessAndAttach2(0, commandLine, ref createProcessOptions,
                    (uint)Marshal.SizeOf(typeof(DEBUG_CREATE_PROCESS_OPTIONS)), null, null, 0, DEBUG_ATTACH.DEFAULT);
                if (hr < 0) {
                    Marshal.ThrowExceptionForHR(hr);
                }
                // wait for the first event (create process)
                hr = ((IDebugControl2)client).WaitForEvent(0, 1000);
                if (hr < 0 && (uint)hr != 0x8000000A) {
                    Marshal.ThrowExceptionForHR(hr);
                }
                processName = GetProcessName(client);
                return;
            }
            throw new ArgumentException("Something is wrong with the arguments - I wasn't able to create a process.");
        }

        static void DetachProcess(MiniDumper minidumper, IDebugClient5 client)
        {
            client.SetEventCallbacks(null);
            client.SetOutputCallbacks(null);
            minidumper.Dispose();
            int size; 
            if (((IDebugAdvanced2)client).Request(DEBUG_REQUEST.TARGET_CAN_DETACH, null, 0, null, 0, out size) != 0) {
                Console.WriteLine("FIXME: I'm afraid can't detach and will wait for the process to end.");
            }
            int hr = client.DetachCurrentProcess();
            if (hr != 0) {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        static void ShowBanner(CommandLineOptions options, int pid, string processName, string dumpFolder)
        {
            Console.WriteLine("MiniDumper - writes .NET process dump files");
            Console.WriteLine("Copyright (C) 2015 Sasha Goldstein (@goldshtn)");
            Console.WriteLine();
            Console.WriteLine("With contributions from Sebastian Solnica (@lowleveldesign)");
            Console.WriteLine();

            Console.WriteLine("Process:             {0} ({1})", processName, pid);
            Console.WriteLine("Exception monitor:   {0}", options.DumpOnException == 1 ? "First Chance+Unhandled" :
                (options.DumpOnException == 2 ? "Unhandled" : "Disabled"));
            Console.WriteLine("Exception filter:    {0}", options.ExceptionFilter ?? "*");
            Console.WriteLine("Dump folder:         {0}", dumpFolder);
            Console.WriteLine("Number of dumps:     {0}", options.NumberOfDumps);
            Console.WriteLine("Dump filename/mask:  PROCESSNAME_YYMMDD_HHMMSS");
            Console.WriteLine("Terminal monitor:    {0}", options.DumpOnProcessTerminate ? "Enabled" : "Disabled");
            Console.WriteLine("Debug output:        {0}", options.Verbose ? "Enabled" : "Disabled");
            Console.WriteLine();
            Console.WriteLine("Press Ctrl-C to end monitoring without terminating the process.");
            Console.WriteLine();
        }

        static string GetProcessName(IDebugClient client)
        {
            var buffer = new StringBuilder(30);
            uint size;
            ((IDebugSystemObjects)client).GetCurrentProcessExecutableName(buffer, buffer.Capacity, out size);
            return buffer.ToString(0, (int)size - 1);
        }

        static MiniDumper CreateAndAttachMiniDumper(IDebugClient5 client, DebuggerListener listener, String dumpFolder,
            int pid, IntPtr hProcess, string processName, TextWriter logger, CommandLineOptions options)
        {
            var miniDumper = new MiniDumper(client, dumpFolder, pid, hProcess, processName, logger,
                OptionToDumpType(options), options.Async, options.ExceptionFilter);
            if (options.DumpOnException >= 1) {
                if (options.DumpOnException == 1) {
                    listener.FirstChanceExceptionEvent += miniDumper.DumpOnException;
                } else {
                    listener.SecondChanceExceptionEvent += miniDumper.DumpOnException;
                }
            }
            if (options.DumpOnProcessTerminate) {
                listener.ExitProcessEvent += miniDumper.DumpOnProcessExit;
            }
            return miniDumper;
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
