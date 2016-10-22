using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using ZD.Tool.Examine;

namespace ZD.Tool
{
    class Program
    {
        private static void writeInfo()
        {
            Console.WriteLine("Invalid arguments. Usage:");
            Console.WriteLine();
            Console.WriteLine("--examine <dict-file> <out-diag-file> <failed-roundtrip-file>");
            Console.WriteLine("  Parses dictionary in CEDICT format, logs anomalies.");
            Console.WriteLine();
        }

        private static object parseArgs(string[] args)
        {
            if (args[0] == "--examine")
            {
                if (args.Length != 4) return null;
                OptExamine opt = new OptExamine
                {
                    DictFileName = args[1],
                    DiagFileName = args[2],
                    RoundtripFileName = args[3],
                };
                return opt;
            }
            return null;
        }

        private static IWorker createWorker(object opt)
        {
            if (opt is OptExamine) return new WrkExamine(opt as OptExamine);
            throw new Exception(opt.GetType().ToString() + " is not recognized as an options type.");
        }

        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                writeInfo();
                if (Debugger.IsAttached) { Console.WriteLine("Press Enter..."); Console.ReadLine(); }
                return -1;
            }

            object opt = parseArgs(args);
            if (opt == null)
            {
                writeInfo();
                if (Debugger.IsAttached) { Console.WriteLine("Press Enter..."); Console.ReadLine(); }
                return -1;
            }

            IWorker worker = createWorker(opt);
            try
            {
                worker.Init();
                worker.Work();
                worker.Finish();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                if (Debugger.IsAttached) { Console.WriteLine("Press Enter..."); Console.ReadLine(); }
                return -1;
            }
            finally
            {
                worker.Dispose();
                if (Debugger.IsAttached) { Console.WriteLine("Press Enter..."); Console.ReadLine(); }
            }
            return 0;
        }
    }
}
