using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DumpWriter;
using System.Diagnostics;
using System.IO;

namespace MiniDumper
{
    class Program
    {
        static void TakeDump(CommandLineOptions options)
        {
            ValidateOptions(options);
            var pid = GetTargetProcessId(options);
            Console.WriteLine("Dumping process id {0}...", pid);
            var dumper = new DumpWriter.DumpWriter(options.Verbose ? Console.Out : null);
            dumper.Dump(
                pid,
                OptionToDumpType(options),
                options.DumpFileName,
                writeAsync: options.Async
                );
            Console.WriteLine("Dump generated successfully, file size {0:N0} bytes",
                new FileInfo(options.DumpFileName).Length);
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

        private static int GetTargetProcessId(CommandLineOptions options)
        {
            if (options.ProcessId != 0)
            {
                // Just to make sure it exists
                var proc = Process.GetProcessById(options.ProcessId);
                return proc.Id;
            }
            else
            {
                var procs = Process.GetProcessesByName(options.ProcessName);
                if (procs.Length == 0)
                    throw new ArgumentException("Couldn't find a process with the specified name");
                if (procs.Length > 1)
                    throw new ArgumentException("There is more than one process with the specified name");
                return procs[0].Id;
            }
        }

        private static void ValidateOptions(CommandLineOptions options)
        {
            int set = 0;
            set += options.FullDump ? 1 : 0;
            set += options.MinimalDump ? 1 : 0;
            set += options.MinimalDumpWithCLRHeap ? 1 : 0;
            if (set == 0)
            {
                throw new ArgumentException("No dump option specified");
            }
            if (set > 1)
            {
                throw new ArgumentException("More than one dump option specified");
            }

            if (options.ProcessId == 0 && String.IsNullOrEmpty(options.ProcessName))
                throw new ArgumentException("Either a process id or process name is required");

            if (options.ProcessId != 0 && !String.IsNullOrEmpty(options.ProcessName))
                throw new ArgumentException("Expecting either process id or process name, not both");

            if (File.Exists(options.DumpFileName))
                throw new ArgumentException("The specified file already exists");
        }

        static void Main(string[] args)
        {
            try
            {
                var result = Parser.Default.ParseArguments<CommandLineOptions>(args);
                result.WithParsed(TakeDump);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: {0}", ex.Message);
            }
        }
    }
}
