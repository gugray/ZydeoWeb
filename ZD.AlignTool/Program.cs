using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;
using System.Threading;

using ZD.LangUtils;

namespace ZD.AlignTool
{
    public class Program
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

        static Regex reLowerAlfa = new Regex("[a-z]");

        static void filterA()
        {
            int inCount = 0;
            int keptCount = 0;
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
            using (var srMain = ropen("02-tmp-zh-hu.txt"))
            using (var srSimplified = ropen("02-tmp-simplified.txt"))
            using (var sw = wopen("02-zh-hu.txt"))
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
                    sw.Write(parts[0]);
                    sw.Write('\t');
                    sw.WriteLine(parts[1]);
                }
            }
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

        static void remixTokenized()
        {
            int keptCount = 0;
            int droppedCountBadStem = 0;
            int droppedCountDupe = 0;
            int failedCount = 0;
            HashSet<int> hashes = new HashSet<int>();
            using (StreamReader srMain = ropen("03-zh-hu.txt"))
            using (StreamReader srZhTok = ropen("04-tmp-zh-seg.txt"))
            using (StreamReader srHuTok20 = ropen("04-tmp-hu-bpe20tok.txt"))
            using (StreamReader srHuTok40 = ropen("04-tmp-hu-bpe40tok.txt"))
            using (StreamReader srHuTokStem = ropen("04-tmp-hu-stemtok.txt"))
            using (StreamWriter sw04 = wopen("04-zh-hu.txt"))
            using (StreamWriter sw05tmp20 = wopen("05-tmp-zh-hu20.txt"))
            using (StreamWriter sw05tmp40 = wopen("05-tmp-zh-hu40.txt"))
            using (StreamWriter sw05tmpStem = wopen("05-tmp-zh-hustem.txt"))
            {
                while (true)
                {
                    string lnMain = srMain.ReadLine();
                    if (lnMain == null) break;
                    string[] parts = lnMain.Split('\t');
                    string lnZhTok = srZhTok.ReadLine();
                    srZhTok.ReadLine(); // Extra empty lines in Jieba output
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

                    sw05tmp20.Write(tokZh);
                    sw05tmp20.Write(" ||| ");
                    sw05tmp20.Write(tokHu20);
                    sw05tmp20.Write('\n');
                    sw05tmp40.Write(tokZh);
                    sw05tmp40.Write(" ||| ");
                    sw05tmp40.Write(tokHu40);
                    sw05tmp40.Write('\n');
                    sw05tmpStem.Write(tokZh);
                    sw05tmpStem.Write(" ||| ");
                    sw05tmpStem.Write(tokHuStem);
                    sw05tmpStem.Write('\n');

                    ++keptCount;
                }
            }
            Console.WriteLine("Kept: " + keptCount);
            Console.WriteLine("Dropped (failed to stem): " + droppedCountBadStem);
            Console.WriteLine("Dropped (dupe): " + droppedCountDupe);
            Console.WriteLine("Failed (token map): " + failedCount);
        }


        static void remixAligned()
        {
            using (StreamReader srMain = ropen("04-zh-hu.txt"))
            using (StreamReader sr05tmp20A = ropen("05-tmp-zh-hu20.align"))
            using (StreamReader sr05tmp40A = ropen("05-tmp-zh-hu40.align"))
            using (StreamReader sr05tmpStemA = ropen("05-tmp-zh-hustem.align"))
            using (StreamWriter sw05 = wopen("05-zh-hu.txt"))
            {
                while (true)
                {
                    string lnMain = srMain.ReadLine();
                    if (lnMain == null) break;
                    string[] parts = lnMain.Split('\t');
                    string ln20A = sr05tmp20A.ReadLine();
                    string ln40A = sr05tmp40A.ReadLine();
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

        static void getSet(string filter)
        {
            using (StreamReader srText = ropen("05-tmp-zh-hu20.txt"))
            using (StreamReader srAlign = ropen("05-tmp-zh-hu20.align"))
            using (StreamWriter sw = wopen("set.txt"))
            {
                while (true)
                {
                    string lnText = srText.ReadLine();
                    string lnAlign = srAlign.ReadLine();
                    if (lnText == null) break;
                    if (lnText.IndexOf(filter) < 0) continue;
                    sw.Write(lnText.Replace(" ||| ", "\t"));
                    sw.Write('\t');
                    sw.WriteLine(lnAlign);
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

        public static void Main(string[] args)
        {
            //filterA();
            //getTrad();
            //fixTrad();
            //histogram("02-zh-hu.txt", "02-lenhists.txt");
            //filterB();
            //splitForTok();
            //getHuSurfVocab();
            //stemHu();
            //remixTokenized();
            remixAligned();

            //getSet("隧道");
            //stemTest();

            Console.ReadLine();
        }
    }
}
