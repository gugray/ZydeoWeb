using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

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
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    swZh.WriteLine(parts[0]);
                    swHu.WriteLine(parts[1].ToLower());
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

        static void tokToMap(string surf, string inTok, out string tokStr, out string mapStr)
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
                surfPos = surf.IndexOf(tok, surfPos);
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

        static void remixTokenized()
        {
            int keptCount = 0;
            int droppedCount = 0;
            int failedCount = 0;
            HashSet<int> hashes = new HashSet<int>();
            using (StreamReader srMain = ropen("03-zh-hu.txt"))
            using (StreamReader srZhTok = ropen("04-tmp-zh-seg.txt"))
            using (StreamReader srHuTok20 = ropen("04-tmp-hu-bpe20tok.txt"))
            using (StreamReader srHuTok30 = ropen("04-tmp-hu-bpe30tok.txt"))
            using (StreamReader srHuTok40 = ropen("04-tmp-hu-bpe40tok.txt"))
            using (StreamWriter sw04 = wopen("04-zh-hu.txt"))
            using (StreamWriter sw05tmp20 = wopen("05-tmp-zh-hu20.txt"))
            using (StreamWriter sw05tmp30 = wopen("05-tmp-zh-hu30.txt"))
            using (StreamWriter sw05tmp40 = wopen("05-tmp-zh-hu40.txt"))
            {
                while (true)
                {
                    string lnMain = srMain.ReadLine();
                    if (lnMain == null) break;
                    string[] parts = lnMain.Split('\t');
                    string lnZhTok = srZhTok.ReadLine();
                    srZhTok.ReadLine(); // Extra empty lines in Jieba output
                    string lnHuTok20 = srHuTok20.ReadLine();
                    string lnHuTok30 = srHuTok30.ReadLine();
                    string lnHuTok40 = srHuTok40.ReadLine();

                    // Compact form of HU for hash. Any tokenized version will yield the same.
                    string huCompact = compact(lnHuTok20);
                    string toHash = parts[0].Replace(" ", "") + '\t' + huCompact;
                    int hash = toHash.GetHashCode();
                    if (hashes.Contains(hash))
                    {
                        ++droppedCount;
                        continue;
                    }
                    hashes.Add(hash);

                    string[] tokFields = new string[8];
                    string tok, map;
                    tokToMap(parts[0], lnZhTok, out tok, out map);
                    tokFields[0] = tok; tokFields[1] = map;
                    string huLo = parts[1].ToLower();
                    tokToMap(huLo, lnHuTok20, out tok, out map);
                    tokFields[2] = tok; tokFields[3] = map;
                    tokToMap(huLo, lnHuTok30, out tok, out map);
                    tokFields[4] = tok; tokFields[5] = map;
                    tokToMap(huLo, lnHuTok40, out tok, out map);
                    tokFields[6] = tok; tokFields[7] = map;

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

                    sw05tmp20.Write(tokFields[0]);
                    sw05tmp20.Write(" ||| ");
                    sw05tmp20.Write(tokFields[2]);
                    sw05tmp20.Write('\n');
                    sw05tmp30.Write(tokFields[0]);
                    sw05tmp30.Write(" ||| ");
                    sw05tmp30.Write(tokFields[4]);
                    sw05tmp30.Write('\n');
                    sw05tmp40.Write(tokFields[0]);
                    sw05tmp40.Write(" ||| ");
                    sw05tmp40.Write(tokFields[6]);
                    sw05tmp40.Write('\n');

                    ++keptCount;
                }
            }
            Console.WriteLine("Kept: " + keptCount);
            Console.WriteLine("Dropped: " + droppedCount);
            Console.WriteLine("Failed: " + failedCount);
        }


        static void remixAligned()
        {
            using (StreamReader srMain = ropen("04-zh-hu.txt"))
            using (StreamReader sr05tmp20A = ropen("05-tmp-zh-hu20.align"))
            using (StreamReader sr05tmp30A = ropen("05-tmp-zh-hu30.align"))
            using (StreamReader sr05tmp40A = ropen("05-tmp-zh-hu40.align"))
            using (StreamWriter sw05 = wopen("05-zh-hu.txt"))
            {
                while (true)
                {
                    string lnMain = srMain.ReadLine();
                    if (lnMain == null) break;
                    string[] parts = lnMain.Split('\t');
                    string ln20A = sr05tmp20A.ReadLine();
                    string ln30A = sr05tmp30A.ReadLine();
                    string ln40A = sr05tmp40A.ReadLine();
                    sw05.Write(parts[0]); // IDX
                    sw05.Write('\t');
                    sw05.Write(parts[1]); // ZH
                    sw05.Write('\t');
                    sw05.Write(parts[2]); // HU
                    sw05.Write('\t');
                    sw05.Write(parts[3]); // ZH-TOK
                    sw05.Write('\t');
                    sw05.Write(parts[4]); // ZH-TOK-MAP
                    sw05.Write('\t');
                    sw05.Write(parts[5]); // HU-TOK20
                    sw05.Write('\t');
                    sw05.Write(parts[6]); // HU-TOK20-MAP
                    sw05.Write('\t');
                    sw05.Write(ln20A); // ALIGN-20
                    sw05.Write('\t');
                    sw05.Write(parts[7]); // HU-TOK-30
                    sw05.Write('\t');
                    sw05.Write(parts[8]); // HU-TOK-30-MAP
                    sw05.Write('\t');
                    sw05.Write(ln30A); // ALIGN-30
                    sw05.Write('\t');
                    sw05.Write(parts[9]); //HU-TOK-40
                    sw05.Write('\t');
                    sw05.Write(parts[10]); // HU-TOK-40-MAP
                    sw05.Write('\t');
                    sw05.Write(ln40A); // ALIGN-40
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

        public static void Main(string[] args)
        {
            //filterA();
            //getTrad();
            //fixTrad();
            //histogram("02-zh-hu.txt", "02-lenhists.txt");
            //filterB();
            //splitForTok();
            //remixTokenized();
            remixAligned();

            //getSet("隧道");

            Console.ReadLine();
        }
    }
}
