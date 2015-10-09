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

        // FUTURE
        //[Option('r', HelpText = "Dump using a clone.")]
        //public bool CloneProcess { get; set; }

        [Option('e', HelpText ="Write a dump when the process encounters an unhandled exception. " +
            "Include the 1 to create dump on first chance exceptions, include the 2 to create dump on second chance exceptions.")]
        public int DumpOnException { get; set; }

        [Option('b', HelpText = "Treat debug breakpoints as exceptions (otherwise ignore them).")]
        public bool TreatBreakpointAsException { get; set; }

        [Option('l', HelpText = "Display the debug logging of the process + diagnostics info from the minidumper.")]
        public bool Verbose { get; set; }

        [Option('f', HelpText = "Filter on the content of exceptions and debug logging. Wildcards (*) are supported.")]
        public String ExceptionFilter { get; set; }

        [Option('x', HelpText = "Launch the specified image with optional arguments.")]
        public String DumpFolderForNewlyStartedProcess { get; set; }

        [Option('n', HelpText = "Number of dumps to write before exiting.", Default = 1)]
        public int NumberOfDumps { get; set; }

        [Option('t', HelpText = "Write a dump when the process terminates.")]
        public bool DumpOnProcessTerminate { get; set; }

        [Value(0, Required = true)]
        public String ProcessInfo { get; set; }

        [Value(0, Required = false)]
        public IList<String> Args { get; set; }
    }
}
