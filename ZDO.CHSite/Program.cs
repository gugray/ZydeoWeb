using System;
using System.Diagnostics;
using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace ZDO.CHSite
{
    public class Program
    {
        private static int workOffline(string[] args)
        {
            try
            {
                OfflineTool ot = new OfflineTool();
                if (args[1] == "recreate-db") ot.RecreateDB();
                else if (args[1] == "import-freq") ot.ImportFreq(args[2]);
                else if (args[1] == "import-dict") ot.ImportDict(args[2], args[3]);
                else if (args[1] == "bulkadd") ot.BulkAdd(args[2], args[3]);
                else throw new Exception("Unrecognized task: " + args[1]);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return -1;
            }
        }

        public static int Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "--task")
            {
                int res = workOffline(args);
                if (Debugger.IsAttached) Console.ReadLine();
                return res;
            }

            // What port are we listening at? Comes from environment variable.
            string portStr = Environment.GetEnvironmentVariable("ZDO_PORT");
            if (portStr == null) portStr = "";
            else portStr = ":" + portStr;

            var host = new WebHostBuilder()
               .UseUrls("http://0.0.0.0" + portStr)
               .UseKestrel()
               .UseContentRoot(Directory.GetCurrentDirectory())
               .ConfigureLogging(x => { })
               .UseStartup<Startup>()
               .CaptureStartupErrors(true)
               .Build();
            host.Run();
            return 0;
        }
    }
}
