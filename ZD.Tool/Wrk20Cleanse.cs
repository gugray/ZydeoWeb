using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using ZD.Common;
using ZD.LangUtils;

namespace ZD.Tool
{
    public class Wrk20Cleanse : IWorker
    {
        private class RawEntry
        {
            public string LnId = null;
            public string LnMetaVer1 = null;
            public string LnVer1 = null;
        }

        public void Work()
        {
            using (FileStream fsIn = new FileStream("x-10-handedict.txt", FileMode.Open, FileAccess.Read))
            using (StreamReader sr = new StreamReader(fsIn))
            using (FileStream fsOut = new FileStream("x-20-handedict.txt", FileMode.Create, FileAccess.ReadWrite))
            using (StreamWriter sw = new StreamWriter(fsOut))
            {
                RawEntry re = null;
                while ((re = getNext(sr)) != null)
                {
                    bool isVerified = re.LnMetaVer1.Contains("Stat-Verif");
                    string cleansed = cleanse(re.LnVer1);
                    // Write
                    sw.WriteLine(re.LnId);
                    sw.WriteLine(re.LnMetaVer1);
                    // No change
                    if (cleansed == re.LnVer1) sw.WriteLine(re.LnVer1);
                    // Add new version
                    else
                    {
                        sw.WriteLine("# " + re.LnVer1);
                        sw.WriteLine("# Ver-2 2016-10-23T15:32:07Z gabor " + (isVerified ? "Stat-Verif" : "Stat-New") + " 002>Datenreinigung");
                        sw.WriteLine(cleansed);
                    }
                    // Empty line
                    sw.WriteLine();
                }
            }
        }

        private Regex reLtGt = new Regex(@"<([^>\/]+)>");
        private Regex reAngled = new Regex(@"\[([^\]\/]+)\]");
        private Regex reCommaSpace = new Regex(@",(\p{L})");
        private Regex reParenSpace1 = new Regex(@"\( *([^\)]+)\)");
        private Regex reParenSpace2 = new Regex(@" +\)");

        private string cleanse(string line)
        {
            // Curly quotes, non-breakding spaces
            line = line.Replace(' ', ' '); // NBSP
            line = line.Replace('\t', ' '); // TAB
            line = line.Replace('“', '"'); // Curly quote
            line = line.Replace('”', '"'); // Curly quote
            // Remove "(u.E.)" from entry itself. We out this info into the Status meta field.
            line = line.Replace("(u.E.)", "");
            // Fix &gt in place of >
            line = line.Replace("&gt", ">");
            // <something> -> (something)
            line = reLtGt.Replace(line, "($1)");

            // Angle [brackets] inside body
            int spos = line.IndexOf('/');
            string head = line.Substring(0, spos);
            string body = line.Substring(spos);
            body = reAngled.Replace(body, "($1)");
            line = head + body;

            // No space after comma
            line = reCommaSpace.Replace(line, ", $1");
            // Multiple spaces
            while (true)
            {
                string b = line.Replace("  ", " ");
                if (b == line) break;
                line = b;
            }
            // Spaces inside ( parentheses )
            line = reParenSpace1.Replace(line, "($1)");
            line = reParenSpace2.Replace(line, ")");
            // Trailing / leading spaces in senses
            spos = line.IndexOf('/');
            head = line.Substring(0, spos);
            body = line.Substring(spos);
            body = body.Replace("/ ", "/");
            body = body.Replace(" /", "/");
            line = head + body;
            // Phew
            return line;
        }

        private RawEntry getNext(StreamReader sr)
        {
            RawEntry res = null;
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                if (line == "") continue;
                if (line.StartsWith("# ID"))
                {
                    res = new RawEntry { LnId = line };
                    continue;
                }
                if (line.StartsWith("# Ver-"))
                {
                    res.LnMetaVer1 = line;
                    continue;
                }
                res.LnVer1 = line;
                return res;
            }
            return null;
        }

        public void Init()
        { }

        public void Dispose()
        { }

        public void Finish()
        { }

    }
}
