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
            DateTime dtStart = DateTime.Now;
            SphinxResult res = new SphinxResult();
            string lang = isZho ? "zh" : "hu";
            string currDir = Directory.GetCurrentDirectory();
            using (Process p = new Process())
            {
                if (currDir.StartsWith("/"))
                   p.StartInfo.FileName = "/usr/bin/perl";
                else
                    p.StartInfo.FileName = "C:/Strawberry/perl/bin/perl.exe";
                p.StartInfo.Arguments = "query.pl " + WebUtility.UrlEncode(query) + " " + lang + " 0 " + limit.ToString();
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.Start();

                string line;
                while ((line = p.StandardOutput.ReadLine()) != null)
                {
                    if (line == "") continue;
                    if (line.StartsWith("STEMMED"))
                    {
                        res.StemmedQuery = line.Replace("STEMMED ", "");
                        continue;
                    }
                    if (line.StartsWith("COUNT"))
                    {
                        string[] parts = line.Split('\t');
                        res.TotalCount = int.Parse(parts[1]);
                        res.PerlInnerElapsed = float.Parse(parts[2]);
                        break;
                    }
                    if (res.StemmedQuery == null)
                        res.SurfSegPositions.Add(int.Parse(line) - 1);
                    else
                    {
                        string[] parts = line.Split('\t');
                        var kvp = new KeyValuePair<int, string>(int.Parse(parts[0]) - 1, parts[1]);
                        res.StemmedSegs.Add(kvp);
                    }
                }

                p.WaitForExit();
                if (p.ExitCode != 0)
                {
                    string err = p.StandardError.ReadToEnd();
                    return null;
                }
            }
            DateTime dtEnd = DateTime.Now;
            res.PerlOuterElapsed = (float)(dtEnd.Subtract(dtStart).TotalMilliseconds / 1000);
            return res;
        }
    }
}
