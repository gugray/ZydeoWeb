using System;
using System.Collections.Generic;
using System.Diagnostics;
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
