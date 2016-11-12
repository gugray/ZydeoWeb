using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace ZDO.Console.Logic
{
    public class Helpers
    {
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
