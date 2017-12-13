using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;

using ZD.Common;
using ZD.LangUtils;
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

        /// <summary>
        /// Import new entries from file as a single bulk change.
        /// </summary>
        public void BulkAdd(string dictPath, string workingFolder)
        {
            CedictParser parser = new CedictParser();
            Startup.InitDB(config, null, false);
            SqlDict dict = new SqlDict(null, mut);
            int lineNum = 0;

            DateTime dt = DateTime.Now;
            string fnLog = "importlog-" + dt.Year + "-" + dt.Month.ToString("00") + "-" + dt.Day.ToString("00") + "!" + dt.Hour.ToString("00") + "-" + dt.Minute.ToString("00") + "-" + dt.Second.ToString("00") + ".txt";
            fnLog = Path.Combine(workingFolder, fnLog);
            using (FileStream fs = new FileStream(dictPath, FileMode.Open, FileAccess.Read))
            using (StreamReader sr = new StreamReader(fs))
            using (FileStream fsLog = new FileStream(fnLog, FileMode.Create, FileAccess.ReadWrite))
            using (StreamWriter swLog = new StreamWriter(fsLog))
            {
                // First two lines are commented and have metainfo
                // First line: user name
                // Second line: bulk change's comment
                string user = sr.ReadLine().Substring(1).Trim();
                string note = sr.ReadLine().Substring(1).Trim();
                lineNum = 2;
                using (SqlDict.ImportBuilder builder = dict.GetBulkBuilder(user, note))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        ++lineNum;
                        if (line == "" || line.StartsWith("#")) continue;
                        CedictEntry entry = parser.ParseEntry(line, lineNum, swLog);
                        if (entry == null)
                        {
                            swLog.WriteLine(line);
                            continue;
                        }
                        entry.Status = EntryStatus.Approved;
                        bool ok = builder.AddNewEntry(entry);
                        if (!ok)
                        {
                            swLog.WriteLine("Line " + lineNum + ": Entry rejected by importer.");
                            swLog.WriteLine(line);
                            continue;
                        }
                    }
                    builder.CommitRest();
                }
            }
        }

        /// <summary>
        /// Import dictionary data including version history.
        /// Only used to initialize from scratch.
        /// </summary>
        public void ImportDict(string dictPath, string workingFolder)
        {
            Startup.InitDB(config, null, false);
            SqlDict dict = new SqlDict(null, mut);
            List<EntryVersion> vers = new List<EntryVersion>();

            using (FileStream fs = new FileStream(dictPath, FileMode.Open, FileAccess.Read))
            using (StreamReader sr = new StreamReader(fs))
            {
                EntryBlockParser ebp = new EntryBlockParser(sr);
                // Two passes. In the first we collect user names and bulk changes.
                var users = new HashSet<string>();
                var bulks = new Dictionary<int, SqlDict.ImportBuilder.BulkChangeInfo>();
                while (true)
                {
                    vers.Clear();
                    int id = ebp.ReadBlock(vers);
                    if (id == -1) break;
                    for (int i = 0; i != vers.Count; ++i)
                    {
                        var ver = vers[i];
                        users.Add(ver.User);
                        // First change referencing this bulk
                        if (ver.BulkRef != -1 && !bulks.ContainsKey(ver.BulkRef))
                        {
                            SqlDict.ImportBuilder.BulkChangeInfo bci = new SqlDict.ImportBuilder.BulkChangeInfo
                            {
                                Timestamp = ver.Timestamp,
                                UserName = ver.User,
                                Comment = ver.Comment,
                                NewEntries = i == 0 ? 1 : 0,
                                ChangedEntries = i != 0 ? 1 : 0,
                            };
                            bulks[ver.BulkRef] = bci;
                        }
                        // Bulk, and seen before
                        else if (ver.BulkRef != -1)
                        {
                            if (i == 0) ++bulks[ver.BulkRef].NewEntries;
                            else ++bulks[ver.BulkRef].ChangedEntries;
                        }
                    }
                }
                // Enrich known built-in user names with "about"
                HashSet<string> richUsers = new HashSet<string>();
                foreach (string x in users)
                {
                    if (x == "HanDeDict") richUsers.Add(x + "\t" + "Platzhalter für das ursprüngliche HanDeDict-Team");
                    if (x == "zydeo-robot") richUsers.Add(x + "\t" + "Platzhalter für automatische Datenverarbeitung");
                    else richUsers.Add(x + "\t");
                }
                // Second pass. Actual import.
                fs.Position = 0;
                using (SqlDict.ImportBuilder builder = dict.GetBulkBuilder(richUsers, bulks))
                {
                    while (true)
                    {
                        vers.Clear();
                        int id = ebp.ReadBlock(vers);
                        if (id == -1) break;
                        builder.AddEntry(id, vers);
                    }
                    builder.CommitRest();
                }
            }
        }
    }
}

