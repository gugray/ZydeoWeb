using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

namespace ZD.AlignTool
{
    public partial class Program
    {
        class HeapPrefNode
        {
            public Dictionary<char, HeapPrefNode> Nexts = new Dictionary<char, HeapPrefNode>();
            public bool Word;
        }

        struct PrefNode
        {
            public PrefNode[] Nexts;
            public char C;
            public bool Word;
        }

        class PrefNodeComparer : IComparer<PrefNode>
        {
            public int Compare(PrefNode x, PrefNode y)
            {
                return x.C.CompareTo(y.C);
            }
        }

        static PrefNode prefRoot;
        static PrefNodeComparer prefComp = new PrefNodeComparer();
        static Dictionary<string, string> wvaStemDict = new Dictionary<string, string>();
        static Dictionary<string, Dictionary<string, int>> wvaLoToForms = new Dictionary<string, Dictionary<string, int>>();
        static Dictionary<string, string> wvaHiToLo = new Dictionary<string, string>();

        static void wvaReadCedict()
        {
            Console.WriteLine("Building simplified ZH prefix tree");
            Dictionary<int, int> senseCountToEntryCount = new Dictionary<int, int>();
            HeapPrefNode hroot = new HeapPrefNode();
            HashSet<string> simps = new HashSet<string>();
            string line;
            using (var sr = ropen("cedict_ts.u8"))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.StartsWith("#") || line == "") continue;
                    Match m = reEntry.Match(line);
                    string simp = m.Groups[2].Value;
                    if (simps.Contains(simp)) continue;
                    simps.Add(simp);
                    HeapPrefNode n = hroot;
                    foreach (char c in simp)
                    {
                        if (!n.Nexts.ContainsKey(c)) n.Nexts[c] = new HeapPrefNode();
                        n = n.Nexts[c];
                    }
                    n.Word = true;
                    // How many senses?
                    int senseCount = -1;
                    foreach (char c in line) if (c == '/') ++senseCount;
                    if (senseCountToEntryCount.ContainsKey(senseCount)) ++senseCountToEntryCount[senseCount];
                    else senseCountToEntryCount[senseCount] = 1;
                }
            }
            prefRoot = wvaBuildTree(hroot, (char)0);
            List<int> scList = new List<int>();
            foreach (var x in senseCountToEntryCount) scList.Add(x.Key);
            scList.Sort((x, y) => senseCountToEntryCount[y].CompareTo(senseCountToEntryCount[x]));
            Console.WriteLine("Sense #\tEntry #");
            foreach (int sc in scList) Console.WriteLine(sc + "\t" + senseCountToEntryCount[sc]);
        }

        static PrefNode wvaBuildTree(HeapPrefNode hn, char c)
        {
            PrefNode pn = new PrefNode { C = c, Word = hn.Word };
            if (hn.Nexts.Count == 0) return pn;
            pn.Nexts = new PrefNode[hn.Nexts.Count];
            List<char> nexts = new List<char>();
            foreach (char next in hn.Nexts.Keys) nexts.Add(next);
            nexts.Sort();
            for (int i = 0; i != nexts.Count; ++i)
                pn.Nexts[i] = wvaBuildTree(hn.Nexts[nexts[i]], nexts[i]);
            return pn;
        }

        static void wvaBuildStemDict()
        {
            Console.WriteLine("Building HU stem dictionary");
            Stemmer s = new Stemmer();
            using (StreamReader sr = ropen("04-tmp-hu-vocab-stemmed.txt"))
            {
                string line;
                string surf = null;
                List<string> anas = new List<string>();
                List<string> stems = new List<string>();
                while ((line = sr.ReadLine()) != null)
                {
                    if (line != "")
                    {
                        string[] parts = line.Split('\t');
                        if (surf == null) surf = parts[0];
                        anas.Add(parts[1]);
                        continue;
                    }
                    stems.Clear();
                    // Create stem from every analysis
                    foreach (string ana in anas)
                    {
                        if (ana.EndsWith("?"))
                            break;
                        string norm = s.preproc(ana);
                        Stemmer.Stem st = s.process(norm);
                        // No a compund: "as is"
                        if (st.compoundDelims.Count == 0)
                        {
                            stems.Add(st.szStem);
                            continue;
                        }
                        // Compound: put in + delimiters NOT
                        List<string> parts = new List<string>();
                        int ix = 0;
                        for (int i = 0; i < st.compoundDelims.Count; ++i)
                        {
                            parts.Add(st.szStem.Substring(ix, st.compoundDelims[i] - ix));
                            ix = st.compoundDelims[i];
                        }
                        parts.Add(st.szStem.Substring(ix));
                        sb2.Clear();
                        for (int i = 0; i != parts.Count; ++i)
                        {
                            //if (i > 0) sb2.Append('+');
                            sb2.Append(parts[i]);
                        }
                        stems.Add(sb2.ToString());
                    }
                    // Find shortest stem (if any)
                    if (stems.Count > 0)
                    {
                        string shortestStem = stems[0];
                        for (int i = 1; i < stems.Count; ++i)
                        {
                            if (shortestStem.Length == 0 && stems[i].Length > 0)
                                shortestStem = stems[i];
                            else if (stems[i].Length > 0 && stems[i].Length < shortestStem.Length)
                                shortestStem = stems[i];
                        }
                        // Stems where we still want to keep surface form
                        if (Array.IndexOf(noStemSurfs, shortestStem) != -1)
                            shortestStem = surf;
                        // File stem in dictionary, if we have one
                        if (shortestStem.Length != 0)
                        {
                            wvaStemDict[surf] = shortestStem;
                        }
                    }
                    // Next!
                    surf = null;
                    anas.Clear();
                }
            }
        }

        static char[] sentFinal = { '-', '.', '?', '!' };

        static List<string> wvaGetTrimmedToks(string seg)
        {
            List<string> res = new List<string>();
            string[] parts = seg.Split(' ');
            bool sentStart = false;
            foreach (string surf in parts)
            {
                // After last sentence, feed empty token
                if (sentStart)
                {
                    res.Add("");
                    sentStart = false;
                }
                if (surf.Length == 0) continue;
                // Trailing punctuation that indicates a sentence: ?!-.
                if (Array.IndexOf(sentFinal, surf[surf.Length - 1]) != -1)
                    sentStart = true;
                // "Trim" punctuation from start and end
                int i = 0;
                while (i < surf.Length && char.IsPunctuation(surf[i])) ++i;
                if (i == surf.Length) continue;
                string s = i == 0 ? surf : surf.Substring(i);
                i = s.Length - 1;
                while (i >= 0 && char.IsPunctuation(s[i])) --i;
                if (i != s.Length - 1) s = s.Substring(0, i + 1);
                res.Add(s);
            }
            return res;
        }

        static void wvaCaseCount(string hu)
        {
            string[] toks = hu.Split(' ');
            for (int i = 0; i != toks.Length; ++i)
            {
                string tok = toks[i];
                if (tok == "") continue;
                // We don't count sentence-first tokens
                if (i == 0 || toks[i - 1] == "") continue;
                string lo = tok.ToLower();
                if (!wvaLoToForms.ContainsKey(lo)) wvaLoToForms[lo] = new Dictionary<string, int>();
                Dictionary<string, int> forms = wvaLoToForms[lo];
                if (forms.ContainsKey(tok)) ++forms[tok];
                else forms[tok] = 1;
            }
        }

        static string wvaTokZh(string zh)
        {
            StringBuilder sb = new StringBuilder();
            int start = 0;
            while (start < zh.Length)
            {
                int end = start;
                int pos = start;
                PrefNode pn = prefRoot;
                while (pos < zh.Length)
                {
                    if (pn.Nexts == null) break;
                    int ix = Array.BinarySearch(pn.Nexts, new PrefNode { C = zh[pos] }, prefComp);
                    if (ix < 0) break;
                    ++pos;
                    pn = pn.Nexts[ix];
                    if (pn.Word) end = pos;
                }
                if (start == end)
                {
                    char c = zh[start];
                    if (!char.IsWhiteSpace(c) && !char.IsPunctuation(c))
                    {
                        // No space: if both this, and previous, are a-zA-Z0-9
                        bool blockSpace = false;
                        bool isAlnum = (c >= '0' && c <= '9' || c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z');
                        if (sb.Length > 0 && isAlnum)
                        {
                            char d = sb[sb.Length - 1];
                            if (d >= '0' && d <= '9' || d >= 'a' && d <= 'z' || d >= 'A' && d <= 'Z')
                                blockSpace = true;
                        }
                        if (!blockSpace && sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                        if (!isAlnum) sb.Append('@');
                        sb.Append(zh[start]);
                    }
                    ++start;
                    continue;
                }
                string tok = zh.Substring(start, end - start).Trim();
                if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                sb.Append(tok);
                start = end;
            }
            return sb.ToString();
        }

        static string wvaTokHu(string hu)
        {
            StringBuilder sb = new StringBuilder();
            List<string> ttoks = wvaGetTrimmedToks(hu);
            for (int i = 0; i != ttoks.Count; ++i)
            {
                string tok = ttoks[i];
                if (tok == "")
                {
                    if (i == ttoks.Count - 1) continue;
                    if (i > 0 && ttoks[i - 1] == "") continue;
                    if (sb.Length == 0) continue;
                    sb.Append(' ');
                    continue;
                }
                string stem = tok;
                if (wvaStemDict.ContainsKey(tok))
                {
                    stem = wvaStemDict[tok];
                    if (char.IsUpper(tok[0]) && !char.IsUpper(stem[0]))
                    {
                        if (stem.Length > 0) stem = char.ToUpper(stem[0]) + stem.Substring(1);
                        else stem = char.ToUpper(stem[0]).ToString();
                    }
                }
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(stem);
            }
            return sb.ToString();
        }

        static void wvaTokenize()
        {
            Console.Write("Tokenizing");
            string line;
            int count = 0;
            using (var sr = ropen("03-zh-hu.txt"))
            using (var sw = wopen("40-zh-hu-tok.txt"))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    string zhTok = wvaTokZh(parts[0]);
                    string huTok = wvaTokHu(parts[1]);
                    wvaCaseCount(huTok);
                    sw.Write(zhTok);
                    sw.Write('\t');
                    sw.Write(huTok);
                    sw.Write('\n');
                    if (count % 100000 == 0) Console.Write("\rTokenizing (" + count + ")");
                    ++count;
                }
            }
            Console.WriteLine("\rTokenizing: done.     ");
        }

        static string wvaTrueToks(string[] toks)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i != toks.Length; ++i)
            {
                string tok = toks[i];
                if (tok == "") continue;
                char c = tok[0];
                string keptForm = null;
                // Not a sentence start: nothing to do, keep as is
                if (i != 0 && toks[i - 1] != "") keptForm = tok;
                // Sentence start: if upper-case, keep dominant form
                else if (!char.IsUpper(c)) keptForm = tok;
                else if (!wvaHiToLo.ContainsKey(tok)) keptForm = tok;
                else keptForm = wvaHiToLo[tok];
                // Append.
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(keptForm);
            }
            return sb.ToString();
        }

        static void wvaTrueCase()
        {
            Console.WriteLine("Truecasing");
            // Pick forms to be lower-cased at sentence start
            foreach (var x in wvaLoToForms)
            {
                string upperForm = null;
                int upperCount = 0;
                string lowerForm = null;
                int lowerCount = 0;
                foreach (var y in x.Value)
                {
                    bool isUpper = char.IsUpper(y.Key[0]);
                    if (isUpper && y.Value > upperCount) { upperForm = y.Key; upperCount = y.Value; }
                    if (!isUpper && y.Value > lowerCount) { lowerForm = y.Key; lowerCount = y.Value; }
                }
                if (lowerCount == 0) continue;
                if (upperForm == null)
                {
                    upperForm = char.ToUpper(x.Key[0]).ToString();
                    if (x.Key.Length > 1) upperForm += x.Key.Substring(1);
                }
                // We replace most frequent upper form with most frequent lower form, if diff is big enough
                if (lowerCount >= upperCount * 2)
                {
                    wvaHiToLo[upperForm] = lowerForm;
                }
            }

            string line;
            using (var sr = ropen("40-zh-hu-tok.txt"))
            using (var sw = wopen("41-zh-hu-tok-true.txt"))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    string[] huToks = parts[1].Split(' ');
                    string huTrue = wvaTrueToks(huToks);
                    sw.Write(parts[0].Replace("@", ""));
                    sw.Write('\t');
                    sw.Write(huTrue);
                    sw.Write('\n');
                }
            }
            Console.WriteLine("\rTokenizing: done.");
        }

        static void wvaCorpusStats()
        {
            string line;
            int lineCount = 0;
            int zhTotalTokCount = 0;
            int zhMaxTokCount = 0;
            int huTotalTokCount = 0;
            int huMaxTokCount = 0;
            // ZH
            int totalChars = 0;
            int untokChars = 0;
            Dictionary<string, int> zhFreqs = new Dictionary<string, int>();
            Dictionary<char, int> zhUntokFreqs = new Dictionary<char, int>();
            using (var sr = ropen("40-zh-hu-tok.txt"))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    ++lineCount;
                    string[] zhToks = line.Split('\t')[0].Split(' ');
                    zhTotalTokCount += zhToks.Length;
                    if (zhToks.Length > zhMaxTokCount) zhMaxTokCount = zhToks.Length;
                    foreach (string tok in zhToks)
                    {
                        if (tok[0] == '@')
                        {
                            ++untokChars;
                            ++totalChars;
                            char c = tok[1];
                            if (zhUntokFreqs.ContainsKey(c)) ++zhUntokFreqs[c];
                            else zhUntokFreqs[c] = 1;
                        }
                        else
                        {
                            totalChars += tok.Length;
                            if (zhFreqs.ContainsKey(tok)) ++zhFreqs[tok];
                            else zhFreqs[tok] = 1;
                        }
                    }
                }
            }
            Console.WriteLine("ZH chars: " + totalChars + "; untokenized: " + untokChars);
            List<string> zhList = new List<string>();
            foreach (var x in zhFreqs) zhList.Add(x.Key);
            zhList.Sort((x, y) => zhFreqs[y].CompareTo(zhFreqs[x]));
            List<char> untokList = new List<char>();
            foreach (var x in zhUntokFreqs) untokList.Add(x.Key);
            untokList.Sort((x, y) => zhUntokFreqs[y].CompareTo(zhUntokFreqs[x]));
            using (var sw = wopen("42-zh-wordfreqs.txt"))
            {
                for (int i = 0; i != zhList.Count; ++i)
                {
                    sw.WriteLine((i + 1) + "\t" + zhFreqs[zhList[i]] + "\t" + zhList[i]);
                }
            }
            using (var sw = wopen("42-zh-untokfreqs.txt"))
            {
                for (int i = 0; i != untokList.Count; ++i)
                {
                    sw.WriteLine((i + 1) + "\t" + zhUntokFreqs[untokList[i]] + "\t" + untokList[i]);
                }
            }
            // HU
            Dictionary<string, int> huFreqs = new Dictionary<string, int>();
            using (var sr = ropen("41-zh-hu-tok-true.txt"))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    string[] huToks = line.Split('\t')[1].Split(' ');
                    huTotalTokCount += huToks.Length;
                    if (huToks.Length > huMaxTokCount) huMaxTokCount = huToks.Length;
                    foreach (string tok in huToks)
                    {
                        if (huFreqs.ContainsKey(tok)) ++huFreqs[tok];
                        else huFreqs[tok] = 1;
                    }
                }
            }
            List<string> huList = new List<string>();
            foreach (var x in huFreqs) huList.Add(x.Key);
            huList.Sort((x, y) => huFreqs[y].CompareTo(huFreqs[x]));
            using (var sw = wopen("42-hu-wordfreqs.txt"))
            {
                for (int i = 0; i != huList.Count; ++i)
                {
                    sw.WriteLine((i + 1) + "\t" + huFreqs[huList[i]] + "\t" + huList[i]);
                }
            }
            double zhTokAvg = ((double)zhTotalTokCount) / lineCount;
            double huTokAvg = ((double)huTotalTokCount) / lineCount;
            Console.WriteLine("ZH tokens: avg: " + zhTokAvg.ToString("0.00") + " max: " + zhMaxTokCount);
            Console.WriteLine("HU tokens: avg: " + huTokAvg.ToString("0.00") + " max: " + huMaxTokCount);
        }

        static HashSet<string> wvaKeptHu = new HashSet<string>();
        static HashSet<string> wvaKeptZh = new HashSet<string>();
        static Dictionary<string, float[]> wvaZhVects = new Dictionary<string, float[]>();
        static Dictionary<string, float[]> wvaHuVects = new Dictionary<string, float[]>();

        static void wvaLoadFreqs(int huMinFreq, int zhMinFreq)
        {
            string line;
            using (var sr = ropen("42-hu-wordfreqs.txt"))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    int freq = int.Parse(parts[1]);
                    if (freq < huMinFreq) continue;
                    wvaKeptHu.Add(parts[2]);
                }
            }
            using (var sr = ropen("42-zh-wordfreqs.txt"))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    int freq = int.Parse(parts[1]);
                    if (freq < zhMinFreq) continue;
                    wvaKeptZh.Add(parts[2]);
                }
            }
        }

        static void wvaLoadVects(string vectFileName)
        {
            using (var sr = ropen(vectFileName))
            {
                string line = sr.ReadLine();
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split(' ');
                    string word = parts[0];
                    bool isZh = word[0] >= 0x4e00;
                    if (isZh && !wvaKeptZh.Contains(word)) continue;
                    if (!isZh && !wvaKeptHu.Contains(word)) continue;
                    float[] vect = new float[300];
                    for (int i = 1; i != parts.Length; ++i)
                        vect[i - 1] = float.Parse(parts[i]);
                    if (isZh) wvaZhVects[word] = vect;
                    else wvaHuVects[word] = vect;
                }
            }
        }

        private static float wvaCalcSim(float[] a, float[] b)
        {
            float prodSum = 0;
            float squareSumA = 0;
            float squareSumB = 0;
            for (int i = 0; i != a.Length; ++i)
            {
                prodSum += a[i] * b[i];
                squareSumA += a[i] * a[i];
                squareSumB += b[i] * b[i];
            }
            return prodSum / (float)(Math.Sqrt(squareSumA) * Math.Sqrt(squareSumB));
        }

        static void wvaInvRank(string fnOut, int simCount, 
            Dictionary<string, float[]> srcVects, Dictionary<string, float[]> trgVects)
        {
            Console.WriteLine("Calculating similarities.");
            // For every HU vector, find nearest N ZH vectors
            Dictionary<string, SrcTrgPair[]> trgToSrcs = new Dictionary<string, SrcTrgPair[]>();
            List<SrcTrgPair> nearests = new List<SrcTrgPair>();
            int trgCount = 0;
            foreach (var x in trgVects)
            {
                nearests.Clear();
                foreach (var y in srcVects)
                {
                    SrcTrgPair sp = new SrcTrgPair
                    {
                        Trg = x.Key,
                        Src = y.Key,
                        Sim = wvaCalcSim(x.Value, y.Value),
                    };
                    nearests.Add(sp);
                }
                nearests.Sort((a, b) => b.Sim.CompareTo(a.Sim));
                SrcTrgPair[] ranked = new SrcTrgPair[simCount];
                for (int i = 0; i != simCount; ++i) ranked[i] = nearests[i];
                trgToSrcs[x.Key] = ranked;
                if (trgCount % 100 == 0) Console.Write("\r" + trgCount + " / " + trgVects.Count);
                ++trgCount;
                //if (huCount == 200) break; // DBG
            }
            Console.WriteLine("Discovering best-ranked equivalents.");
            // Now, for every ZH, find that HU for it which it ranks best
            Dictionary<string, List<RankedSim>> srcToRanked = new Dictionary<string, List<RankedSim>>();
            foreach (var x in trgToSrcs)
            {
                for (int i = 0; i != x.Value.Length; ++i)
                {
                    SrcTrgPair sp = x.Value[i];
                    List<RankedSim> ranked;
                    if (!srcToRanked.ContainsKey(sp.Src)) { ranked = new List<RankedSim>(); srcToRanked[sp.Src] = ranked; }
                    else ranked = srcToRanked[sp.Src];
                    ranked.Add(new RankedSim { Pair = sp, Rank = i });
                }
            }
            // Results: for each ZH, top-ranked HU words
            using (var sw = wopen(fnOut))
            {
                foreach (var x in srcToRanked)
                {
                    // Sort each ranked list
                    x.Value.Sort((a, b) => a.Rank.CompareTo(b.Rank));
                    // Write result
                    sw.Write(x.Key);
                    for (int i = 0; i < simCount && i < x.Value.Count; ++i)
                    {
                        RankedSim rs = x.Value[i];
                        sw.Write('\t');
                        sw.Write(rs.Rank.ToString() + " " + rs.Pair.Sim.ToString("0.00") + " " + rs.Pair.Trg);
                    }
                    sw.WriteLine();
                }
            }
        }

        private class SrcTrgPair
        {
            public string Src;
            public string Trg;
            public float Sim;
        }

        class RankedSim
        {
            public SrcTrgPair Pair;
            public int Rank;
        }

        public static void Main(string[] args)
        {
            //wvaReadCedict();
            //wvaBuildStemDict();
            //wvaTokenize();
            //wvaTrueCase();

            //wvaCorpusStats();

            wvaLoadFreqs(5, 5);
            wvaLoadVects("43-wv-model.txt");
            wvaInvRank("44-wv-zh-hu.txt", 10, wvaZhVects, wvaHuVects);
            wvaInvRank("44-wv-hu-zh.txt", 10, wvaHuVects, wvaZhVects);

            Console.ReadLine();
        }
    }
}
