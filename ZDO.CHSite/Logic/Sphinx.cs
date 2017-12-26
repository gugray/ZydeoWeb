using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;

namespace ZDO.CHSite.Logic
{
    public class SphinxResult
    {
        public List<int> SurfSegPositions = new List<int>();
        public List<KeyValuePair<int, string>> StemmedSegs = new List<KeyValuePair<int, string>>();
        public string StemmedQuery;
        public int TotalCount = 0;
        public float PerlInnerElapsed;
        public float PerlOuterElapsed;
    }

    public class Sphinx
    {
        private readonly ILogger logger;
        private readonly string perlBin;
        private readonly string sphinxScript;
        private readonly string corpusBinFileName;

        public Sphinx(ILoggerFactory lf, string perlBin, string sphinxScript, string corpusBinFileName)
        {
            if (lf != null) logger = lf.CreateLogger(GetType().FullName);
            else logger = new DummyLogger();

            this.perlBin = perlBin;
            this.sphinxScript = sphinxScript;
            this.corpusBinFileName = corpusBinFileName;
        }

        public string CorpusBinFileName
        {
            get { return corpusBinFileName;  }
        }

        public SphinxResult Query(string query, bool isZho, int ofs, int limit)
        {
            try
            {
                return doQuery(query, isZho, ofs, limit);
            }
            catch (Exception ex)
            {
                logger.LogError(new EventId(), ex, "Sphinx query failed.");
                throw;
            }
        }

        private SphinxResult doQuery(string query, bool isZho, int ofs, int limit)
        {
            DateTime dtStart = DateTime.Now;
            SphinxResult res = new SphinxResult();
            string lang = isZho ? "zh" : "hu";
            string currDir = Directory.GetCurrentDirectory();
            using (Process p = new Process())
            {
                p.StartInfo.FileName = perlBin;
                p.StartInfo.Arguments = sphinxScript + ' ' +
                    WebUtility.UrlEncode(query) + " " + lang + ' ' + ofs.ToString() + ' ' + limit.ToString();
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
                    string err = "<unknown>";
                    try { err = p.StandardError.ReadToEnd(); }
                    catch { }
                    throw new Exception("Sphinx query process exited with code " + p.ExitCode + ": " + err);
                }
            }
            DateTime dtEnd = DateTime.Now;
            res.PerlOuterElapsed = (float)(dtEnd.Subtract(dtStart).TotalMilliseconds / 1000);
            return res;
        }

    }
}
