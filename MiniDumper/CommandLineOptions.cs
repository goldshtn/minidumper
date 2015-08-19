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
        [Option("pid", HelpText = "The id of the process to dump.")]
        public int ProcessId { get; set; }

        [Option("pn", HelpText = "The name of the process to dump.")]
        public string ProcessName { get; set; }

        [Option('f', Required = true, HelpText = "The name of the dump file to create.")]
        public string DumpFileName { get; set; }

        [Option("mini", HelpText =
            "Create a minimal dump file, enough to diagnose crashes and display call stacks.")]
        public bool MinimalDump { get; set; }

        [Option("heap", HelpText = 
            "Create a dump file with the CLR heap, but without module code or unmanaged memory contents.")]
        public bool MinimalDumpWithCLRHeap { get; set; }

        [Option("full", HelpText =
            "Create a complete dump file with the full memory address space.")]
        public bool FullDump { get; set; }

        [Option('v', HelpText = "Get detailed diagnostic output from the dump capturing process.")]
        public bool Verbose { get; set; }
    }
}
