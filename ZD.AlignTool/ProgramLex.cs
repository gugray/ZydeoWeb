using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

namespace ZD.AlignTool
{
    public partial class Program
    {
        static void lexStats()
        {
            Dictionary<string, int> zhToRank = new Dictionary<string, int>();
            HashSet<string> ceHeads = new HashSet<string>();
            HashSet<string> chHeads = new HashSet<string>();
            HashSet<string> hasLL = new HashSet<string>();
            HashSet<string> hasMI = new HashSet<string>();
            HashSet<string> hasWV = new HashSet<string>();
            Dictionary<string, int> mtCount = new Dictionary<string, int>();

            string line;
            using (var srSubtlex = ropen("subtlex-ch.txt"))
            using (var srCe = ropen("cedict_ts.u8"))
            using (var srCh = ropen("chdict.u8"))
            using (var srLL = ropen("15-colloc-ll-filtered.txt"))
            using (var srMI = ropen("15-colloc-mi-filtered.txt"))
            using (var srWV = ropen("11-jiestem-dict-wvsims-filtered.txt"))
            using (var srMtCC = ropen("22-xl-char-char-filtered.txt"))
            using (var srMtCS = ropen("22-xl-char-stem-filtered.txt"))
            using (var srMtJC = ropen("22-xl-jie-char-filtered.txt"))
            {
                int i = 0;
                while ((line = srSubtlex.ReadLine()) != null) { ++i; zhToRank[line.Split('\t')[0]] = i; }
                while ((line = srCe.ReadLine()) != null)
                {
                    if (line.StartsWith("#") || line == "") continue;
                    ceHeads.Add(line.Split(' ')[1]);
                }
                while ((line = srCh.ReadLine()) != null)
                {
                    if (line.StartsWith("#") || line == "") continue;
                    chHeads.Add(line.Split(' ')[1]);
                }
                while ((line = srLL.ReadLine()) != null) hasLL.Add(line.Split('\t')[0]);
                while ((line = srMI.ReadLine()) != null) hasMI.Add(line.Split('\t')[0]);
                while ((line = srWV.ReadLine()) != null) hasWV.Add(line.Split('\t')[0]);
                while ((line = srMtCC.ReadLine()) != null)
                {
                    string zh = line.Split('\t')[0];
                    if (!mtCount.ContainsKey(zh)) mtCount[zh] = 1;
                    else ++mtCount[zh];
                }
                while ((line = srMtCS.ReadLine()) != null)
                {
                    string zh = line.Split('\t')[0];
                    if (!mtCount.ContainsKey(zh)) mtCount[zh] = 1;
                    else ++mtCount[zh];
                }
                while ((line = srMtJC.ReadLine()) != null)
                {
                    string zh = line.Split('\t')[0];
                    if (!mtCount.ContainsKey(zh)) mtCount[zh] = 1;
                    else ++mtCount[zh];
                }
            }
            HashSet<string> zhallSet = new HashSet<string>();
            foreach (var x in ceHeads) zhallSet.Add(x);
            foreach (var x in chHeads) zhallSet.Add(x);
            foreach (var x in hasLL) zhallSet.Add(x);
            foreach (var x in hasMI) zhallSet.Add(x);
            foreach (var x in hasWV) zhallSet.Add(x);
            List<string> zhall = new List<string>();
            foreach (var x in zhallSet)
            {
                zhall.Add(x);
                if (!zhToRank.ContainsKey(x)) zhToRank[x] = 99999;
            }
            zhall.Sort((x, y) => zhToRank[x].CompareTo(zhToRank[y]));
            using (var sw = wopen("30-lex-stats.txt"))
            {
                sw.Write("simp\trank\tcedict\tchdict\thas-ll\thas-mi\tany-clc\thas-wv\tmt-cnt\n");
                foreach (var zh in zhall)
                {
                    sw.Write(zh);
                    sw.Write('\t');
                    sw.Write(zhToRank[zh]);
                    sw.Write('\t');
                    sw.Write(ceHeads.Contains(zh) ? "yes" : "no");
                    sw.Write('\t');
                    sw.Write(chHeads.Contains(zh) ? "yes" : "no");
                    sw.Write('\t');
                    sw.Write(hasLL.Contains(zh) ? "yes" : "no");
                    sw.Write('\t');
                    sw.Write(hasMI.Contains(zh) ? "yes" : "no");
                    sw.Write('\t');
                    sw.Write((hasMI.Contains(zh) || hasLL.Contains(zh)) ? "yes" : "no");
                    sw.Write('\t');
                    sw.Write(hasWV.Contains(zh) ? "yes" : "no");
                    sw.Write('\t');
                    sw.Write(!mtCount.ContainsKey(zh) ? 0 : mtCount[zh]);
                    sw.Write('\n');
                }
            }
        }

