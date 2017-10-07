using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

using ZD.LangUtils;

namespace ZD.AlignTool
{
    public partial class Program
    {
        static StreamReader ropen(string fileName)
        {
            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            return new StreamReader(fs);
        }

        static StreamWriter wopen(string fileName)
        {
            FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite);
            StreamWriter sw = new StreamWriter(fs);
            sw.NewLine = "\n";
            return sw;
        }

        static bool isBarfZh(char c)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            return (cat == System.Globalization.UnicodeCategory.PrivateUse ||
                cat == System.Globalization.UnicodeCategory.Surrogate);
        }

        static bool hasBarfZh(string str)
        {
            foreach (char c in str)
                if (isBarfZh(c)) return true;
            return false;
        }

        static Regex reLowerAlfa = new Regex("[a-z]");

        static void filterA()
        {
            int inCount = 0;
            int keptCount = 0;
            char[] huBadChars = new char[] { '{', '[', '}', ']', '@', '\\' };
            char[] huWrongCode = new char[] { 'õ', 'û', 'Õ', 'Û' };
            using (var srZh = ropen("00-osub.zh.txt"))
            using (var srHu = ropen("00-osub.hu.txt"))
            using (var sw = wopen("01-zh-hu.txt"))
            {
                while (true)
                {
                    string lnZh = srZh.ReadLine();
                    string lnHu = srHu.ReadLine();
                    if (lnZh == null && lnHu != null || lnHu == null && lnZh != null)
                        throw new Exception("Line count mismatch.");
                    if (lnZh == null) break;
                    ++inCount;
                    if (reLowerAlfa.IsMatch(lnZh)) continue;
                    if (hasBarfZh(lnZh)) continue;
                    if (lnHu.IndexOfAny(huBadChars) >= 0) continue;
                    if (lnHu.IndexOfAny(huWrongCode) >= 0)
                    {
                        lnHu = lnHu.Replace('õ', 'ő');
                        lnHu = lnHu.Replace('û', 'ű');
                        lnHu = lnHu.Replace('Õ', 'Ő');
                        lnHu = lnHu.Replace('Û', 'Ű');
                    }
                    ++keptCount;
                    sw.Write(lnZh);
                    sw.Write('\t');
                    sw.WriteLine(lnHu);
                }
            }
            Console.WriteLine("Input lines: " + inCount);
            Console.WriteLine("Kept lines: " + keptCount);
        }

        static void getTrad()
        {
            int tradCount = 0;
            int noHanziCount = 0;
            LangRepo langRepo = new LangRepo("unihanzi.bin");
            using (var sr = ropen("01-zh-hu.txt"))
            using (var swToSimp = wopen("02-tmp-tosimp.txt"))
            using (var swMain = wopen("02-tmp-zh-hu.txt"))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    UniHanziInfo[] uhis = langRepo.GetUnihanInfo(parts[0]);
                    bool hasTrad = false;
                    bool hasHanzi = false;
                    foreach (var uhi in uhis)
                    {
                        if (uhi != null) hasHanzi = true;
                        if (uhi != null && !uhi.CanBeSimp) { hasTrad = true;  break; }
                    }
                    if (!hasHanzi) continue;
                    if (hasTrad)
                    {
                        ++tradCount;
                        swToSimp.WriteLine(parts[0]);
                        parts[0] = "@TOSIMP@";
                    }
                    swMain.Write(parts[0]);
                    swMain.Write('\t');
                    swMain.WriteLine(parts[1]);
                }
            }
            Console.WriteLine("No Hanzi in source (dropped): " + noHanziCount);
            Console.WriteLine("Segments to simplify: " + tradCount);
        }

        static void fixTrad()
        {
            int barfCount = 0;
            Dictionary<char, int> freqs = new Dictionary<char, int>();
            using (var srMain = ropen("02-tmp-zh-hu.txt"))
            using (var srSimplified = ropen("02-tmp-simplified.txt"))
            using (var sw = wopen("02-zh-hu.txt"))
            using (var swZhFreq = wopen("02-zh-freq.txt"))
            {
                string line;
                while ((line = srMain.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    if (parts[0] == "@TOSIMP@")
                    {
                        string simp = srSimplified.ReadLine();
                        parts[0] = simp;
                    }

                    // We're getting bards back from simplification too
                    // At this point, just ignore
                    if (hasBarfZh(parts[0])) { ++barfCount; continue; }

                    foreach (char c in parts[0])
                    {
                        if (!freqs.ContainsKey(c)) freqs[c] = 1;
                        else ++freqs[c];
                    }

                    sw.Write(parts[0]);
                    sw.Write('\t');
                    sw.WriteLine(parts[1]);
                }
                List<char> chars = new List<char>();
                foreach (var x in freqs) chars.Add(x.Key);
                chars.Sort((x, y) => freqs[y].CompareTo(freqs[x]));
                foreach (char c in chars)
                {
                    swZhFreq.Write(freqs[c].ToString());
                    swZhFreq.Write('\t');
                    swZhFreq.Write(c);
                    swZhFreq.Write('\n');
                }
            }
            Console.WriteLine("Barf lines from simplified: " + barfCount);
        }

        static void histogram(string fn, string outfn)
        {
            Histogram hist = new Histogram();
            using (StreamReader sr = ropen(fn))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    int punctCount = 0;
                    foreach (char c in parts[0]) if (char.IsPunctuation(c)) ++punctCount;
                    hist.File(parts[0].Length, parts[1].Length, punctCount);
                }
            }
            hist.Write(outfn);
        }

        static bool isGood(string zh, string hu)
        {
            // Max ZH length: 40
            if (zh.Length > 40) return false;

            int punctCount = 0;
            foreach (char c in zh) if (char.IsPunctuation(c)) ++punctCount;

            double minLenRatio, maxLenRatio;
            double maxPunctRatio;
            if (zh.Length <= 5)
            {
                minLenRatio = 2.5;
                maxLenRatio = 7;
                maxPunctRatio = 0.3;
            }
            else if (zh.Length <= 20)
            {
                minLenRatio = 2.3;
                maxLenRatio = 5;
                maxPunctRatio = 0.2;
            }
            else
            {
                minLenRatio = 2.1;
                maxLenRatio = 4;
                maxPunctRatio = 0.1;
            }
            double ratio = ((double)hu.Length) / zh.Length;
            if (ratio < minLenRatio || ratio > maxLenRatio) return false;
            double pratio = ((double)punctCount) / zh.Length;
            if (pratio > maxPunctRatio) return false;
            return true;
        }

        static void filterB()
        {
            int droppedCount = 0;
            int keptCount = 0;
            using (StreamReader sr = ropen("02-zh-hu.txt"))
            using (StreamWriter sw = wopen("03-zh-hu.txt"))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    bool keep = isGood(parts[0], parts[1]);
                    if (!keep) { ++droppedCount; continue; }
                    ++keptCount;
                    sw.Write(parts[0]);
                    sw.Write('\t');
                    sw.WriteLine(parts[1]);
                }
            }
            Console.WriteLine("Kept: " + keptCount);
            Console.WriteLine("Dropped: " + droppedCount);
        }

        static void splitForTok()
        {
            using (StreamReader sr = ropen("03-zh-hu.txt"))
            using (StreamWriter swZh = wopen("04-tmp-zh.txt"))
            using (StreamWriter swHu = wopen("04-tmp-hu.txt"))
            using (StreamWriter swHuLo = wopen("04-tmp-hu-lo.txt"))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    swZh.WriteLine(parts[0]);
                    swHu.WriteLine(parts[1]);
                    swHuLo.WriteLine(parts[1].ToLower());
                }
            }
        }

        static Dictionary<string, string> surf2Stem = new Dictionary<string, string>();
        //static string[] noStemSurfs = new string[] { "az", "ez", "én", "te", "ő", "mi", "ti", "ők" };
        static string[] noStemSurfs = new string[0];

        static void buildStemDict()
        {
            // Gather lower-case words over a frequency threshold (20)
            // If these have no analysis, we'll keep them nonetheless, stemmed to themselves
            // This includes légyszives, baszki etc.
            HashSet<string> frequentForms = new HashSet<string>();
            using (StreamReader sr = ropen("04-tmp-hu-freqs.txt"))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    if (parts.Length != 2) continue;
                    int freq = int.Parse(parts[0]);
                    if (freq < 20) break;
                    if (char.IsLower(parts[1][0])) frequentForms.Add(parts[1]);
                }
            }
            int unAnaCount = 0;
            Stemmer s = new Stemmer();
            using (StreamReader sr = ropen("04-tmp-hu-vocab-stemmed.txt"))
            using (StreamWriter swUnAna = wopen("04-tmp-hu-vocab-unanalyzed.txt"))
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
                        // Compound: put in + delimiters
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
                            if (i > 0) sb2.Append('+');
                            sb2.Append(parts[i]);
                        }
                        stems.Add(sb2.ToString());
                    }
                    // Find shortest stem (if any)
                    bool storedAsStem = false;
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
                            shortestStem = surf.ToLower();
                        // File lower-cased stem in dictionary, if we have one
                        if (shortestStem.Length != 0)
                        {
                            surf2Stem[surf] = shortestStem.ToLower();
                            storedAsStem = true;
                        }
                    }
                    // If no stem, but frequent lower-case word: file anyway
                    if (!storedAsStem && frequentForms.Contains(surf))
                        surf2Stem[surf] = surf.ToLower();
                    if (!storedAsStem)
                    {
                        ++unAnaCount;
                        swUnAna.WriteLine(surf);
                    }

                    // Next!
                    surf = null;
                    anas.Clear();
                }
            }
            Console.WriteLine("Stemmed or kept b/c frequent: " + surf2Stem.Count);
            Console.WriteLine("Unanalyzed: " + unAnaCount);
            // Write stem dictionary
            HashSet<string> uniqueStems = new HashSet<string>();
            foreach (var x in surf2Stem) uniqueStems.Add(x.Value);
            using (StreamWriter swStems = wopen("04-tmp-hu-stems.txt"))
            {
                foreach (var x in uniqueStems) swStems.WriteLine(x);
            }
            Console.WriteLine("Unique analyzed stems: " + uniqueStems.Count);
        }

        static void stemHuLine(string rawSeg, out string tok, out int tokCount, out int badCount)
        {
            string[] surfs = rawSeg.Split(' ');
            sb1.Clear();
            badCount = 0;
            tokCount = 0;
            foreach (string surf in surfs)
            {
                if (char.IsPunctuation(surf[0])) continue;
                ++tokCount;
                // Append everything
                if (sb1.Length > 0) sb1.Append(' ');
                // Stem if we got one
                if (surf2Stem.ContainsKey(surf))
                {
                    string stem = surf2Stem[surf];
                    bool compound = stem.IndexOf('+') >= 0;
                    if (!compound)
                    {
                        sb1.Append("1/");
                        sb1.Append(stem);
                        sb1.Append('#');
                        sb1.Append(surf);
                    }
                    else
                    {
                        string[] comps = stem.Split('+');
                        for (int i = 0; i != comps.Length; ++i)
                        {
                            if (i > 0) sb1.Append(' ');
                            sb1.Append(comps.Length.ToString());
                            sb1.Append('/');
                            sb1.Append(comps[i]);
                            sb1.Append('#');
                            sb1.Append(surf);
                        }
                        tokCount += (comps.Length - 1);
                    }
                }
                // Lower-cased surf otherwise
                if (!surf2Stem.ContainsKey(surf))
                {
                    sb1.Append("1/");
                    sb1.Append(surf.ToLower());
                    sb1.Append('#');
                    sb1.Append(surf);
                    // Upper-case is not bad apple (assuming proper name)
                    if (!char.IsUpper(surf[0])) ++badCount;
                }
            }
            tok = sb1.ToString();
        }

        static void stemHu()
        {
            buildStemDict();
            int count = 0;
            int segsNoBad = 0;
            int segsOneBad = 0;
            int segsMoreBad = 0;
            using (StreamReader sr = ropen("04-tmp-hu-rawseg.txt"))
            using (StreamWriter sw = wopen("04-tmp-hu-stemtok.txt"))
            {
                // Read raw tokenized HU
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string tok;
                    int tokCount, badCount;
                    stemHuLine(line, out tok, out tokCount, out badCount);
                    sw.Write(tokCount.ToString());
                    sw.Write('\t');
                    sw.Write(badCount.ToString());
                    sw.Write('\t');
                    sw.WriteLine(tok);
                    ++count;
                    if (badCount == 0) ++segsNoBad;
                    else if (badCount == 1) ++segsOneBad;
                    else ++segsMoreBad;
                }
            }
            Console.WriteLine("Lines: " + count);
            Console.WriteLine("No UNKs: " + segsNoBad);
            Console.WriteLine("One UNK: " + segsOneBad);
            Console.WriteLine("More UNKs: " + segsMoreBad);
        }

        static void getHuSurfVocab()
        {
            Dictionary<string, int> freqs = new Dictionary<string, int>();
            using (StreamReader sr = ropen("04-tmp-hu-rawseg.txt"))
            using (StreamWriter swv = wopen("04-tmp-hu-vocab.txt"))
            using (StreamWriter swf = wopen("04-tmp-hu-freqs.txt"))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] surfs = line.Split(' ');
                    foreach (string surf in surfs)
                    {
                        if (char.IsPunctuation(surf[0])) continue;
                        if (!freqs.ContainsKey(surf)) freqs[surf] = 1;
                        else ++freqs[surf];
                    }
                }
                List<string> ordered = new List<string>();
                foreach (var x in freqs) ordered.Add(x.Key);
                ordered.Sort((x, y) => freqs[y].CompareTo(freqs[x]));
                foreach (string surf in ordered)
                {
                    swv.WriteLine(surf);
                    swf.Write(freqs[surf].ToString());
                    swf.Write('\t');
                    swf.WriteLine(surf);
                }
            }
        }

        static StringBuilder sb1 = new StringBuilder();
        static StringBuilder sb2 = new StringBuilder();

        static string compact(string str)
        {
            sb1.Clear();
            foreach (char c in str)
            {
                if (char.IsPunctuation(c)) continue;
                if (char.IsWhiteSpace(c)) continue;
                if (c == '￭') continue;
                sb1.Append(c);
            }
            return sb1.ToString();
        }

        static void tokToMapBPE(string sufLo, string inTok, out string tokStr, out string mapStr)
        {
            int surfPos = 0;
            sb1.Clear(); sb2.Clear();
            string[] toks = inTok.Split(' ');
            int tokIx = 0;
            for (int i = 0; i != toks.Length; ++i)
            {
                string tok = toks[i];
                if (tok == "") continue;
                if (char.IsPunctuation(tok[0])) continue;
                if (tok.IndexOf('￭') >= 0) tok = tok.Replace("￭", "");
                if (tok.EndsWith(".") || tok.EndsWith(","))
                {
                    tok = tok.Substring(0, tok.Length - 1);
                    if (tok.Length == 0) { tokStr = mapStr = null; return; }
                }
                if (sb1.Length > 0) sb1.Append(' ');
                sb1.Append(tok);
                surfPos = sufLo.IndexOf(tok, surfPos);
                // Fail?
                if (surfPos < 0) { tokStr = mapStr = null; return; }
                if (sb2.Length > 0) sb2.Append(' ');
                sb2.Append(surfPos.ToString());
                sb2.Append('/');
                sb2.Append(tok.Length.ToString());
                surfPos += tok.Length;
                ++tokIx;
            }
            if (sb1.Length == 0) { tokStr = mapStr = null; return; }
            tokStr = sb1.ToString();
            mapStr = sb2.ToString();
        }

        static void tokToMapStem(string segSurf, string inTok, out string tokStr, out string mapStr)
        {
            sb1.Clear(); sb2.Clear();
            string[] toks = inTok.Split(' ');
            int tIx = 0;
            int sPos = 0;
            while (tIx < toks.Length)
            {
                int tokSepPos = toks[tIx].IndexOf('#');
                string tok = toks[tIx].Substring(2, tokSepPos - 2);
                string surf = toks[tIx].Substring(tokSepPos + 1);
                sPos = segSurf.IndexOf(surf, sPos);
                int comps = int.Parse(toks[tIx].Substring(0, 1));
                // Not a compound? Easy.
                if (comps == 1)
                {
                    if (sb1.Length > 0) sb1.Append(' ');
                    sb1.Append(tok);
                    if (sb2.Length > 0) sb2.Append(' ');
                    sb2.Append(sPos.ToString());
                    sb2.Append('/');
                    sb2.Append(surf.Length.ToString());
                    sPos += surf.Length;
                    ++tIx;
                    continue;
                }
                // Compound: bit of trickery with character indexes within surf
                int surfLenUsed = 0;
                while (comps > 0)
                {
                    if (sb1.Length > 0) sb1.Append(' ');
                    sb1.Append(tok);
                    if (sb2.Length > 0) sb2.Append(' ');
                    sb2.Append(sPos.ToString());
                    sb2.Append('/');
                    // Length: except for last, length of stem, but not beyond length of surface form
                    if (comps > 1)
                    {
                        int len = tok.Length;
                        if (len + surfLenUsed > surf.Length) len = surf.Length - surfLenUsed;
                        if (len == 0) throw new Exception("Ran beyond end of surface form in compound stem");
                        sb2.Append(len.ToString());
                        surfLenUsed += len;
                        sPos += len;
                    }
                    // Last: till end of surf
                    else
                    {
                        if (surfLenUsed >= surf.Length) throw new Exception("Ran beyond end of surface form in compound stem");
                        int len = surf.Length - surfLenUsed;
                        sb2.Append(len.ToString());
                        surfLenUsed += len;
                        sPos += len;
                    }
                    ++tIx;
                    --comps;
                    if (comps > 0)
                    {
                        tokSepPos = toks[tIx].IndexOf('#');
                        tok = toks[tIx].Substring(2, tokSepPos - 2);
                    }
                }
            }

            if (sb1.Length == 0) { tokStr = mapStr = null; return; }
            tokStr = sb1.ToString();
            mapStr = sb2.ToString();
        }

        static void getZhFreqsJieba()
        {
            Dictionary<string, int> freqs = new Dictionary<string, int>();
            using (StreamReader srZhSeg = ropen("04-tmp-zh-seg-jieba.txt"))
            using (StreamWriter swFreq = wopen("04-zh-wordfreqs-jieba.txt"))
            {
                string line;
                while ((line = srZhSeg.ReadLine()) != null)
                {
                    string[] toks = line.Split(' ');
                    foreach (string tok in toks)
                    {
                        if (tok == "") continue;
                        if (char.IsPunctuation(tok[0])) continue;
                        if (char.IsDigit(tok[0])) continue;
                        if (!freqs.ContainsKey(tok)) freqs[tok] = 1;
                        else ++freqs[tok];
                    }
                }
                List<string> zhWords = new List<string>();
                foreach (var x in freqs) zhWords.Add(x.Key);
                zhWords.Sort((x, y) => freqs[y].CompareTo(freqs[x]));
                foreach (var word in zhWords)
                {
                    swFreq.Write(freqs[word]);
                    swFreq.Write('\t');
                    swFreq.Write(word);
                    swFreq.Write('\n');
                }
            }
        }

        static void tokenizeZh(string tokChunksFile)
        {
            Dictionary<string, string> chunktToTok = new Dictionary<string, string>();
            Dictionary<string, int> freqs = new Dictionary<string, int>();
            using (StreamReader sr = ropen(tokChunksFile))
            using (StreamReader srToTok = ropen("04-tmp-zh.txt"))
            using (StreamWriter swZh = wopen("04-tmp-zh-seg-collocfew.txt"))
            using (StreamWriter swFreq = wopen("04-zh-wordfreqs-colloc.txt"))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    chunktToTok[parts[0]] = parts[1];
                    string[] toks = parts[1].Split(' ');
                    foreach (string tok in toks)
                    {
                        if (char.IsPunctuation(tok[0])) continue;
                        if (char.IsDigit(tok[0])) continue;
                        if (!freqs.ContainsKey(tok)) freqs[tok] = 1;
                        else ++freqs[tok];
                    }
                }
                StringBuilder sbOut = new StringBuilder();
                while ((line = srToTok.ReadLine()) != null)
                {
                    sbOut.Clear();
                    string[] chunks = line.Split(' ');
                    foreach (string chunk in chunks)
                    {
                        if (chunk == "") continue;
                        if (sbOut.Length > 0) sbOut.Append(' ');
                        sbOut.Append(chunktToTok[chunk]);
                    }
                    swZh.WriteLine(sbOut.ToString());
                }
                List<string> zhWords = new List<string>();
                foreach (var x in freqs) zhWords.Add(x.Key);
                zhWords.Sort((x, y) => freqs[y].CompareTo(freqs[x]));
                foreach (var word in zhWords)
                {
                    swFreq.Write(freqs[word]);
                    swFreq.Write('\t');
                    swFreq.Write(word);
                    swFreq.Write('\n');
                }
            }
        }

        static void remixTokenized()
        {
            int keptCount = 0;
            int droppedCountBadStem = 0;
            int droppedCountDupe = 0;
            int failedCount = 0;
            HashSet<int> hashes = new HashSet<int>();
            using (StreamReader srMain = ropen("03-zh-hu.txt"))
            using (StreamReader srZhTok = ropen("04-tmp-zh-seg-jieba.txt"))
            using (StreamReader srHuTok20 = ropen("04-tmp-hu-bpe20tok.txt"))
            using (StreamReader srHuTok40 = ropen("04-tmp-hu-bpe40tok.txt"))
            using (StreamReader srHuTokStem = ropen("04-tmp-hu-stemtok.txt"))
            using (StreamWriter sw04 = wopen("04-zh-hu.txt"))
            using (StreamWriter sw05tmpZhStem = wopen("05-tmp-zh-hustem.txt"))
            using (StreamWriter sw05tmpStemZh = wopen("05-tmp-hustem-zh.txt"))
            {
                while (true)
                {
                    string lnMain = srMain.ReadLine();
                    if (lnMain == null) break;
                    string[] parts = lnMain.Split('\t');
                    string lnZhTok = srZhTok.ReadLine();
                    //srZhTok.ReadLine(); // Extra empty lines in Jieba output
                    string lnHuTok20 = srHuTok20.ReadLine();
                    string lnHuTok40 = srHuTok40.ReadLine();
                    string lnHuTokStem = srHuTokStem.ReadLine();
                    string[] lnHuTokStemParts = lnHuTokStem.Split('\t');

                    if (lnHuTokStemParts[2] == "")
                    {
                        ++droppedCountBadStem;
                        continue;
                    }

                    // Too many unanalyzed tokens: drop (might be English, many typos etc.)
                    int stemTokCount = int.Parse(lnHuTokStemParts[0]);
                    int stemBadCount = int.Parse(lnHuTokStemParts[1]);
                    bool dropBadStem = false;
                    if (stemTokCount <= 3 && stemBadCount > 0) dropBadStem = true;
                    if (stemTokCount > 3 && stemBadCount * 3 >= stemTokCount) dropBadStem = true;
                    if (stemBadCount >= 3) dropBadStem = true;
                    if (dropBadStem)
                    {
                        ++droppedCountBadStem;
                        continue;
                    }

                    // Compact form of HU for hash. Any tokenized version will yield the same.
                    string huCompact = compact(lnHuTok20);
                    string toHash = parts[0].Replace(" ", "") + '\t' + huCompact;
                    int hash = toHash.GetHashCode();
                    if (hashes.Contains(hash))
                    {
                        ++droppedCountDupe;
                        continue;
                    }
                    hashes.Add(hash);

                    string[] tokFields = new string[4];
                    string tokZh, tokHu20, tokHu40, tokHuStem, map;
                    tokToMapBPE(parts[0], lnZhTok, out tokZh, out map);
                    tokFields[0] = map;

                    string huLo = parts[1].ToLower();
                    tokToMapBPE(huLo, lnHuTok20, out tokHu20, out map);
                    tokFields[1] = map;
                    tokToMapBPE(huLo, lnHuTok40, out tokHu40, out map);
                    tokFields[2] = map;
                    tokToMapStem(parts[1], lnHuTokStemParts[2], out tokHuStem, out map);
                    tokFields[3] = map;

                    if (Array.IndexOf(tokFields, null) >= 0)
                    {
                        ++failedCount;
                        continue;

                    }
                    sw04.Write((keptCount + 1).ToString());
                    sw04.Write('\t');
                    sw04.Write(parts[0]);
                    sw04.Write('\t');
                    sw04.Write(parts[1]);
                    foreach (var tf in tokFields)
                    {
                        sw04.Write('\t');
                        sw04.Write(tf);
                    }
                    sw04.Write('\n');

                    //sw05tmp20.Write(tokZh);
                    //sw05tmp20.Write(" ||| ");
                    //sw05tmp20.Write(tokHu20);
                    //sw05tmp20.Write('\n');
                    //sw05tmp40.Write(tokZh);
                    //sw05tmp40.Write(" ||| ");
                    //sw05tmp40.Write(tokHu40);
                    //sw05tmp40.Write('\n');

                    sw05tmpZhStem.Write(tokZh);
                    sw05tmpZhStem.Write(" ||| ");
                    sw05tmpZhStem.Write(tokHuStem);
                    sw05tmpZhStem.Write('\n');

                    sw05tmpStemZh.Write(tokHuStem);
                    sw05tmpStemZh.Write(" ||| ");
                    sw05tmpStemZh.Write(tokZh);
                    sw05tmpStemZh.Write('\n');

                    ++keptCount;
                }
            }
            Console.WriteLine("Kept: " + keptCount);
            Console.WriteLine("Dropped (failed to stem): " + droppedCountBadStem);
            Console.WriteLine("Dropped (dupe): " + droppedCountDupe);
            Console.WriteLine("Failed (token map): " + failedCount);
        }

        static void tmpGetForAlignSurf()
        {
            StringBuilder sbHu = new StringBuilder();
            StringBuilder sbZh = new StringBuilder();
            HashSet<int> hashes = new HashSet<int>();
            using (StreamReader srMain = ropen("03-zh-hu.txt"))
            using (StreamReader srZhTok = ropen("04-tmp-zh-seg-jieba.txt"))
            using (StreamReader srHuTok20 = ropen("04-tmp-hu-bpe20tok.txt"))
            using (StreamReader srHuTokStem = ropen("04-tmp-hu-stemtok.txt"))
            using (StreamWriter swTmpFull = wopen("05-tmp-zh-hufull.txt"))
            {
                while (true)
                {
                    string lnMain = srMain.ReadLine();
                    if (lnMain == null) break;
                    string[] parts = lnMain.Split('\t');
                    string lnZhTok = srZhTok.ReadLine();
                    string lnHuTokStem = srHuTokStem.ReadLine();
                    string[] lnHuTokStemParts = lnHuTokStem.Split('\t');
                    string lnHuTok20 = srHuTok20.ReadLine();

                    if (lnHuTokStemParts[2] == "") continue;

                    // Too many unanalyzed tokens: drop (might be English, many typos etc.)
                    int stemTokCount = int.Parse(lnHuTokStemParts[0]);
                    int stemBadCount = int.Parse(lnHuTokStemParts[1]);
                    bool dropBadStem = false;
                    if (stemTokCount <= 3 && stemBadCount > 0) dropBadStem = true;
                    if (stemTokCount > 3 && stemBadCount * 3 >= stemTokCount) dropBadStem = true;
                    if (stemBadCount >= 3) dropBadStem = true;
                    if (dropBadStem) continue;

                    // Compact form of HU for hash. Any tokenized version will yield the same.
                    string huCompact = compact(lnHuTok20);
                    string toHash = parts[0].Replace(" ", "") + '\t' + huCompact;
                    int hash = toHash.GetHashCode();
                    if (hashes.Contains(hash)) continue;
                    hashes.Add(hash);

                    sbZh.Clear();
                    string[] zhToks = lnZhTok.Split(' ');
                    foreach (string tok in zhToks)
                    {
                        if (tok == "") continue;
                        if (char.IsPunctuation(tok[0])) continue;
                        if (sbZh.Length > 0) sbZh.Append(' ');
                        sbZh.Append(tok);
                    }

                    sbHu.Clear();
                    string[] huToks = lnHuTokStemParts[2].Split(' ');
                    int ix = 0;
                    while (ix < huToks.Length)
                    {
                        string tok = huToks[ix];
                        if (tok == "") { ++ix; continue; }
                        int cnt = int.Parse(tok.Substring(0, 1));
                        if (cnt == 0) { ++ix;  continue; }
                        string surf = tok.Substring(tok.IndexOf('#') + 1).ToLower();
                        if (sbHu.Length > 0) sbHu.Append(' ');
                        sbHu.Append(surf);
                        ix += cnt;
                    }
                    swTmpFull.Write(sbZh.ToString());
                    swTmpFull.Write(" ||| ");
                    swTmpFull.Write(sbHu.ToString());
                    swTmpFull.Write('\n');
                }
            }
        }

        static void remixForMT(bool huStemmed, bool useZhStem)
        {
            int count = 0;
            int validCount = 0;
            HashSet<int> hashes = new HashSet<int>();
            StringBuilder sb = new StringBuilder();
            string huFile = huStemmed ? "04-tmp-hu-stemtok.txt" : "04-tmp-hu-bpe10tok.txt";
            using (StreamReader srMain = ropen("03-zh-hu.txt"))
            using (StreamReader srZhTok = ropen("04-tmp-zh-seg-jieba.txt"))
            using (StreamReader srHuTok = ropen(huFile))
            using (StreamWriter swZhTrain = wopen("10-zh-train.txt"))
            using (StreamWriter swHuTrain = wopen("10-hu-train.txt"))
            using (StreamWriter swZhValid = wopen("10-zh-valid.txt"))
            using (StreamWriter swHuValid = wopen("10-hu-valid.txt"))
            {
                while (true)
                {
                    string lnMain = srMain.ReadLine();
                    if (lnMain == null) break;
                    string[] parts = lnMain.Split('\t');
                    string lnZhTok = srZhTok.ReadLine();
                    string lnHuTok = srHuTok.ReadLine();

                    if (huStemmed)
                    {
                        sb.Clear();
                        string[] toks = lnHuTok.Split('\t')[2].Split(' ');
                        foreach (string tok in toks)
                        {
                            if (tok == "") continue;
                            int ix1 = tok.IndexOf('/');
                            int ix2 = tok.IndexOf('#');
                            if (sb.Length > 0) sb.Append(' ');
                            sb.Append(tok.Substring(ix1 + 1, ix2 - ix1 - 1));
                        }
                        lnHuTok = sb.ToString();
                    }


                    // Compact form of HU for hash. Any tokenized version will yield the same.
                    string huCompact = compact(lnHuTok);
                    string toHash = parts[0].Replace(" ", "") + '\t' + huCompact;
                    int hash = toHash.GetHashCode();
                    if (hashes.Contains(hash)) continue;
                    hashes.Add(hash);

                    ++count;
                    bool isValid = false;
                    if (count % 580 == 0 && validCount < 5000) { isValid = true; ++validCount; }
                    StreamWriter swHu = isValid ? swHuValid : swHuTrain;
                    StreamWriter swZh = isValid ? swZhValid : swZhTrain;
                    swHu.WriteLine(lnHuTok);

                    sb.Clear();
                    if (!useZhStem)
                    {
                        string[] zhToks = lnZhTok.Split(' ');
                        foreach (string tok in zhToks)
                        {
                            if (tok == "") continue;
                            if (char.IsDigit(tok[0]) || (tok[0] >= 'a' && tok[0] <= 'z') || (tok[0] >= 'A' && tok[0] <= 'Z'))
                            {
                                if (sb.Length > 0) sb.Append(' ');
                                sb.Append(tok);
                            }
                            else
                            {
                                foreach (char c in tok)
                                {
                                    if (sb.Length > 0) sb.Append(' ');
                                    sb.Append(c);
                                }
                            }
                        }
                        swZh.WriteLine(sb.ToString());
                    }
                    else swZh.WriteLine(lnZhTok);
                }
            }
        }

        static void buildAligned()
        {
            string[] parts1, parts2;
            using (StreamReader srMain = ropen("04-zh-hu.txt"))
            using (StreamReader sr05ZhHuT = ropen("05-tmp-zh-hustem.txt"))
            using (StreamReader sr05ZhHuA = ropen("05-tmp-zh-hustem.align"))
            using (StreamReader sr05HuZhA = ropen("05-tmp-hustem-zh.align"))
            using (StreamWriter swHuStem = wopen("zhhu-hustem.txt"))
            using (StreamWriter swHuLo = wopen("zhhu-hulo.txt"))
            using (StreamWriter swZh = wopen("zhhu-zh.txt"))
            using (var bw = new ZD.Common.BinWriter("zhhu-data.bin"))
            {
                while (true)
                {
                    string lnMain = srMain.ReadLine();
                    if (lnMain == null) break;
                    string lnZhHu = sr05ZhHuT.ReadLine();
                    lnZhHu = lnZhHu.Replace(" ||| ", "\t");
                    string huStem = lnZhHu.Split('\t')[1];
                    string lnZhHuA = sr05ZhHuA.ReadLine();
                    string lnHuZhA = sr05HuZhA.ReadLine();

                    string[] parts = lnMain.Split('\t');
                    string zhSurf = parts[1];
                    string huSurf = parts[2];
                    string huLo = huSurf.ToLower();
                    
                    string zhTokMapStr = parts[3];
                    List<short[]> zhTokMap = new List<short[]>();
                    parts1 = zhTokMapStr.Split(' ');
                    foreach (string part in parts1)
                    {
                        parts2 = part.Split('/');
                        zhTokMap.Add(new short[2] { short.Parse(parts2[0]), short.Parse(parts2[1]) });
                    }

                    string huTokMapStr = parts[6];
                    List<short[]> huTokMap = new List<short[]>();
                    parts1 = huTokMapStr.Split(' ');
                    foreach (string part in parts1)
                    {
                        parts2 = part.Split('/');
                        huTokMap.Add(new short[2] { short.Parse(parts2[0]), short.Parse(parts2[1]) });
                    }

                    List<ZD.Common.CorpusSegment.AlignPair> zhToHuAlign = new List<Common.CorpusSegment.AlignPair>();
                    parts1 = lnZhHuA.Split(' ');
                    foreach (string part in parts1)
                    {
                        if (part == "") continue;
                        int pos1 = part.IndexOf('-');
                        int pos2 = part.IndexOf('!');
                        var alm = new Common.CorpusSegment.AlignPair
                        {
                            Ix1 = short.Parse(part.Substring(0, pos1)),
                            Ix2 = short.Parse(part.Substring(pos1 + 1, pos2 - pos1 - 1)),
                            Score = float.Parse(part.Substring(pos2 + 1))
                        };
                        zhToHuAlign.Add(alm);
                    }

                    List<ZD.Common.CorpusSegment.AlignPair> huToZhAlign = new List<Common.CorpusSegment.AlignPair>();
                    parts1 = lnHuZhA.Split(' ');
                    foreach (string part in parts1)
                    {
                        if (part == "") continue;
                        int pos1 = part.IndexOf('-');
                        int pos2 = part.IndexOf('!');
                        var alm = new Common.CorpusSegment.AlignPair
                        {
                            Ix1 = short.Parse(part.Substring(0, pos1)),
                            Ix2 = short.Parse(part.Substring(pos1 + 1, pos2 - pos1 - 1)),
                            Score = float.Parse(part.Substring(pos2 + 1))
                        };
                        huToZhAlign.Add(alm);
                    }

                    int pos = bw.Position + 1;
                    ZD.Common.CorpusSegment cseg = new Common.CorpusSegment(zhSurf, huSurf, zhTokMap, huTokMap, zhToHuAlign, huToZhAlign);
                    cseg.Serialize(bw);

                    swHuStem.Write(pos.ToString());
                    swHuStem.Write('\t');
                    swHuStem.WriteLine(huStem);

                    swHuLo.Write(pos.ToString());
                    swHuLo.Write('\t');
                    swHuLo.WriteLine(huLo);

                    swZh.Write(pos.ToString());
                    swZh.Write('\t');
                    swZh.WriteLine(zhSurf);
                }
            }
        }

        static void remixAligned()
        {
            using (StreamReader srMain = ropen("04-zh-hu.txt"))
            //using (StreamReader sr05tmp20A = ropen("05-tmp-zh-hu20.align"))
            //using (StreamReader sr05tmp40A = ropen("05-tmp-zh-hu40.align"))
            using (StreamReader sr05tmpStemA = ropen("05-tmp-zh-hustem.align"))
            using (StreamWriter sw05 = wopen("05-zh-hu.txt"))
            {
                while (true)
                {
                    string lnMain = srMain.ReadLine();
                    if (lnMain == null) break;
                    string[] parts = lnMain.Split('\t');
                    //TMP
                    //string ln20A = sr05tmp20A.ReadLine();
                    //string ln40A = sr05tmp40A.ReadLine();
                    string ln20A = "";
                    string ln40A = "";
                    string lnStemA = sr05tmpStemA.ReadLine();
                    sw05.Write(parts[0]); // IDX
                    sw05.Write('\t');
                    sw05.Write(parts[1]); // ZH
                    sw05.Write('\t');
                    sw05.Write(parts[2]); // HU
                    sw05.Write('\t');
                    sw05.Write(parts[3]); // ZH-TOK-MAP
                    sw05.Write('\t');
                    sw05.Write(parts[4]); // HU-TOK20-MAP
                    sw05.Write('\t');
                    sw05.Write(ln20A); // ALIGN-20
                    sw05.Write('\t');
                    sw05.Write(parts[5]); // HU-TOK-40-MAP
                    sw05.Write('\t');
                    sw05.Write(ln40A); // ALIGN-40
                    sw05.Write('\t');
                    sw05.Write(parts[6]); // HU-TOK-STEM-MAP
                    sw05.Write('\t');
                    sw05.Write(lnStemA); // ALIGN-STEM
                    sw05.Write('\n');
                }
            }
        }

        static void stemTest()
        {
            Stemmer stemmer = new Stemmer();
            using (FileStream fs = new FileStream("_ana.txt", FileMode.Open, FileAccess.Read))
            using (StreamReader sr = new StreamReader(fs))
            using (FileStream fsx = new FileStream("_stem.txt", FileMode.Create, FileAccess.ReadWrite))
            using (StreamWriter sw = new StreamWriter(fsx))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line == "")
                    {
                        sw.WriteLine();
                        continue;
                    }
                    var ana = line.Split('\t')[1];
                    if (ana.EndsWith("?"))
                    {
                        sw.WriteLine(ana);
                        continue;
                    }
                    string norm = stemmer.preproc(ana);
                    Stemmer.Stem st = stemmer.process(norm);
                    if (st.compoundDelims.Count == 0)
                    {
                        sw.WriteLine(st.szStem);
                    }
                    else
                    {
                        List<string> parts = new List<string>();
                        int ix = 0;
                        for (int i = 0; i < st.compoundDelims.Count; ++i)
                        {
                            parts.Add(st.szStem.Substring(ix, st.compoundDelims[i] - ix));
                            ix = st.compoundDelims[i];
                        }
                        parts.Add(st.szStem.Substring(ix));
                        for (int i = 0; i != parts.Count; ++i)
                        {
                            if (i > 0) sw.Write('+');
                            sw.Write(parts[i]);
                        }
                        sw.WriteLine();
                    }
                }
            }
        }

        private class StemFreqItem
        {
            public string Stem;
            public int AllFreq = 0;
            public Dictionary<string, int> FormFreqs = new Dictionary<string, int>();
        }

        static Regex resStripPunct = new Regex("\\p{P}");

        static void getForWord2Vec()
        {
            Dictionary<string, StemFreqItem> huStemToFreq = new Dictionary<string, StemFreqItem>();
            Dictionary<string, int> zhFreq = new Dictionary<string, int>();
            int maxJointLen = 0;
            int sumLen = 0;
            int count = 0;
            using (StreamReader sr04ZhoTok = ropen("04-tmp-zh-seg-jieba.txt"))
            using (StreamReader sr04Stemtok = ropen("04-tmp-hu-stemtok.txt"))
            using (StreamWriter swHuFreqs = wopen("10-jiestem-hufreqs.txt"))
            using (StreamWriter swZhfreqs = wopen("10-jiestem-zhfreqs.txt"))
            using (StreamWriter swW2V = wopen("10-jiestem-for-w2v.txt"))
            {
                string line;
                // Chinese frequencies, and w2v output
                while ((line = sr04ZhoTok.ReadLine()) != null)
                {
                    string lineHu = sr04Stemtok.ReadLine();
                    ++count;
                    string[] zhToks = line.Split(' ');
                    bool first = true;
                    foreach (string zhTok in zhToks)
                    {
                        if (!first) swW2V.Write(' ');
                        swW2V.Write("zh_");
                        swW2V.Write(zhTok);
                        first = false;
                        // ZH counts
                        if (!zhFreq.ContainsKey(zhTok)) zhFreq[zhTok] = 1;
                        else ++zhFreq[zhTok];
                    }
                    string[] huStems = lineHu.Split(' ');
                    StringBuilder sb = new StringBuilder();
                    foreach (string tok in huStems)
                    {
                        if (tok == "") continue;
                        int ix1 = tok.IndexOf('/');
                        if (ix1 == -1) continue;
                        int ix2 = tok.IndexOf('#');
                        if (sb.Length > 0) sb.Append(' ');
                        sb.Append(tok.Substring(ix1 + 1, ix2 - ix1 - 1));
                    }
                    huStems = sb.ToString().Split(' ');

                    foreach (string huStem in huStems)
                    {
                        if (!first) swW2V.Write(' ');
                        swW2V.Write("hu_");
                        swW2V.Write(huStem);
                        first = false;
                    }
                    swW2V.Write('\n');
                    if (zhToks.Length + huStems.Length > maxJointLen) maxJointLen = zhToks.Length + huStems.Length;
                    sumLen += zhToks.Length + huStems.Length;
                }
                List<string> zhFreqVect = new List<string>();
                foreach (var x in zhFreq) zhFreqVect.Add(x.Key);
                zhFreqVect.Sort((x, y) => zhFreq[y].CompareTo(zhFreq[x]));
                foreach (var zhTok in zhFreqVect)
                {
                    swZhfreqs.Write(zhFreq[zhTok].ToString());
                    swZhfreqs.Write('\t');
                    swZhfreqs.Write(zhTok);
                    swZhfreqs.Write('\n');
                }
                zhFreqVect.Clear();
                zhFreq.Clear();
                // Max joint length (in tokens)
                Console.WriteLine("Max length: " + maxJointLen);
                double avgLen = ((double)sumLen) / count;
                Console.WriteLine("Avg length: " + avgLen.ToString("0.00"));
                // Hungarian stem frequencies
                sr04Stemtok.BaseStream.Position = 0;
                while ((line = sr04Stemtok.ReadLine()) != null)
                {
                    string sent = line.Split()[2];
                    string[] words = sent.Split(' ');
                    foreach (string word in words)
                    {
                        if (word == "") continue;
                        int tokCount = int.Parse(word.Substring(0, 1));
                        int slashPos = word.IndexOf('/');
                        int hashPos = word.IndexOf('#');
                        string stem = word.Substring(slashPos + 1, hashPos - slashPos - 1).ToLower();
                        string surf = word.Substring(hashPos + 1).ToLower();
                        surf = resStripPunct.Replace(surf, "");
                        if (tokCount > 1) surf = stem;
                        StemFreqItem sfi = null;
                        if (!huStemToFreq.ContainsKey(stem))
                        {
                            sfi = new StemFreqItem { Stem = stem };
                            huStemToFreq[stem] = sfi;
                        }
                        else sfi = huStemToFreq[stem];
                        ++sfi.AllFreq;
                        if (!sfi.FormFreqs.ContainsKey(surf)) sfi.FormFreqs[surf] = 1;
                        else ++sfi.FormFreqs[surf];
                    }
                }
                List<StemFreqItem> huFreqs = new List<StemFreqItem>();
                foreach (var x in huStemToFreq) huFreqs.Add(x.Value);
                huFreqs.Sort((x, y) => y.AllFreq.CompareTo(x.AllFreq));
                foreach (var sfi in huFreqs)
                {
                    swHuFreqs.Write(sfi.AllFreq.ToString());
                    swHuFreqs.Write('\t');
                    swHuFreqs.Write(sfi.Stem);
                    swHuFreqs.Write('\t');
                    List<string> surfs = new List<string>();
                    foreach (var x in sfi.FormFreqs) surfs.Add(x.Key);
                    surfs.Sort((x, y) => sfi.FormFreqs[y].CompareTo(sfi.FormFreqs[x]));
                    bool first = true;
                    foreach (string surf in surfs)
                    {
                        if (!first) swHuFreqs.Write(' ');
                        swHuFreqs.Write(sfi.FormFreqs[surf].ToString());
                        swHuFreqs.Write('/');
                        swHuFreqs.Write(surf);
                        first = false;
                    }
                    swHuFreqs.Write('\n');
                }
                huStemToFreq.Clear();
                huFreqs.Clear();
            }
        }

        static void getForColloc()
        {
            using (var sr = ropen("04-tmp-zh.txt"))
            using (var sw = wopen("04-tmp-zh-tocolloc.txt"))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] sents = line.Split(' ');
                    foreach (string sent in sents)
                    {
                        if (sent == "") continue;
                        bool barf = false;
                        foreach (char c in sent)
                        {
                            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                            if (cat == System.Globalization.UnicodeCategory.PrivateUse ||
                                cat == System.Globalization.UnicodeCategory.Surrogate)
                            {
                                barf = true;
                                break;
                            }
                        }
                        if (barf) continue;
                        sw.Write(sent);
                        sw.Write('\t');
                        for (int i = 0; i < sent.Length; ++i)
                        {
                            if (i > 0) sw.Write(' ');
                            sw.Write(sent[i]);
                        }
                        sw.Write('\n');
                    }
                }
            }
        }

        static int gramSampleSize = 0;
        static Dictionary<string, int> coCounts = new Dictionary<string, int>();
        static Dictionary<string, int> uniCounts = new Dictionary<string, int>();
        static Dictionary<string, double> grams = new Dictionary<string, double>();
        static Dictionary<string, string> mergeables = new Dictionary<string, string>();

        static void mergeColloc(double threshold, int minCount, string inName, string gramName, string outName, bool preMerge)
        {
            string line;
            gramSampleSize = 0;
            coCounts.Clear();
            uniCounts.Clear();
            grams.Clear();
            using (var sr = ropen(inName))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    string[] cols = line.Split('\t');
                    string line2 = preMerge ? mergeAlnum(cols[1]) : cols[1];
                    string[] parts = line2.Split(' ');
                    for (int i = 0; i < parts.Length; ++i)
                    {
                        string a = parts[i];
                        if (char.IsPunctuation(a[0]) || char.IsDigit(a[0])) continue;
                        if (!uniCounts.ContainsKey(a)) uniCounts[a] = 1;
                        else ++uniCounts[a];
                        if (i + 1 < parts.Length)
                        {
                            string b = parts[i + 1];
                            if (char.IsPunctuation(b[0]) || char.IsDigit(b[0])) continue;
                            ++gramSampleSize;
                            string bi = "" + a + " " + b;
                            if (!coCounts.ContainsKey(bi)) coCounts[bi] = 1;
                            else ++coCounts[bi];
                        }
                    }
                }
            }
            foreach (var x in coCounts)
            {
                string bi = x.Key;
                if (x.Value < minCount) continue;
                string[] ab = bi.Split(' ');
                if (isMergeBlocked(ab[0], ab[1])) continue;
                double scoreMI = Math.Log(gramSampleSize / ((double)uniCounts[ab[0]] * uniCounts[ab[1]]) * coCounts[bi]);
                if (scoreMI > threshold)
                {
                    grams[bi] = scoreMI;
                    mergeables[bi] = bi.Replace(" ", "");
                }
            }
            Console.WriteLine("Merged gram count: " + grams.Count);
            List<string> biSorted = new List<string>();
            foreach (var x in grams) biSorted.Add(x.Key);
            biSorted.Sort((x, y) => coCounts[y].CompareTo(coCounts[x]));
            using (var sw = wopen(gramName))
            {
                foreach (string bi in biSorted)
                {
                    sw.Write(bi.Replace(" ", "_"));
                    sw.Write('\t');
                    sw.Write(coCounts[bi]);
                    sw.Write('\t');
                    sw.Write(grams[bi].ToString("0.000"));
                    sw.Write('\n');
                }
            }
            List<object>[] chartToRepl = new List<object>[65536];
            foreach (var x in mergeables)
            {
                if (chartToRepl[x.Key[0]] == null) chartToRepl[x.Key[0]] = new List<object>();
                chartToRepl[x.Key[0]].Add(x);
            }
            int lineCount = 0;
            using (var sr = ropen(inName))
            using (var sw = wopen(outName))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    ++lineCount;
                    if (lineCount % 100000 == 0) Console.WriteLine(lineCount.ToString());
                    string[] cols = line.Split('\t');
                    string line2 = preMerge ? mergeAlnum(cols[1]) : cols[1];
                    string[] parts = line2.Split(' ');
                    List<string> canMerge = new List<string>();
                    for (int i = 0; i + 1 < parts.Length; ++i)
                    {
                        string pair = parts[i] + " " + parts[i + 1];
                        if (mergeables.ContainsKey(pair))
                            canMerge.Add(pair);
                    }
                    canMerge.Sort((x, y) => grams[y].CompareTo(grams[x]));
                    foreach (var cm in canMerge)
                        line2 = line2.Replace(cm, "|" + mergeables[cm] + "|");
                    line2 = line2.Replace("|", "");
                    sw.Write(cols[0]);
                    sw.Write('\t');
                    sw.WriteLine(line2);
                }
            }
        }

        public static void Main(string[] args)
        {
            initMergeLimits();

            //filterA();

            //getTrad();
            //fixTrad();

            //histogram("02-zh-hu.txt", "02-lenhists.txt");
            //filterB();
            //splitForTok();

            //getHuSurfVocab();
            //stemHu();

            //getForColloc();
            //mergeColloc(2.5, 3, "04-tmp-zh-tocolloc.txt", "04-tmpA-zh-2grams.txt", "04-tmpA-zh-2merged.txt", true);
            //mergeColloc(6, 5, "04-tmpA-zh-2merged.txt", "04-tmpB-zh-3grams.txt", "04-tmpB-zh-3merged.txt", false);
            //mergeColloc(8, 3, "04-tmpB-zh-3merged.txt", "04-tmpC-zh-4grams.txt", "04-tmpC-zh-4merged.txt", false);

            //getForColloc();
            //mergeColloc(1.5, 3, "04-tmp-zh-tocolloc.txt", "04-tmpA-zh-2grams.txt", "04-tmpA-zh-2merged.txt", true);
            //mergeColloc(4.0, 3, "04-tmpA-zh-2merged.txt", "04-tmpB-zh-3grams.txt", "04-tmpB-zh-3merged.txt", false);
            //mergeColloc(4.0, 3, "04-tmpB-zh-3merged.txt", "04-tmpC-zh-4grams.txt", "04-tmpC-zh-4merged.txt", false);

            //tokenizeZh("04-tmpC-zh-4merged.txt");

            //tmpGetForAlignSurf();

            //remixForMT(false, false);
            //remixTokenized();
            //remixAligned();
            buildAligned();


            //getZhFreqsJieba();
            //getForWord2Vec();


            //getSet("隧道");
            //stemTest();

            Console.ReadLine();
        }
    }
}
