using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;

namespace ZDO.Console.Logic
{
    public class Helpers
    {
        private class MyALC : AssemblyLoadContext
        {
            protected override Assembly Load(AssemblyName assemblyName) { return null; }
        }

        public static string ExecWorkingDir = null;

        public static bool IsSrvRunning(string pidFileName)
        {
            try
            {
                string pidStr;
                using (FileStream fs = new FileStream(pidFileName, FileMode.Open, FileAccess.Read))
                using (StreamReader sr = new StreamReader(fs))
                {
                    pidStr = sr.ReadToEnd();
                }
                using (Process p = Process.GetProcessById(int.Parse(pidStr)))
                {
                    return p != null;
                }
            }
            catch { return false; }
        }

        private static MySqlConnection getDbConn(IConfigurationRoot config)
        {
            // Build connection string. Comes from Private.config
            MySqlConnectionStringBuilder csb = new MySqlConnectionStringBuilder();
            csb.Server = config["dbServer"];
            csb.Port = uint.Parse(config["dbPort"]);
            csb.Database = config["dbDatabase"];
            csb.UserID = config["dbUserID"];
            csb.Password = config["dbPass"];
            csb.Pooling = true;
            csb.IgnorePrepare = false;
            csb.CharacterSet = "utf8";
            csb.SslMode = MySqlSslMode.None; // SSL currently not supported in .NET Core library
            string connectionString = csb.GetConnectionString(true);
            MySqlConnection conn = new MySqlConnection(connectionString);
            try { conn.Open(); }
            catch { conn.Dispose(); throw; }
            return conn;
        }

        public static string GetDbModel(IConfigurationRoot config)
        {
            MySqlConnection conn = null;
            MySqlCommand cmd = null;
            try
            {
                conn = getDbConn(config);
                cmd = new MySqlCommand("SELECT value FROM sys_params WHERE xkey='db_model';", conn);
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read()) return rdr.GetString(0);
                }
                return "n/a";
            }
            catch { return "n/a"; }
            finally
            {
                if (cmd != null) cmd.Dispose();
                if (conn != null) conn.Dispose();
            }
        }

        public static IConfigurationRoot GetAppConfig(string etcRoot)
        {
            try
            {
                string cfgFileName = Path.Combine(etcRoot, "appsettings.json");
                var builder = new ConfigurationBuilder().AddJsonFile(cfgFileName, optional: true);
                return builder.Build();
            }
            catch { return null; }
        }

        public static string GetAppVer(string appRoot)
        {
            try
            {
                var alc = new MyALC();
                Assembly a = alc.LoadFromAssemblyPath(Path.Combine(appRoot, "app/ZDO.CHSite.dll"));
                string fn = a.FullName;
                int ix1 = fn.IndexOf("Version=") + "Version=".Length;
                int ix2 = fn.IndexOf('.', ix1);
                int ix3 = fn.IndexOf('.', ix2 + 1);
                string strMajor = fn.Substring(ix1, ix2 - ix1);
                string strMinor = fn.Substring(ix2 + 1, ix3 - ix2 - 1);
                return strMajor + "." + strMinor;
            }
            catch { return "n/a"; }
            //catch (Exception ex) { return ex.ToString(); }
        }

        public static string GetSizeString(int sz)
        {
            int order = 0;
            if (sz >= 1000000) order = 1000000;
            else if (sz >= 1000) order = 1000;
            if (order == 0)
            {
                return sz + " byte";
            }
            int meg = sz / order;
            int rem = sz - meg * order;
            int frac = rem / (order / 10);
            string res = meg + "." + frac;
            if (order == 1000) return res + "KB";
            else return res + "MB";

        }

        public static string GetDTString(DateTime dt)
        {
            string res = dt.Year + "-" + dt.Month.ToString("00") + "-" + dt.Day.ToString("00") + "!";
            res += dt.Hour.ToString("00") + ":" + dt.Minute.ToString("00") + "." + dt.Second.ToString("00");
            return res;
        }

        public static readonly string BupRegex = @"db\-dump\-(\d+)\-(\d+)\-(\d+)T(\d+)\-(\d+)\-(\d+)Z\.sql\.gz";

        public static void FindLatestBackup(string folder, out string latestFullName, out DateTime latestDT, out int latestSize)
        {
            // Enumerate files in instance's exports folder
            Regex reDump = new Regex(BupRegex);
            DirectoryInfo di = new DirectoryInfo(folder);
            var fis = di.EnumerateFiles();
            latestFullName = null;
            latestDT = DateTime.MinValue;
            latestSize = 0;
            foreach (var fi in fis)
            {
                Match m = reDump.Match(fi.Name);
                if (!m.Success) continue;
                DateTime dt = new DateTime(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value),
                    int.Parse(m.Groups[4].Value), int.Parse(m.Groups[5].Value), int.Parse(m.Groups[6].Value));
                dt = dt.ToLocalTime();
                if (dt <= latestDT) continue;
                latestDT = dt;
                latestFullName = fi.FullName;
                latestSize = (int)fi.Length;
            }
        }

        public static string Exec(string cmd, string args, out string stdout, out string stderr, Dictionary<string, string> env = null)
        {
            try
            {
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = cmd;
                    p.StartInfo.Arguments = args;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.RedirectStandardError = true;
                    if (ExecWorkingDir != null) p.StartInfo.WorkingDirectory = ExecWorkingDir;
                    else p.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
                    if (env != null) foreach (var x in env) p.StartInfo.Environment[x.Key] = x.Value;
                    p.Start();
                    p.WaitForExit();
                    stdout = p.StandardOutput.ReadToEnd();
                    stderr = p.StandardError.ReadToEnd();
                    return p.ExitCode != 0 ? "Return code: " + p.ExitCode.ToString() : null;
                }
            }
            catch (Exception ex)
            {
                stderr = stdout = "";
                return "Failed to execute " + cmd + " " + args + "\n" + ex.ToString();
            }
        }
    }
}