        static Regex reEntry = new Regex(@"([^ ]+) ([^ ]+) ([^\]]+\]) (\/[^\n]+\/)$");

        static void getScope(int step, int stepOffset, int max)
        {
            string line;
            Dictionary<string, List<string>> ceSimpToHeads = new Dictionary<string, List<string>>();
            Dictionary<string, string> ceSimpToSenses = new Dictionary<string, string>();
            HashSet<string> chHeads = new HashSet<string>();
            List<string> simps = new List<string>();

            using (var srCe = ropen("cedict_ts.u8"))
            using (var srCh = ropen("../../Zydeo-DictTrans/_work_chdict_corpus/chdict.u8"))
            using (var srBase = ropen("31-lex-scope-base.txt"))
            {
                while ((line = srCe.ReadLine()) != null)
                {
                    if (line.StartsWith("#") || line == "") continue;
                    Match m = reEntry.Match(line);
                    string simp = m.Groups[2].Value;
                    if (!ceSimpToHeads.ContainsKey(simp)) ceSimpToHeads[simp] = new List<string>();
                    ceSimpToHeads[simp].Add(m.Groups[1].Value + " " + m.Groups[3].Value);
                    if (!ceSimpToSenses.ContainsKey(simp)) ceSimpToSenses[simp] = m.Groups[4].Value;
                    else ceSimpToSenses[simp] += m.Groups[4].Value;
                }
                while ((line = srCh.ReadLine()) != null)
                {
                    if (line.StartsWith("#") || line == "") continue;
                    chHeads.Add(line.Split(' ')[1]);
                }
                int i = 0;
                while ((line = srBase.ReadLine()) != null)
                {
                    if (i % step == stepOffset && simps.Count < max) simps.Add(line.Split('\t')[0]);
                    ++i;
                }
            }
            HashSet<string> onlyCeHeads = new HashSet<string>();
            foreach (var x in ceSimpToHeads.Keys) if (!chHeads.Contains(x)) onlyCeHeads.Add(x);
            HashSet<string> allSimps = new HashSet<string>();
            List<string> vars;
            int xtraVars = 0;
            int xtraSubs = 0;
            int xtraLongers = 0;
            int subsInCH = 0;
            using (var sw = wopen("32-selected-heads.txt"))
            {
                foreach (string simp in simps)
                {
                    allSimps.Add(simp);
                    vars = ceSimpToHeads[simp];
                    // All pronunciation / traditional variants
                    foreach (string var in vars)
                        sw.WriteLine(simp + " " + var);
                    xtraVars += vars.Count - 1;
                    // All CEDICT-only entries whose simp is substring of current
                    foreach (var x in onlyCeHeads)
                    {
                        if (x.Length < simp.Length && simp.IndexOf(x) != -1 && !allSimps.Contains(x))
                        {
                            allSimps.Add(x);
                            vars = ceSimpToHeads[x];
                            foreach (string var in vars)
                                sw.WriteLine(x + " " + var);
                            xtraSubs += vars.Count;
                        }
                    }
                    // All CEDICT-only entries that have current simp as substring
                    foreach (var x in onlyCeHeads)
                    {
                        if (x.Length > simp.Length && x.IndexOf(simp) != -1 && !allSimps.Contains(x))
                        {
                            allSimps.Add(x);
                            vars = ceSimpToHeads[x];
                            foreach (string var in vars)
                                sw.WriteLine(x + " " + var);
                            xtraLongers += vars.Count;
                        }
                    }
                    // Parts found in CHDICT
                    foreach (var x in chHeads)
                        if (x.Length < simp.Length && simp.IndexOf(x) != -1)
                            ++subsInCH;
                    sw.WriteLine();
                }
            }
            Console.WriteLine("Simplified words: " + simps.Count);
            Console.WriteLine("Trad/pron variants: " + xtraVars);
            Console.WriteLine("Part-of-word: " + xtraSubs);
            Console.WriteLine("Longer-words: " + xtraLongers);
            Console.WriteLine("Total: " + allSimps.Count);
            Console.WriteLine("Parts in CHDICT: " + subsInCH);
            using (var swEn = wopen("32-trans-en.txt"))
            using (var swZh = wopen("32-trans-zh.txt"))
            {
                foreach (var simp in allSimps)
                {
                    swZh.WriteLine(simp);
                    string[] senses = ceSimpToSenses[simp].Split('/');
                    foreach (var sense in senses)
                    {
                        if (sense.Trim() == "") continue;
                        swEn.WriteLine(sense.Trim());
                    }
                }
            }
        }

        public static void lMain(string[] args)
        {
            //lexStats();
            getScope(49, 1, 200);

            Console.ReadLine();
        }
    }
}
