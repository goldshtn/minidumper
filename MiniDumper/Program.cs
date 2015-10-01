using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DumpWriter;
using System.Diagnostics;
using System.IO;
using Microsoft.Diagnostics.Runtime.Interop;
using Microsoft.Diagnostics.Runtime;
using System.Runtime.InteropServices;

namespace MiniDumper
{
    class Native
    {
        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DebugSetProcessKillOnExit([In] bool flag);
    }

    class Program
    {
        static void TakeDumps(CommandLineOptions options)
        {
            ValidateOptions(options);
            var pid = GetTargetProcessId(options);

            // FIXME nicely print defined options
            /*
ProcDump v7.1 - Writes process dump files
Copyright (C) 2009-2014 Mark Russinovich
Sysinternals - www.sysinternals.com
With contributions from Andrew Richards

Process:               Notepad2.exe (10028)
CPU threshold:         n/a
Performance counter:   n/a
Commit threshold:      n/a
Threshold seconds:     10
Hung window check:     Disabled
Log debug strings:     Disabled
Exception monitor:     First Chance+Unhandled
Exception filter:      *
Terminate monitor:     Disabled
Cloning type:          Disabled
Concurrent limit:      n/a
Avoid outage:          n/a
Number of dumps:       1
Dump folder:           D:\temp\
Dump filename/mask:    PROCESSNAME_YYMMDD_HHMMSS


Press Ctrl-C to end monitoring without terminating the process.


^C
[17:55:23] Dump count not reached.*/

            Console.WriteLine("Dumping process id {0}...", pid);

            var dbg = new DbgEngDataReader("notepad", "c:\\temp");

            // setup listener
            var dbglstn = new DebuggerListener();
            dbglstn.ModuleLoadEvent += (d, ev) => {
                Console.WriteLine("Module loaded: {0}", ev.ImageName);
            };

            dbglstn.StartListening(dbg.DebuggerInterface);

            Console.CancelKeyPress += (o, ev) => {
                // FIXME this probably needs to go to a seperate thread 
                // and should stop the event loop
                dbglstn.StopListening(dbg.DebuggerInterface);
                dbg.Dispose();
            };

            DEBUG_STATUS status;
            do {
                status = dbg.ProcessEvents(0xffff);
            } while (status != DEBUG_STATUS.NO_DEBUGGEE);

            // FIXME Better communication - add Ctrl + C handler
            //var dumper = new DumpWriter.DumpWriter(options.Verbose ? Console.Out : null);

            //int numberOfDumpsTaken = 0;
            //while (numberOfDumpsTaken < options.NumberOfDumps) {

                // FIXME dump logic here
                //dumper.Dump(
                //    pid,
                //    OptionToDumpType(options),
                //    options.DumpFileName,
                //    writeAsync: options.Async
                //    );
                //Console.WriteLine("Dump generated successfully, file size {0:N0} bytes",
                //    new FileInfo(options.DumpFileName).Length);
            //}
        }

        private static DumpType OptionToDumpType(CommandLineOptions options)
        {
            if (options.MinimalDump)
                return DumpType.Minimal;
            if (options.MinimalDumpWithCLRHeap)
                return DumpType.MinimalWithFullCLRHeap;
            if (options.FullDump)
                return DumpType.FullMemory;

            // Should never get here
            return DumpType.MinimalWithFullCLRHeap;
        }

        private static String GetDumpFolderPath(CommandLineOptions options)
        {
            // FIXME use the provided folder or the folder of the EXE file
            return null;
        }

        private static int GetTargetProcessId(CommandLineOptions options)
        {
            if (options.ProcessInfo != null) {
                int pid;
                if (Int32.TryParse(options.ProcessInfo, out pid)) {
                    return pid;
                }
                // not numeric - let's try to find it by name
                var procs = Process.GetProcessesByName(options.ProcessInfo);
                if (procs.Length == 1) {
                    return procs[0].Id;
                }
                if (procs.Length > 1) {
                    throw new ArgumentException("There is more than one process with the specified name");
                }
                // final try - let's try creating it
                // FIXME
            }
            throw new ArgumentException("Couldn't find or create a process.");
        }

        private static void ValidateOptions(CommandLineOptions options)
        {
            // FIXME add new validation rules
            //int set = 0;
            //set += options.FullDump ? 1 : 0;
            //set += options.MinimalDump ? 1 : 0;
            //set += options.MinimalDumpWithCLRHeap ? 1 : 0;
            //if (set == 0) {
            //    throw new ArgumentException("No dump option specified");
            //}
            //if (set > 1) {
            //    throw new ArgumentException("More than one dump option specified");
            //}

            //if (options.ProcessId == 0 && String.IsNullOrEmpty(options.ProcessName))
            //    throw new ArgumentException("Either a process id or process name is required");

            //if (options.ProcessId != 0 && !String.IsNullOrEmpty(options.ProcessName))
            //    throw new ArgumentException("Expecting either process id or process name, not both");

            //if (File.Exists(options.DumpFileName))
            //    throw new ArgumentException("The specified file already exists");
        }

        static void Main(string[] args)
        {
            //var dumpType = DumpType.MinimalWithFullCLRHeap;
            //bool dumpOnFirstChanceException = false, dumpOnSecondChanceException = false, 
            //    treatBreakpointAsException = false, cloneProcess = false, installAsSystemDebugger = false, 
            //    showsDebugLogs = false, overwriteExistingDumpFile = false, dumpOnTermination = false, 
            //    waitForProcess = false, showHelp = false;
            //String exceptionFilter = null;


            //var p = new OptionSet {
            //    { "b", "Treat debug breakpoints as exceptions (otherwise ignore them).", v => treatBreakpointAsException = v != null },
            //    { "e=", "Treat debug breakpoints as exceptions (otherwise ignore them).", v => treatBreakpointAsException = v != null },
            //    { "log=", "a path to the folder where transaction log backups are stored", v => logPath = v },
            //    { "h|help", "show help usage", v => showHelp = v != null },
            //    { "m|mirror", "if the database is mirrored we will restore the mirroring session", v => checkMirror = v != null },
            //};

            try {
                var result = Parser.Default.ParseArguments<CommandLineOptions>(args);
                result.WithParsed(TakeDumps);
            } catch (Exception ex) {
                Console.Error.WriteLine("ERROR: {0}", ex.Message);
            }
        }
    }
}
