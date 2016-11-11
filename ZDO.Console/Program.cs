using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace ZDO.CHSite
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
               .UseUrls("http://0.0.0.0:5099")
               .UseKestrel()
               .UseContentRoot(Directory.GetCurrentDirectory())
               .ConfigureLogging(x => { })
               .UseStartup<Startup>()
               .CaptureStartupErrors(true)
               .Build();
            host.Run();
        }
    }
}
