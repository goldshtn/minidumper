using CommandLine;
using System.Collections.Generic;

namespace MiniDumper
{
    class CommandLineOptions
    {
        [Option('m', HelpText =
            "Create a dump file, second paramer: \r\n" +
            "    -mm minidump enough to diagnose crashes and display call stacks.\r\n" +
            "    -mh dump file with the CLR heap, but without module code or unmanaged memory contents\r\n" +
            "    -ma complete dump file with the full memory address space", Required = true)]
        public char DumpType { get; set; }

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

        [Option('l', HelpText = "Display the debug logging of the process + diagnostics info from the minidumper.")]
        public bool Verbose { get; set; }

        [Option('y', HelpText = "Memory commit threshold in MB at which to create a dump.")]
        public uint? MemoryCommitThreshold { get; set; }
        
        [Option('z', HelpText = "Memory commit threshold in MB at which to create a dump.")]
        public uint? MemoryCommitDrops { get; set; }

        [Option('f', HelpText = "Filter on the content of exceptions and debug logging. Wildcards (*) are supported.")]
        public string ExceptionFilter { get; set; }

        [Option('x', HelpText = "Launch the specified image with optional arguments.")]
        public string DumpFolderForNewlyStartedProcess { get; set; }

        [Option('n', HelpText = "Number of dumps to write before exiting.", Default = 1)]
        public int NumberOfDumps { get; set; }

        [Option('t', HelpText = "Write a dump when the process terminates.")]
        public bool DumpOnProcessTerminate { get; set; }

        [Option('c', HelpText = "Start the process in a new console window.")]
        public bool StartProcessInNewConsoleWindow { get; set; }

        [Value(0, Required = true, HelpText = "PID or process name")]
        public string ProcessInfo { get; set; }

        [Value(0, Required = false, HelpText = "Arguments for the process to start")]
        public IList<string> Args { get; set; }

        public bool NoDumpOptionSelected
        {
            get { return !DumpOnProcessTerminate && DumpOnException == 0; }
        }
    }
}
