using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniDumper
{
    class CommandLineOptions
    {
        // FIXME to remove
        [Option("pid", HelpText = "The id of the process to dump.")]
        public int ProcessId { get; set; }

        // FIXME to remove
        [Option("pn", HelpText = "The name of the process to dump.")]
        public string ProcessName { get; set; }

        // FIXME to remove
        [Option('z', Required = true, HelpText = "The name of the dump file to create.")]
        public string DumpFileName { get; set; }

        [Option("mm", HelpText =
            "Create a minimal dump file, enough to diagnose crashes and display call stacks.")]
        public bool MinimalDump { get; set; }

        [Option("mh", HelpText = 
            "Create a dump file with the CLR heap, but without module code or unmanaged memory contents.")]
        public bool MinimalDumpWithCLRHeap { get; set; }

        [Option("ma", HelpText =
            "Create a complete dump file with the full memory address space.")]
        public bool FullDump { get; set; }

        [Option("async", HelpText =
            "Write dump chunks to disk asynchronously. Reduces process suspension time " +
            "at the expense of higher memory usage.")]
        public bool Async { get; set; }

        [Option('r', HelpText = "Dump using a clone.")]
        public bool CloneProcess { get; set; }

        [Option('h', HelpText = "Write dump if a process has a hung window (does not respond to window " +
            "messages for at least 5 seconds)")]
        public bool DumpOnWindowHang { get; set; }

        [Option('e', Default = 2, HelpText ="Write a dump when the process encounters an unhandled exception. " +
            "Include the 1 to create dump on first chance exceptions.")]
        public byte DumpOnException { get; set; }

        [Option('b', HelpText = "Treat debug breakpoints as exceptions (otherwise ignore them).")]
        public bool TreatBreakpointAsException { get; set; }

        [Option('l', HelpText = "Display the debug logging of the process + diagnostics info from the minidumper.")]
        public bool Verbose { get; set; }

        [Option('f', HelpText = "Filter on the content of exceptions and debug logging. Wildcards (*) are supported.")]
        public String ExceptionFilter { get; set; }

        [Option('x', HelpText = "Launch the specified image with optional arguments.")]
        public String DumpFolderForNewlyStartedProcess { get; set; }

        [Option('n', HelpText = "Number of dumps to write before exiting.")]
        public int NumberOfDumps { get; set; }

        [Option('o', HelpText = "Overwrite an existing dump file.")]
        public bool OverwriteExistingFile { get; set; }

        [Option('i', HelpText = "Install as the AeDebug postmortem debugger.")]
        public String DumpFolderForInstalledVersion { get; set; }

        [Option('u', HelpText = "Uninstalls minidumper, restores previous settings for the AeDebug.")]
        public bool Uninstall { get; set; }

        [Value(0, Required = true)]
        public String ProcessInfo { get; set; }

        [Value(1, Required = false)]
        public String[] Arguments { get; set; }
    }
}
