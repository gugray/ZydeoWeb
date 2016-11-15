using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;

using ZDO.CHSite.Logic;

namespace ZDO.CHSite
{
    public class OfflineTool
    {
        private readonly Mutation mut;
        private readonly HostingEnv henv;
        private readonly IConfiguration config;

        public OfflineTool()
        {
            if (Environment.GetEnvironmentVariable("MUTATION") == "CHD") mut = Mutation.CHD;
            else if (Environment.GetEnvironmentVariable("MUTATION") == "HDD") mut = Mutation.HDD;
            else throw new Exception("Environment variable MUTATION missing value invalid. Supported: CHD, HDD.");
            Console.WriteLine("Mutation: " + mut);

            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production") henv = HostingEnv.Production;
            else if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Staging") henv = HostingEnv.Staging;
            else henv = HostingEnv.Development;
            Console.WriteLine("Environment: " + henv);

            var builder = new ConfigurationBuilder().AddEnvironmentVariables();
            string appSettingsFile = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            string appSettingsDevenvFile = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.devenv.json");
            if (File.Exists(appSettingsFile)) builder.AddJsonFile(appSettingsFile, optional: true);
            if (File.Exists(appSettingsDevenvFile)) builder.AddJsonFile(appSettingsDevenvFile, optional: true);
                
            // Config specific to mutation and hosting environment
            string cfgFileName = null;
            if (henv == HostingEnv.Production && mut == Mutation.HDD) cfgFileName = "/etc/zdo/zdo-hdd-live/appsettings.json";
            if (henv == HostingEnv.Staging && mut == Mutation.HDD) cfgFileName = "/etc/zdo/zdo-hdd-stage/appsettings.json";
            if (henv == HostingEnv.Production && mut == Mutation.CHD) cfgFileName = "/etc/zdo/zdo-chd-live/appsettings.json";
            if (henv == HostingEnv.Staging && mut == Mutation.CHD) cfgFileName = "/etc/zdo/zdo-chd-stage/appsettings.json";
            if (henv != HostingEnv.Development) builder.AddJsonFile(cfgFileName, optional: false);
            config = builder.Build();
        }

        public void RecreateDB()
        {
            Startup.InitDB(config, null, false);
            DB.CreateTables();
        }

        public void ImportFreq(string freqPath)
        {
            Startup.InitDB(config, null, false);
            using (SqlDict.Freq freq = new SqlDict.Freq())
            using (FileStream fs = new FileStream(freqPath, FileMode.Open, FileAccess.Read))
            using (StreamReader sr = new StreamReader(fs))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    if (parts.Length != 2) continue;
                    freq.StoreFreq(parts[0], int.Parse(parts[1]));
                }
                freq.CommitRest();
            }
        }

        public void ImportDict(string dictPath, string workingFolder)
        {
            Startup.InitDB(config, null, false);
            SqlDict dict = new SqlDict(null);
            using (SqlDict.BulkBuilder imp = dict.GetBulkBuilder(workingFolder, 0, "Importing stuff.", false))
            using (FileStream fs = new FileStream(dictPath, FileMode.Open, FileAccess.Read))
            using (StreamReader sr = new StreamReader(fs))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line == "" || line.StartsWith("#")) continue;
                    imp.AddEntry(line);
                }
                imp.CommitRest();
            }
        }
    }
}
