using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Net;

namespace ZDO.ConcAlign
{
    public class Sphinx
    {
        public static SphinxResult Query(string query, bool isZho, int limit)
        {
            SphinxResult res = new SphinxResult();
            query = query.Replace("'", "");
            query = query.Replace("\"", "");
            res.ActualQuery = query;
            string lang = isZho ? "zh" : "hulo";
            string currDir = Directory.GetCurrentDirectory();
            using (Process p = new Process())
            {
                p.StartInfo.FileName = "C:/Strawberry/perl/bin/perl.exe";
                p.StartInfo.Arguments = "query.pl " + WebUtility.UrlEncode(query) + " " + lang + " 0 " + limit.ToString();
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.Start();
                p.WaitForExit();
                if (p.ExitCode != 0)
                {
                    string err = p.StandardError.ReadToEnd();
                    return null;
                }
                string line = p.StandardOutput.ReadLine();
                if (line == null) return res;
                res.TotalCount = int.Parse(line.Replace("COUNT: ", ""));
                res.SegPositionsZh = new List<int>();
                while ((line = p.StandardOutput.ReadLine()) != null)
                {
                    if (line == "") continue;
                    res.SegPositionsZh.Add(int.Parse(line) - 1);
                }
            }
            return res;
        }
    }
}
