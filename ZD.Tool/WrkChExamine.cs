using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using ZD.Common;
using ZD.LangUtils;

namespace ZD.Tool
{
    public class WrkChExamine : IWorker
    {
        private int lineNum = 0;
        private CedictParser parser = new CedictParser();

        public void Init()
        {
        }

        public void Dispose()
        {
        }

        private int entryCount = 0;
        private int senseCount = 0;

        public void Work()
        {
            string line;
            using (var fsDict = new FileStream("chdict.u8", FileMode.Open, FileAccess.Read))
            using (var srDict = new StreamReader(fsDict))
            using (var fsDiag = new FileStream("chd-diag.txt", FileMode.Create, FileAccess.ReadWrite))
            using (var swDiag = new StreamWriter(fsDiag))
            using (var fsTrip = new FileStream("chd-trip.txt", FileMode.Create, FileAccess.ReadWrite))
            using (var swTrip = new StreamWriter(fsTrip))
            {
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
                        fileHead(entry);
                        countTags(entry);
                        checkCommas(entry, lineNum, swDiag);
                        countPrefixes(entry, swDiag);
                        ++entryCount;
                        senseCount += entry.SenseCount;
                    }
                }
                writeHeadIssues(swDiag);
                writePrefixes();
                List<TC> tlst = new List<TC>();
                foreach (var x in tags) tlst.Add(new TC { Tag = x.Key, Count = x.Value });
                tlst.Sort((x, y) => y.Count.CompareTo(x.Count));
                using (FileStream fsTags = new FileStream("chd-tags.txt", FileMode.Create, FileAccess.ReadWrite))
                using (StreamWriter sw = new StreamWriter(fsTags))
                {
                    sw.WriteLine("ZH entries: " + entryCount);
                    sw.WriteLine("HU senses: " + senseCount);
                    sw.WriteLine();
                    foreach (var x in tlst) sw.WriteLine(x.Count + "\t" + x.Tag);
                }
            }
        }

        private Dictionary<string, int> prefCounts = new Dictionary<string, int>();

        private void writePrefixes()
        {
            List<string> prefs = new List<string>();
            foreach (var x in prefCounts) prefs.Add(x.Key);
            prefs.Sort((x, y) => prefCounts[y].CompareTo(prefCounts[x]));
            using (FileStream fs = new FileStream("chd-prefixes.txt", FileMode.Create, FileAccess.ReadWrite))
            using (StreamWriter sw = new StreamWriter(fs))
            {
                foreach (var pref in prefs)
                {
                    sw.WriteLine(prefCounts[pref] + "\t" + pref);
                }
            }
        }

        private void countPrefixes(CedictEntry entry, StreamWriter swDiag)
        {
            List<string> words = new List<string>();
            StringBuilder sb = new StringBuilder();
            foreach (var sense in entry.Senses)
            {
                if (sense.Equiv == "" || sense.Equiv.StartsWith("(")) continue;
                if (sense.Equiv.StartsWith("SZ:")) continue;
                sb.Clear();
                foreach (char c in sense.Equiv)
                {
                    if (char.IsWhiteSpace(c) || char.IsPunctuation(c))
                    {
                        if (sb.Length > 0) { words.Add(sb.ToString()); sb.Clear(); }
                    }
                    else sb.Append(c);
                }
                if (sb.Length > 0) words.Add(sb.ToString());
            }
            foreach (string word in words)
            {
                string wnopipe = word.Replace("|", "");
                if (prefCounts.ContainsKey(wnopipe)) ++prefCounts[wnopipe];
                else prefCounts[wnopipe] = 1;
                int pipeCount = word.Count(c => c == '|');
                if (pipeCount == 1)
                {
                    string pref = word.Substring(0, word.IndexOf('|'));
                    string post = word.Substring(word.IndexOf("|") + 1);
                    if (prefCounts.ContainsKey(pref)) ++prefCounts[pref];
                    else prefCounts[pref] = 1;
                    if (prefCounts.ContainsKey(post)) ++prefCounts[post];
                    else prefCounts[post] = 1;
                }
                else if (pipeCount > 1)
                {
                    string msg = "Multiple pipes in word: " + entry.ChTrad + " " + entry.ChSimpl + " " + word;
                    swDiag.WriteLine(msg);
                }
            }
        }

        private Dictionary<string, List<string>> heads = new Dictionary<string, List<string>>();

        private void fileHead(CedictEntry entry)
        {
            string headStr = entry.ChTrad + " " + entry.ChSimpl + " [";
            bool first = true;
            string py = "";
            foreach (var ps in entry.Pinyin)
            {
                if (!first) py += " ";
                first = false;
                py += ps.GetDisplayString(false);
            }
            headStr += py + "]";
            string headLo = headStr.ToLowerInvariant();
            if (!heads.ContainsKey(headLo))
            {
                heads[headLo] = new List<string>();
                heads[headLo].Add(headStr);
            }
            else heads[headLo].Add(headStr);
        }

        private void writeHeadIssues(StreamWriter swDiag)
        {
            foreach (var x in heads)
            {
                if (x.Value.Count == 1) continue;
                string msg = "Duplicate headword:";
                foreach (string hw in x.Value) msg += " " + hw;
                swDiag.WriteLine(msg);
            }
        }

        private void checkCommas(CedictEntry entry, int lineNum, StreamWriter swDiag)
        {
            foreach (var sense in entry.Senses)
            {
                if (sense.Equiv.StartsWith("SZ:")) continue;
                if (sense.Equiv.Contains(","))
                {
                    string msg = "Line {0}: Warning: Comma in sense: {1}";
                    msg = string.Format(msg, lineNum, entry.ChTrad + " " + entry.ChSimpl + " " + sense.Equiv);
                    swDiag.WriteLine(msg);
                }
            }
        }

        private class TC
        {
            public string Tag;
            public int Count;
        }

        private Dictionary<string, int> tags = new Dictionary<string, int>();

        private void incTag(string tag)
        {
            if (!tags.ContainsKey(tag)) tags[tag] = 1;
            else ++tags[tag];
        }

        private void countTags(CedictEntry entry)
        {
            foreach (var sense in entry.Senses)
            {
                string tag = sense.Domain.Trim();
                if (tag == "") continue;
                if (sense.Equiv == "" && !tag.StartsWith("(számlálószó")) continue;
                tag = tag.TrimStart('(');
                tag = tag.TrimEnd(')');
                if (tag.IndexOf(':') > 0)
                    tag = tag.Substring(0, tag.IndexOf(':'));
                List<string> tags = new List<string>();
                if (!tag.Contains(",")) tags.Add(tag);
                else
                {
                    string[] parts = tag.Split(',');
                    foreach (string x in parts) if (x.Trim() != "") tags.Add(x.Trim());
                }
                foreach (string x in tags) incTag(x);
            }
        }

        public void Finish()
        {
        }

    }
}
