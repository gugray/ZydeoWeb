using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using ZD.Common;
using ZD.LangUtils;

namespace ZD.Tool
{
    public class WrkExamine : IWorker
    {
        private FileStream fsDict = null;
        private StreamReader srDict = null;
        private FileStream fsDiag = null;
        private StreamWriter swDiag = null;
        private FileStream fsTrip = null;
        private StreamWriter swTrip = null;
        private int lineNum = 0;
        private CedictParser parser = new CedictParser();

        public void Init()
        {
            fsDict = new FileStream("handedict.u8", FileMode.Open, FileAccess.Read);
            srDict = new StreamReader(fsDict);
            fsDiag = new FileStream("hdd-diag.txt", FileMode.Create, FileAccess.ReadWrite);
            swDiag = new StreamWriter(fsDiag);
            fsTrip = new FileStream("hdd-trip.txt", FileMode.Create, FileAccess.ReadWrite);
            swTrip = new StreamWriter(fsTrip);
        }

        public void Dispose()
        {
            if (swTrip != null) swTrip.Dispose();
            if (fsTrip != null) fsTrip.Dispose();
            if (swDiag != null) swDiag.Dispose();
            if (fsDiag != null) fsDiag.Dispose();
            if (srDict != null) srDict.Dispose();
            if (fsDict != null) fsDict.Dispose();
        }

        public void Work()
        {
            string line;
            while ((line = srDict.ReadLine()) != null)
            {
                ++lineNum;
                if (line.StartsWith("#")) continue;
                CedictEntry entry = parser.ParseEntry(line, lineNum, swDiag);
                if (entry != null)
                {
                    string trippedLine = CedictWriter.Write(entry);
                    if (trippedLine != line)
                    {
                        swTrip.WriteLine(line);
                        swTrip.WriteLine(trippedLine);
                    }
                    countTags(line);
                }
            }
            List<TC> tlst = new List<TC>();
            foreach (var x in tags) tlst.Add(new TC { Tag = x.Key, Count = x.Value });
            tlst.Sort((x, y) => y.Count.CompareTo(x.Count));
            using (FileStream fsTags = new FileStream("hdd-tags.txt", FileMode.Create, FileAccess.ReadWrite))
            using (StreamWriter sw = new StreamWriter(fsTags))
            {
                foreach (var x in tlst) sw.WriteLine(x.Count + "\t" + x.Tag);
            }
        }

        private class TC
        {
            public string Tag;
            public int Count;
        }

        private Dictionary<string, int> tags = new Dictionary<string, int>();
        private Regex reTag1 = new Regex(@"\(([^\)]+)\)");
        private Regex reTag2 = new Regex(@"<([^>]+)>");

        private void incTag(string tag)
        {
            if (!tags.ContainsKey(tag)) tags[tag] = 1;
            else ++tags[tag];
        }

        private void countTags(string line)
        {
            line = line.Replace("&gt", ">");
            line = line.Replace("(u.E.)", "");
            var mc = reTag1.Matches(line);
            foreach (Match m in mc)
            {
                string[] parts = m.Groups[1].Value.Split(',');
                for (int i = 0; i != parts.Length; ++i) parts[i] = parts[i].Trim();
                foreach (string t in parts) incTag("(" + t + ")");
            }
            mc = reTag2.Matches(line);
            foreach (Match m in mc)
            {
                string[] parts = m.Groups[1].Value.Split(',');
                for (int i = 0; i != parts.Length; ++i) parts[i] = parts[i].Trim();
                foreach (string t in parts) incTag("<" + t + ">");
            }
        }

        public void Finish()
        {
        }

    }
}
