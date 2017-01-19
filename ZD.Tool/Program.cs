using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace ZD.Tool
{
    class Program
    {
        private static void writeInfo()
        {
            Console.WriteLine("Invalid arguments. Usage:");
            Console.WriteLine();
            Console.WriteLine("--examine");
            Console.WriteLine("  Parses dictionary in CEDICT format, logs anomalies and stats.");
            Console.WriteLine("  Input name fixed: handedict.u8");
            Console.WriteLine("--10-prepare");
            Console.WriteLine("  Converts original HanDeDict file into enriched format, dropping entries with errors.");
            Console.WriteLine("  Input name fixed: handedict.u8");
            Console.WriteLine("--20-cleanse");
            Console.WriteLine("  Cleanses HDD data");
            Console.WriteLine("  Input name fixed: x-10-handedict.txt");
            Console.WriteLine("--compile-hwinfo");
            Console.WriteLine("  Compiles headword info file");
            Console.WriteLine("  Fixed inputs: Unihan_Readings.txt; Unihan_Variants.txt; cedict_ts.u8; handedict.u8; makemeahanzi.txt");
            Console.WriteLine("  Outputs unihanzi.bin");
            Console.WriteLine("--tobytes");
            Console.WriteLine("  Converts binary file to byte array");
            Console.WriteLine("  Input name fixed: medians.bin");
            Console.WriteLine("  Outputs chardata.js");
            Console.WriteLine();
        }

        private static object parseArgs(string[] args)
        {
            if (args[0] == "--examine") return args[0];
            if (args[0] == "--10-prepare") return args[0];
            if (args[0] == "--20-cleanse") return args[0];
            if (args[0] == "--compile-hwinfo") return args[0];
            if (args[0] == "--tobytes") return args[0];
            return null;
        }

        private static IWorker createWorker(object opt)
        {
            if (opt is string)
            {
                if (opt as string == "--examine") return new WrkExamine();
                if (opt as string == "--10-prepare") return new Wrk10Prepare();
                if (opt as string == "--20-cleanse") return new Wrk20Cleanse();
                if (opt as string == "--compile-hwinfo") return new WrkUnihanzi();
                if (opt as string == "--tobytes") return new WrkToBytes();
            }
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
