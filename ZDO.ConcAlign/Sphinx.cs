using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;

namespace ZDO.ConcAlign
{
    public class Sphinx
    {
        public static List<SphinxResult> Query(string query, int limit)
        {
            List<SphinxResult> res = new List<SphinxResult>();
            query = query.Replace("'", "");
            query = query.Replace("\"", "");
            string xquery = "";
            foreach (char c in query)
            {
                if (query.Length > 0) xquery += "|";
                xquery += c;
            }
            string stdout;
            using (Process p = new Process())
            {
                p.StartInfo.FileName = "D:/MySQL/bin/mysql";
                p.StartInfo.Arguments = "-P9306";
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.Start();
                p.StandardInput.Write("select zh, hu, zhtokmap, hutokstemmap, alignstem from zhhu where match('\"" + query + "\"') limit " + limit.ToString() + ";");
                p.StandardInput.BaseStream.Dispose();
                stdout = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
            }
            if (stdout.IndexOf("\r\n") >= 0) stdout = stdout.Replace("\r", "");
            string[] lines = stdout.Split('\n');
            res.Capacity = lines.Length - 1;
            for (int i = 1; i < lines.Length; ++i)
            {
                string[] parts = lines[i].Split('\t');
                if (parts.Length < 5) continue;
                SphinxResult sr = new SphinxResult
                {
                    Zh = parts[0],
                    Hu = parts[1],
                    ZhTokMap = parts[2],
                    HuTokMap = parts[3],
                    Align = parts[4],
                };
                res.Add(sr);
            }
            return res;
        }
    }
}
