using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

namespace ZD.AlignTool
{
    public partial class Program
    {
        static Dictionary<string, float[]> zhVects = new Dictionary<string, float[]>();
        static Dictionary<string, float[]> huVects = new Dictionary<string, float[]>();

        static void loadVects(string vectFileName)
        {
            using (var sr = ropen(vectFileName))
            {
                string line = sr.ReadLine();
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split(' ');
                    float[] vect = new float[200];
                    for (int i = 1; i != parts.Length; ++i)
                        vect[i - 1] = float.Parse(parts[i]);
                    string word = parts[0];
                    if (word.StartsWith("zh_")) zhVects[word.Substring(3)] = vect;
                    else huVects[word.Substring(3)] = vect;
                }
            }
        }

        private static float calcSim(float[] a, float[] b, List<int> dims)
        {
            float prodSum = 0;
            float squareSumA = 0;
            float squareSumB = 0;
            for (int i = 0; i != a.Length; ++i)
            {
                if (dims != null && dims[i] == -1) continue;
                prodSum += a[i] * b[i];
                squareSumA += a[i] * a[i];
                squareSumB += b[i] * b[i];
            }
            return prodSum / (float)(Math.Sqrt(squareSumA) * Math.Sqrt(squareSumB));
        }

        private class SimPair
        {
            public string Zh;
            public string Hu;
            public float Sim;
        }

        static bool isHuBad(string hu, int minFreq = 10, bool nonWordsOk = false)
        {
            if (!huFreqs.ContainsKey(hu)) return true;
            if (huFreqs[hu] < minFreq) return true;
            if (!nonWordsOk && huNonWords.Contains(hu)) return true;
            return false;
        }

        static bool isZhBad(string zh, int minFreq = 20)
        {
            bool hasDigit = false;
            foreach (char c in zh) if (char.IsDigit(c)) hasDigit = true;
            if (hasDigit) return true;
            if (!zhFreqs.ContainsKey(zh)) return true;
            if (zhFreqs[zh] < minFreq) return true;
            return false;
        }

        static void getGoodPairs()
        {
            Console.WriteLine("Total ZH words: " + zhVects.Count);
            int count = 0;
            List<SimPair> pairs = new List<SimPair>();
            foreach (string zh in zhVects.Keys)
            {
                ++count;
                // Filter
                if (isZhBad(zh)) continue;

                float[] zhVect = zhVects[zh];
                foreach (string hu in huVects.Keys)
                {
                    // Filter
                    if (isHuBad(hu)) continue;

                    float[] huVect = huVects[hu];
                    float sim = calcSim(zhVect, huVect, null);
                    if (sim < 0.78) continue;
                    pairs.Add(new SimPair { Zh = zh, Hu = hu, Sim = sim });
                }
                if (count % 1000 == 0) Console.WriteLine(count.ToString() + " (" + pairs.Count + ")");
            }
            pairs.Sort((x, y) => y.Sim.CompareTo(x.Sim));
            using (StreamWriter sw = wopen("12-zhhusims.txt"))
            {
                foreach (var pair in pairs)
                {
                    sw.Write(pair.Sim.ToString("0.0000"));
                    sw.Write('\t');
                    sw.Write(pair.Zh);
                    sw.Write('\t');
                    sw.Write(pair.Hu);
                    sw.Write('\n');
                }
            }
        }

        private static Dictionary<string, int> zhFreqs = new Dictionary<string, int>();
        private static Dictionary<string, int> huFreqs = new Dictionary<string, int>();
        private static HashSet<string> huNonWords = new HashSet<string>();

        static void readFreqsStems()
        {
            using (var srZh = ropen("10-jiestem-zhfreqs.txt"))
            using (var srHu = ropen("10-jiestem-hufreqs.txt"))
            using (var srHuNon = ropen("04-tmp-hu-vocab-stemmed.txt"))
            {
                string line;
                while ((line = srZh.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    zhFreqs[parts[1]] = int.Parse(parts[0]);
                }
                while ((line = srHu.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    huFreqs[parts[1]] = int.Parse(parts[0]);
                }
                while ((line = srHuNon.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    if (parts.Length < 3) continue;
                    if (parts[1].EndsWith("?")) huNonWords.Add(parts[0].ToLower());
                }
            }
        }

        static void prunePairs()
        {
            Dictionary<string, List<string>> zhToHu = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> huToZh = new Dictionary<string, List<string>>();
            using (var sr = ropen("12-zhhusims.txt"))
            using (var sw = wopen("12-zhhusims-pruned.txt"))
            using (var sw2 = wopen("12-zhhusims-pruned-zh-hus.txt"))
            using (var sw3 = wopen("12-zhhusims-pruned-hu-zhs.txt"))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    float sim = float.Parse(parts[0]);
                    string zh = parts[1];
                    string hu = parts[2];

                    if (isHuBad(hu) || isZhBad(zh)) continue;

                    List<string> hulist = null;
                    if (!zhToHu.ContainsKey(zh))
                    {
                        hulist = new List<string>();
                        zhToHu[zh] = hulist;
                    }
                    else hulist = zhToHu[zh];
                    hulist.Add(hu);

                    List<string> zhlist = null;
                    if (!huToZh.ContainsKey(hu))
                    {
                        zhlist = new List<string>();
                        huToZh[hu] = zhlist;
                    }
                    else zhlist = huToZh[hu];
                    zhlist.Add(zh);

                    sw.Write(sim.ToString("0.0000"));
                    sw.Write('\t');
                    sw.Write(zh);
                    sw.Write('\t');
                    sw.Write(zhFreqs[zh].ToString());
                    sw.Write('\t');
                    sw.Write(hu);
                    sw.Write('\t');
                    sw.Write(huFreqs[hu].ToString());
                    sw.Write('\n');
                }
                List<string> zhs = new List<string>();
                foreach (var x in zhToHu) zhs.Add(x.Key);
                zhs.Sort((x, y) => zhToHu[y].Count.CompareTo(zhToHu[x].Count));
                foreach (string zh in zhs)
                {
                    sw2.Write(zh);
                    sw2.Write('\t');
                    sw2.Write(zhToHu[zh].Count.ToString());
                    foreach (string hu in zhToHu[zh])
                    {
                        sw2.Write('\t');
                        sw2.Write(hu);
                    }
                    sw2.Write('\n');
                }
                List<string> hus = new List<string>();
                foreach (var x in huToZh) hus.Add(x.Key);
                hus.Sort((x, y) => huToZh[y].Count.CompareTo(huToZh[x].Count));
                foreach (string hu in hus)
                {
                    sw3.Write(hu);
                    sw3.Write('\t');
                    sw3.Write(huToZh[hu].Count.ToString());
                    foreach (string zh in huToZh[hu])
                    {
                        sw3.Write('\t');
                        sw3.Write(zh);
                    }
                    sw3.Write('\n');
                }
            }
        }

        static float getAvgSim(List<string[]> pairs, List<int> dims)
        {
            float sum = 0;
            foreach (var pair in pairs)
                sum += calcSim(zhVects[pair[0]], huVects[pair[1]], dims);
            return sum / pairs.Count;
        }

        static void dimReduce()
        {
            List<string[]> pairs = new List<string[]>();
            using (var sr = ropen("12-zhhusims-pruned-hu-zhs.txt"))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    int count = int.Parse(parts[1]);
                    if (count > 3) continue;
                    string hu = parts[0];
                    for (int i = 2; i < parts.Length; ++i)
                    {
                        string[] pair = new string[2];
                        pair[0] = parts[i];
                        pair[1] = hu;
                        pairs.Add(pair);
                    }
                }
            }
            List<string[]> negativePairs = new List<string[]>();
            List<string> allHus = new List<string>();
            foreach (var x in huFreqs)
            {
                if (isHuBad(x.Key)) continue;
                allHus.Add(x.Key);
            }
            List<string> allZhs = new List<string>();
            foreach (var x in zhFreqs)
            {
                if (isZhBad(x.Key)) continue;
                allZhs.Add(x.Key);
            }
            Random rnd = new Random(0);
            for (int i = 0; i != 5 * pairs.Count; ++i)
            {
                string zh = allZhs[rnd.Next(allZhs.Count)];
                string hu = allHus[rnd.Next(allHus.Count)];
                negativePairs.Add(new string[] { zh, hu });
            }

            List<int> dims = new List<int>();
            for (int i = 0; i != 200; ++i) dims.Add(i);
            float avgSim = getAvgSim(pairs, dims);
            float avgNonSim = getAvgSim(negativePairs, dims);
            int dimCount = 200;
            Console.WriteLine(dimCount + " dimensions: " + avgSim.ToString("0.0000") + "; complement: " + avgNonSim.ToString("0.0000"));
            List<int> droppedDims = new List<int>();
            int iter = -2;
            using (StreamWriter sw = wopen("13-dimreduce.txt"))
            {
                while (true)
                {
                    ++iter;
                    if (iter > 0 && iter % 3 == 0)
                    {
                        ++dimCount;
                        int dimToRestore = droppedDims[0];
                        droppedDims.RemoveAt(0);
                        dims[dimToRestore] = dimToRestore;
                        Console.WriteLine("Re-added earliest dropped dimension: " + dimToRestore + "; Back to " + dimCount);
                        avgSim = getAvgSim(pairs, dims);
                        avgNonSim = getAvgSim(negativePairs, dims);
                    }
                    float newAvgSim = avgSim;
                    float newAvgNonSim = avgNonSim;
                    int bestDrop = -1;
                    float bestGain = 0;
                    for (int i = 0; i != dims.Count; ++i)
                    {
                        if (dims[i] == -1) continue;
                        dims[i] = -1;
                        float avg = getAvgSim(pairs, dims);
                        float avgNon = getAvgSim(negativePairs, dims);
                        dims[i] = i;
                        //if (avg < newAvgSim || avgNon > newAvgNonSim) continue;
                        float gain = avg - newAvgSim + newAvgNonSim - avgNon;
                        if (gain > bestGain && newAvgNonSim > 0)
                        {
                            newAvgSim = avg;
                            newAvgNonSim = avgNon;
                            bestGain = gain;
                            bestDrop = i;
                        }
                    }
                    if (bestDrop == -1) break;
                    --dimCount;
                    Console.WriteLine("Dropped dimension: " + bestDrop);
                    Console.WriteLine(dimCount + " dimensions: " + avgSim.ToString("0.0000") + "; complement: " + avgNonSim.ToString("0.0000"));
                    droppedDims.Add(bestDrop);
                    dims[bestDrop] = -1;
                    avgSim = newAvgSim;
                    avgNonSim = newAvgNonSim;

                    sw.Write(dimCount);
                    sw.Write('\t');
                    sw.Write(avgSim.ToString("0.0000"));
                    sw.Write('\t');
                    sw.Write(avgNonSim.ToString("0.0000"));
                    sw.Write('\t');
                    bool first = true;
                    for (int j = 0; j != dims.Count; ++j)
                    {
                        if (dims[j] == -1) continue;
                        if (!first) sw.Write(' ');
                        first = false;
                        sw.Write(j.ToString());
                    }
                    sw.Write('\n');
                }
            }
            Console.WriteLine(dimCount + " dimensions: " + avgSim.ToString("0.0000") + "; complement: " + avgNonSim.ToString("0.0000"));
        }

        static List<SimPair> getSims(string zh, List<int> dims)
        {
            List<SimPair> res = new List<SimPair>();
            if (!zhVects.ContainsKey(zh)) return res;
            var zhVect = zhVects[zh];
            foreach (string hu in huVects.Keys)
            {
                if (isHuBad(hu)) continue;
                var sim = calcSim(zhVect, huVects[hu], dims);
                res.Add(new SimPair { Zh = zh, Hu = hu, Sim = sim });
            }
            res.Sort((x, y) => y.Sim.CompareTo(x.Sim));
            List<SimPair> limited = new List<SimPair>();
            for (int i = 0; i < 20 && i < res.Count; ++i) limited.Add(res[i]);
            return limited;
        }

        static void testZhHu(string[] zhs)
        {
            List<int> dims = new List<int>();
            List<int> dimsAll = new List<int>();
            for (int i = 0; i != 200; ++i)
            {
                dimsAll.Add(i);
                dims.Add(-1);
            }
            using (var sr = ropen("13-dimreduce.txt"))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    int dimCount = int.Parse(parts[0]);
                    if (dimCount == 40)
                    {
                        string[] dimStrs = parts[3].Split(' ');
                        foreach (string s in dimStrs)
                        {
                            int dim = int.Parse(s);
                            dims[dim] = dim;
                        }
                    }
                }
            }
            using (var sw = wopen("_.txt"))
            {
                foreach (var zh in zhs)
                {
                    sw.WriteLine("Similar words for " + zh);
                    List<SimPair> simsAll = getSims(zh, dimsAll);
                    List<SimPair> sims = getSims(zh, dims);
                    for (int i = 0; i < 20 && i < sims.Count && i < simsAll.Count; ++i)
                    {
                        sw.Write(simsAll[i].Sim.ToString("0.0000"));
                        sw.Write('\t');
                        while (simsAll[i].Hu.Length < 20) simsAll[i].Hu += " ";
                        sw.Write(simsAll[i].Hu);
                        sw.Write('\t');
                        sw.Write(sims[i].Sim.ToString("0.0000"));
                        sw.Write('\t');
                        while (sims[i].Hu.Length < 20) sims[i].Hu += " ";
                        sw.Write(sims[i].Hu);
                        sw.Write('\n');
                    }
                    sw.Write('\n');
                }
            }
        }

        static Dictionary<string, Dictionary<string, int>> zhToHuCounts = new Dictionary<string, Dictionary<string, int>>();
        static int corpusSegCount = 0;

        static void countCollocs()
        {
            using (var sr = ropen("05-tmp-zh-hustem.txt"))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    ++corpusSegCount;
                    if (corpusSegCount % 100000 == 0) Console.WriteLine(corpusSegCount.ToString());
                    line = line.Replace(" ||| ", "\t");
                    string[] parts = line.Split('\t');
                    string[] zhs = parts[0].Split(' ');
                    string[] hus = parts[1].Split(' ');
                    foreach (string zh in zhs)
                    {
                        if (isZhBad(zh, 20)) continue;
                        Dictionary<string, int> huCounts;
                        if (zhToHuCounts.ContainsKey(zh)) huCounts = zhToHuCounts[zh];
                        else
                        {
                            huCounts = new Dictionary<string, int>();
                            zhToHuCounts[zh] = huCounts;
                        }
                        foreach (string hu in hus)
                        {
                            if (isHuBad(hu, 10, true)) continue;
                            if (huCounts.ContainsKey(hu)) ++huCounts[hu];
                            else huCounts[hu] = 1;
                        }
                    }
                }
            }
        }

        private class ScoreItem
        {
            public string Zh;
            public string Hu;
            public float ScoreMI;
            public float ScoreLL;
        }

        static float calcMI(int coCount, int zhCount, int huCount)
        {
            float scoreMI = (float)Math.Log((float)(corpusSegCount) / (zhCount * huCount) * coCount);
            return scoreMI;
        }

        static float calcLL(int coCount, int zhCount, int huCount)
        {
            float a = coCount;
            float b = zhCount - a;
            float c = huCount - a;
            float d = corpusSegCount - b - c + a;
            float scoreLL = (float)(2 *
                (a * Math.Log(a) + b * Math.Log(b) + c * Math.Log(c) + d * Math.Log(d) -
                (a + b) * Math.Log(a + b) - (a + c) * Math.Log(a + c) -
                (b + d) * Math.Log(b + d) - (c + d) * Math.Log(c + d) +
                (a + b + c + d) * Math.Log(a + b + c + d)));
            return scoreLL;
        }

        static void scoreCollocs()
        {
            float llLimit = 100;
            float miLimit = 7;
            List<ScoreItem> res = new List<ScoreItem>();
            foreach (string zh in zhToHuCounts.Keys)
            {
                int zhCount = zhFreqs[zh];
                foreach (var x in zhToHuCounts[zh])
                {
                    string hu = x.Key;
                    int huCount = huFreqs[hu];
                    ScoreItem si = new ScoreItem
                    {
                        Zh = zh,
                        Hu = hu,
                        ScoreLL = calcLL(x.Value, zhCount, huCount),
                        ScoreMI = calcMI(x.Value, zhCount, huCount),
                    };
                    if (si.ScoreLL < llLimit && si.ScoreMI < miLimit) continue;
                    if (float.IsNaN(si.ScoreLL) && float.IsNaN(si.ScoreMI)) continue;
                    res.Add(si);
                }
            }
            res.Sort((x, y) => y.ScoreLL.CompareTo(x.ScoreLL));
            using (StreamWriter sw = wopen("14-colloc-ll.txt"))
            {
                foreach (var si in res)
                {
                    if (float.IsNaN(si.ScoreLL) || si.ScoreLL < llLimit) continue;
                    sw.Write(si.ScoreLL.ToString("0"));
                    sw.Write('\t');
                    sw.Write(si.Zh);
                    sw.Write('\t');
                    sw.Write(zhFreqs[si.Zh]);
                    sw.Write('\t');
                    sw.Write(si.Hu);
                    sw.Write('\t');
                    sw.Write(huNonWords.Contains(si.Hu) ? "non" : "real");
                    sw.Write('\t');
                    sw.Write(huFreqs[si.Hu]);
                    sw.Write('\n');
                }
            }
            res.Sort((x, y) => y.ScoreMI.CompareTo(x.ScoreMI));
            using (StreamWriter sw = wopen("14-colloc-mi.txt"))
            {
                foreach (var si in res)
                {
                    if (float.IsNaN(si.ScoreMI) || si.ScoreMI < miLimit) continue;
                    sw.Write(si.ScoreMI.ToString("0.000"));
                    sw.Write('\t');
                    sw.Write(si.Zh);
                    sw.Write('\t');
                    sw.Write(zhFreqs[si.Zh]);
                    sw.Write('\t');
                    sw.Write(si.Hu);
                    sw.Write('\t');
                    sw.Write(huNonWords.Contains(si.Hu) ? "non" : "real");
                    sw.Write('\t');
                    sw.Write(huFreqs[si.Hu]);
                    sw.Write('\n');
                }
            }
        }

        static void readDict(string fn, Dictionary<string, string> dict, List<string> simps, HashSet<string> simpSet)
        {
            string line;
            using (var sr = ropen(fn))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    if (line == "" || line.StartsWith("#")) continue;
                    int ix1 = line.IndexOf(' ');
                    int ix2 = line.IndexOf(' ', ix1 + 1);
                    int ix3 = line.IndexOf(' ', ix2 + 1);
                    string simp = line.Substring(ix1 + 1, ix2 - ix1 - 1);
                    string entry = line.Substring(0, ix1) + line.Substring(ix2);
                    if (dict.ContainsKey(simp)) dict[simp] += " @@ " + entry;
                    else
                    {
                        dict[simp] = entry;
                        if (!simpSet.Contains(simp))
                        {
                            simps.Add(simp);
                            simpSet.Add(simp);
                        }
                    }
                }
            }
        }

        static void getDictBests(string fnOut, int simCount, int minHuFreq)
        {
            Dictionary<string, string> simpToCE = new Dictionary<string, string>();
            Dictionary<string, string> simpToCH = new Dictionary<string, string>();
            List<string> simps = new List<string>();
            HashSet<string> simpSet = new HashSet<string>();
            readDict("cedict_ts.u8", simpToCE, simps, simpSet);
            readDict("chdict.u8", simpToCH, simps, simpSet);

            // Prune HU vectors - for speed
            List<string> huToRem = new List<string>();
            foreach (var x in huVects)
                if (!huFreqs.ContainsKey(x.Key) || huFreqs[x.Key] < minHuFreq)
                    huToRem.Add(x.Key);
            foreach (string hu in huToRem) huVects.Remove(hu);

            StringBuilder sb = new StringBuilder();
            List<SimPair> pairs = new List<SimPair>();
            int count = 0;
            Console.Write("Working: 0");
            using (var sw = wopen(fnOut))
            {
                foreach (string simp in simps)
                {
                    ++count;
                    if (count % 100 == 0) Console.Write("\rWorking: " + count.ToString());

                    sb.Clear();
                    pairs.Clear();
                    sb.Append(simp);
                    sb.Append('\t');
                    if (simpToCE.ContainsKey(simp)) sb.Append(simpToCE[simp]);
                    sb.Append('\t');
                    if (simpToCH.ContainsKey(simp)) sb.Append(simpToCH[simp]);
                    if (!zhVects.ContainsKey(simp))
                    {
                        sb.Insert(0, "0.000\t");
                        for (int i = 0; i != simCount; ++i) sb.Append('\t');
                    }
                    else
                    {
                        float[] zhVect = zhVects[simp];
                        foreach (var x in huVects)
                        {
                            if (!huFreqs.ContainsKey(x.Key)) continue;
                            if (huFreqs[x.Key] < minHuFreq) continue;
                            SimPair sp = new SimPair
                            {
                                Zh = simp,
                                Hu = x.Key,
                                Sim = calcSim(zhVect, x.Value, null)
                            };
                            pairs.Add(sp);
                        }
                        pairs.Sort((x, y) => y.Sim.CompareTo(x.Sim));
                        for (int i = 0; i != simCount; ++i)
                        {
                            sb.Append('\t');
                            sb.Append(huFreqs[pairs[i].Hu].ToString());
                            sb.Append('/');
                            sb.Append(pairs[i].Hu);
                            sb.Append('/');
                            sb.Append(pairs[i].Sim.ToString("0.000"));
                        }
                        sb.Insert(0, pairs[0].Sim.ToString("0.000") + "\t");
                    }
                    sw.WriteLine(sb.ToString());
                }
            }
            Console.WriteLine();
        }

        static void analyzeHints(string fn, int hintLimit)
        {
            string line;
            Dictionary<string, int> huCounts = new Dictionary<string, int>();
            int hintedSimps = 0;
            using (var sr = ropen(fn + ".txt"))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    if (float.Parse(parts[0]) == 0) continue;
                    ++hintedSimps;
                    List<string> fullHints = new List<string>();
                    for (int i = 4; i < parts.Length; ++i)
                    {
                        if (parts[i] == "") continue;
                        fullHints.Add(parts[i]);
                    }
                    foreach (string fh in fullHints)
                    {
                        string[] hintParts = fh.Split('/');
                        string word = hintParts[1];
                        if (huCounts.ContainsKey(word)) ++huCounts[word];
                        else huCounts[word] = 1;
                    }
                }
            }
            Console.WriteLine("Headwords with hints: " + hintedSimps);
            List<string> hints = new List<string>();
            foreach (var x in huCounts) hints.Add(x.Key);
            hints.Sort((x, y) => huCounts[y].CompareTo(huCounts[x]));
            using (var sw = wopen(fn + "-hintcounts.txt"))
            {
                foreach (string hint in hints)
                {
                    sw.Write(huCounts[hint].ToString());
                    sw.Write('\t');
                    sw.Write(hint);
                    sw.Write('\n');
                }
            }
            int keptHintedSimps = 0;
            using (var sr = ropen(fn + ".txt"))
            using (var sw = wopen(fn + "-filtered.txt"))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    if (float.Parse(parts[0]) == 0) continue;
                    List<string> fullHints = new List<string>();
                    for (int i = 4; i < parts.Length; ++i)
                    {
                        if (parts[i] == "") continue;
                        fullHints.Add(parts[i]);
                    }
                    List<string> keptHints = new List<string>();
                    float bestScore = 0;
                    foreach (string fh in fullHints)
                    {
                        string[] hintParts = fh.Split('/');
                        string word = hintParts[1];
                        if (huCounts[word] > hintLimit) continue;
                        keptHints.Add(fh);
                        float score = float.Parse(hintParts[2]);
                        if (score > bestScore) bestScore = score;
                    }
                    if (keptHints.Count == 0) continue;
                    ++keptHintedSimps;
                    //sw.Write(bestScore.ToString("0.000"));
                    //sw.Write('\t');
                    sw.Write(parts[1]);
                    //sw.Write('\t');
                    //sw.Write(parts[2]);
                    //sw.Write('\t');
                    //sw.Write(parts[3]);
                    foreach (string kh in keptHints)
                    {
                        sw.Write('\t');
                        //sw.Write(kh);
                        string[] khparts = kh.Split('/');
                        sw.Write(khparts[2]);
                        sw.Write('\t');
                        sw.Write(khparts[1]);
                    }
                    sw.Write('\n');
                }
            }
            Console.WriteLine("Kept: " + keptHintedSimps);
        }

        static void getForDictTrans()
        {
            Dictionary<string, string> simpToCE = new Dictionary<string, string>();
            List<string> simps = new List<string>();
            HashSet<string> simpSet = new HashSet<string>();
            readDict("cedict_ts.u8", simpToCE, simps, simpSet);

            using (var swHeads = wopen("20-zh-heads.txt"))
            using (var swChar = wopen("20-zh-char.txt"))
            {
                foreach (string simp in simps)
                {
                    bool skip = false;
                    foreach (char c in simp)
                    {
                        if (c >= '0' && c <= '9') skip = true;
                        if (c >= 'a' && c <= 'z') skip = true;
                        if (c >= 'A' && c <= 'Z') skip = true;
                        if (char.IsPunctuation(c)) continue;
                        if (char.IsWhiteSpace(c)) continue;
                    }
                    if (skip) continue;
                    swHeads.WriteLine(simp);
                    for (int i = 0; i != simp.Length; ++i)
                    {
                        if (i > 0) swChar.Write(' ');
                        swChar.Write(simp[i]);
                    }
                    swChar.Write('\n');
                }
            }
        }

        static Regex reSent = new Regex(@"[^\]]+\] SENT[^\:]+\:([^\n]+)");
        static Regex reTrans = new Regex(@"[^\]]+\] \[([^\]]+)\] ([^\n]+)");
        static StringBuilder sb = new StringBuilder();

        static string prunePunct(string str)
        {
            sb.Clear();
            foreach (char c in str)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                }
                else if (char.IsPunctuation(c)) continue;
                else if (char.IsDigit(c)) return "";
                else sb.Append(c);
            }
            string res = sb.ToString();
            if (res.StartsWith("a ")) res = res.Substring(2);
            if (res.StartsWith("az ")) res = res.Substring(3);
            return res;
        }

        static void parseTransLog(string fnLog, string fnClear, string fnFreq)
        {
            Dictionary<string, int> freqTrgs = new Dictionary<string, int>();
            Dictionary<string, int> freqWords = new Dictionary<string, int>();
            string line;
            int count = 0;
            using (var sr = ropen(fnLog))
            using (var swClear = wopen(fnClear))
            using (var swFreqT = wopen(fnFreq))
            {
                List<KeyValuePair<string, float>> currs = new List<KeyValuePair<string, float>>();
                string currZh = null;
                Match m = null;
                while ((line = sr.ReadLine()) != null)
                {
                    m = reTrans.Match(line);
                    if (m.Success)
                    {
                        float score = float.Parse(m.Groups[1].Value);
                        string clear = m.Groups[2].Value.Replace("￭ ", "").Trim();
                        clear = prunePunct(clear);
                        if (clear == "") continue;
                        bool seen = false;
                        foreach (var cc in currs) if (cc.Key == clear) seen = true;
                        if (seen) continue;
                        currs.Add(new KeyValuePair<string, float>(clear, score));
                        continue;
                    }
                    m = reSent.Match(line);
                    if (m.Success)
                    {
                        if (currZh != null && currs.Count > 0)
                        {
                            swClear.Write(currZh);
                            foreach (var x in currs)
                            {
                                swClear.Write('\t');
                                swClear.Write(x.Value.ToString("0.00"));
                                swClear.Write('\t');
                                swClear.Write(x.Key);
                                if (freqTrgs.ContainsKey(x.Key)) ++freqTrgs[x.Key];
                                else freqTrgs[x.Key] = 1;
                            }
                            swClear.Write('\n');
                        }
                        currZh = m.Groups[1].Value;
                        currZh = currZh.Replace(" ", "");
                        currZh = currZh.Replace("\t", "");
                        currs.Clear();
                        ++count;
                        if (count % 5000 == 0) Console.Write("\r" + count.ToString());
                        continue;
                    }
                }
                swClear.Write(currZh);
                foreach (var x in currs)
                {
                    swClear.Write('\t');
                    swClear.Write(x.Value.ToString("0.00"));
                    swClear.Write('\t');
                    swClear.Write(x.Key);
                    if (freqTrgs.ContainsKey(x.Key)) ++freqTrgs[x.Key];
                    else freqTrgs[x.Key] = 1;
                }
                swClear.Write('\n');
                List<string> trgs = new List<string>();
                foreach (var x in freqTrgs)
                {
                    trgs.Add(x.Key);
                    string[] toks = x.Key.Split(' ');
                    foreach (string tok in toks)
                    {
                        if (freqWords.ContainsKey(tok)) freqWords[tok] += x.Value;
                        else freqWords[tok] = x.Value;
                    }
                }
                trgs.Sort((x, y) => freqTrgs[y].CompareTo(freqTrgs[x]));
                foreach (string str in trgs)
                {
                    swFreqT.Write(freqTrgs[str].ToString());
                    swFreqT.Write('\t');
                    swFreqT.Write(str);
                    swFreqT.Write('\n');
                }
            }
            Console.WriteLine();
        }

        static void filterClear(int limit, string fnClear, string fnFreqTrgs, string fnFiltered, string fnDropped)
        {
            string line;
            HashSet<string> tooFrequent = new HashSet<string>();
            int totalVariants = 0;
            int keptCount = 0;
            using (var srFreq = ropen(fnFreqTrgs))
            using (var srClear = ropen(fnClear))
            using (var swFiltered = wopen(fnFiltered))
            using (var swDropped = wopen(fnDropped))
            {
                while ((line = srFreq.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    int freq = int.Parse(parts[0]);
                    if (freq > limit) tooFrequent.Add(parts[1]);
                    else break;
                }
                List<string> kepts = new List<string>();
                while ((line = srClear.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    kepts.Clear();
                    // Don't keep anything if *first* item is too frequent
                    if (!tooFrequent.Contains(parts[2]))
                    {
                        for (int i = 1; i < parts.Length; i += 2)
                        {
                            // Keep rest, removing frequent ones
                            if (tooFrequent.Contains(parts[i + 1])) continue;
                            if (parts[i + 1].Contains("<unk>")) continue;
                            kepts.Add(parts[i]);
                            kepts.Add(parts[i + 1]);
                        }
                    }
                    if (kepts.Count == 0) swDropped.WriteLine(line);
                    else
                    {
                        ++keptCount;
                        totalVariants += kepts.Count / 2;
                        swFiltered.Write(parts[0]);
                        foreach (string str in kepts)
                        {
                            swFiltered.Write('\t');
                            swFiltered.Write(str);
                        }
                        swFiltered.Write('\n');
                    }
                }
            }
            Console.WriteLine("Kept headwords: " + keptCount);
            Console.WriteLine("Average hints: " + ((float)totalVariants / keptCount).ToString("0.00"));
        }

        static void getCollocCounts(string fnColloc, string fnFreqZh, string fnFreqHu)
        {
            string line;
            Dictionary<string, int> zhFreqs = new Dictionary<string, int>();
            Dictionary<string, int> huFreqs = new Dictionary<string, int>();
            using (var sr = ropen(fnColloc))
            using (var swZh = wopen(fnFreqZh))
            using (var swHu = wopen(fnFreqHu))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    string zh = parts[1];
                    string hu = parts[3];
                    if (!zhFreqs.ContainsKey(zh)) zhFreqs[zh] = 1;
                    else ++zhFreqs[zh];
                    if (!huFreqs.ContainsKey(hu)) huFreqs[hu] = 1;
                    else ++huFreqs[hu];
                }
                List<string> zhs = new List<string>();
                foreach (var x in zhFreqs) zhs.Add(x.Key);
                zhs.Sort((x, y) => zhFreqs[y].CompareTo(zhFreqs[x]));
                foreach (var x in zhs)
                {
                    swZh.Write(zhFreqs[x].ToString());
                    swZh.Write('\t');
                    swZh.Write(x);
                    swZh.Write('\n');
                }
                List<string> hus = new List<string>();
                foreach (var x in huFreqs) hus.Add(x.Key);
                hus.Sort((x, y) => huFreqs[y].CompareTo(huFreqs[x]));
                foreach (var x in hus)
                {
                    swHu.Write(huFreqs[x].ToString());
                    swHu.Write('\t');
                    swHu.Write(x);
                    swHu.Write('\n');
                }
            }
        }

        static void getBestCollocs(string fnColloc, string fnFreqZh, string fnFreqHu, int zhLimit, int huLimit, string fnOut)
        {
            string line;
            HashSet<string> zhs = new HashSet<string>();
            HashSet<string> hus = new HashSet<string>();
            using (var sr = ropen(fnFreqZh))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    if (int.Parse(parts[0]) > zhLimit) continue;
                    zhs.Add(parts[1]);
                }
            }
            using (var sr = ropen(fnFreqHu))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    if (int.Parse(parts[0]) > huLimit) continue;
                    hus.Add(parts[1]);
                }
            }
            Dictionary<string, List<KeyValuePair<string, float>>> zhToHu = new Dictionary<string, List<KeyValuePair<string, float>>>();
            using (var sr = ropen(fnColloc))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    string zh = parts[1];
                    string hu = parts[3];
                    if (!zhs.Contains(zh)) continue;
                    if (!hus.Contains(hu)) continue;
                    float score = float.Parse(parts[0]);
                    if (!zhToHu.ContainsKey(zh)) zhToHu[zh] = new List<KeyValuePair<string, float>>();
                    zhToHu[zh].Add(new KeyValuePair<string, float>(hu, score));
                }
            }
            using (var sw = wopen(fnOut))
            {
                foreach (var x in zhToHu)
                {
                    sw.Write(x.Key);
                    foreach (var y in x.Value)
                    {
                        sw.Write('\t');
                        sw.Write(y.Value.ToString());
                        sw.Write('\t');
                        sw.Write(y.Key);
                    }
                    sw.Write('\n');
                }
            }
        }

        static void collocDictCompare()
        {
            string line;
            Dictionary<string, string> simpToCE = new Dictionary<string, string>();
            Dictionary<string, string> simpToCH = new Dictionary<string, string>();
            List<string> simps = new List<string>();
            HashSet<string> simpSet = new HashSet<string>();
            readDict("cedict_ts.u8", simpToCE, simps, simpSet);
            readDict("chdict.u8", simpToCH, simps, simpSet);

            List<string> llInDict = new List<string>();
            List<string> llInCH = new List<string>();
            List<string> llNotInDict = new List<string>();
            List<string> miInDict = new List<string>();
            List<string> miInCH = new List<string>();
            List<string> miNotInDict = new List<string>();
            using (var sr = ropen("15-colloc-ll-filtered.txt"))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    string zh = line.Split('\t')[0];
                    if (simpToCE.ContainsKey(zh)) llInDict.Add(zh);
                    else llNotInDict.Add(zh);
                    if (simpToCH.ContainsKey(zh)) llInCH.Add(zh);
                }
            }
            using (var sr = ropen("15-colloc-mi-filtered.txt"))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    string zh = line.Split('\t')[0];
                    if (simpToCE.ContainsKey(zh)) miInDict.Add(zh);
                    else miNotInDict.Add(zh);
                    if (simpToCH.ContainsKey(zh)) miInCH.Add(zh);
                }
            }
            using (var sw = wopen("15-colloc-vs-cedict.txt"))
            {
                sw.WriteLine("ll-in\tll-out\tll-ch\tmi-in\tmi-out\tmi-ch");
                int count = 0;
                if (llInDict.Count > count) count = llInDict.Count;
                if (llNotInDict.Count > count) count = llNotInDict.Count;
                if (miInDict.Count > count) count = miInDict.Count;
                if (miNotInDict.Count > count) count = miNotInDict.Count;
                for (int i = 0; i < count; ++i)
                {
                    if (i < llInDict.Count) sw.Write(llInDict[i]);
                    sw.Write('\t');
                    if (i < llNotInDict.Count) sw.Write(llNotInDict[i]);
                    sw.Write('\t');
                    if (i < llInCH.Count) sw.Write(llInCH[i]);
                    sw.Write('\t');
                    if (i < miInDict.Count) sw.Write(miInDict[i]);
                    sw.Write('\t');
                    if (i < miNotInDict.Count) sw.Write(miNotInDict[i]);
                    sw.Write('\t');
                    if (i < miInCH.Count) sw.Write(miInCH[i]);
                    sw.Write('\n');
                }
            }
        }

        static void getWVInvRank(string fnOut, int simCount, int minHuFreq, int minZhFreq)
        {
            // Prune HU and ZH vectors - minimum frequency
            List<string> huToRem = new List<string>();
            foreach (var x in huVects)
                if (!huFreqs.ContainsKey(x.Key) || huFreqs[x.Key] < minHuFreq)
                    huToRem.Add(x.Key);
            foreach (string hu in huToRem) huVects.Remove(hu);
            List<string> zhToRem = new List<string>();
            foreach (var x in zhVects)
                if (!zhFreqs.ContainsKey(x.Key) || zhFreqs[x.Key] < minZhFreq)
                    zhToRem.Add(x.Key);
            foreach (string zh in zhToRem) zhVects.Remove(zh);

            // For every HU vector, find nearest N ZH vectors
            Dictionary<string, SimPair[]> huToZhs = new Dictionary<string, SimPair[]>();
            List<SimPair> nearests = new List<SimPair>();
            int huCount = 0;
            foreach (var x in huVects)
            {
                nearests.Clear();
                foreach (var y in zhVects)
                {
                    SimPair sp = new SimPair
                    {
                        Hu = x.Key,
                        Zh = y.Key,
                        Sim = calcSim(x.Value, y.Value, null),
                    };
                    nearests.Add(sp);
                }
                nearests.Sort((a, b) => b.Sim.CompareTo(a.Sim));
                SimPair[] ranked = new SimPair[simCount];
                for (int i = 0; i != simCount; ++i) ranked[i] = nearests[i];
                huToZhs[x.Key] = ranked;
                if (huCount % 100 == 0) Console.Write("\r" + huCount + " / " + huVects.Count);
                ++huCount;
                //if (huCount == 200) break; // DBG
            }
            Console.WriteLine();
            // Now, for every ZH, find that HU for it which it ranks best
            Dictionary<string, List<RankedSim>> zhToRanked = new Dictionary<string, List<RankedSim>>();
            foreach (var x in huToZhs)
            {
                for (int i = 0; i != x.Value.Length; ++i)
                {
                    SimPair sp = x.Value[i];
                    List<RankedSim> ranked;
                    if (!zhToRanked.ContainsKey(sp.Zh)) { ranked = new List<RankedSim>(); zhToRanked[sp.Zh] = ranked; }
                    else ranked = zhToRanked[sp.Zh];
                    ranked.Add(new RankedSim { SP = sp, Rank = i });
                }
            }
            // Results: for each ZH, top-ranked HU words
            using (var sw = wopen(fnOut))
            {
                foreach (var x in zhToRanked)
                {
                    // Sort each ranked list
                    x.Value.Sort((a, b) => a.Rank.CompareTo(b.Rank));
                    // Write result
                    sw.Write(x.Key);
                    for (int i = 0; i < simCount && i < x.Value.Count; ++i)
                    {
                        RankedSim rs = x.Value[i];
                        sw.Write('\t');
                        sw.Write(rs.Rank.ToString() + " " + rs.SP.Sim.ToString("0.00") + " " + rs.SP.Hu);
                    }
                    sw.WriteLine();
                }
            }
        }

        class RankedSim
        {
            public SimPair SP;
            public int Rank;
        }


        public static void Main(string[] args)
        {
            // Word embedding vectors
            loadVects("10-jiestem-wv.txt");
            readFreqsStems();
            //getDictBests("11-jiestem-dict-wvsims.txt", 40, 3);
            //analyzeHints("11-jiestem-dict-wvsims", 100);
            getWVInvRank("11-wvsims-invrank.txt", 10, 3, 5);

            //getForDictTrans();
            //--
            //parseTransLog("20-zh-char-xlog.txt", "21-xl-char-char-clear.txt", "21-xl-char-char-freqs.txt");
            //filterClear(200, "21-xl-char-char-clear.txt", "21-xl-char-char-freqs.txt", "22-xl-char-char-filtered.txt", "22-xl-char-char-dropped.txt");
            //--
            //parseTransLog("20-zh-char-stem-xlog.txt", "21-xl-char-stem-clear.txt", "21-xl-char-stem-freqs.txt");
            //filterClear(1000, "21-xl-char-stem-clear.txt", "21-xl-char-stem-freqs.txt", "22-xl-char-stem-filtered.txt", "22-xl-char-stem-dropped.txt");
            //--
            //parseTransLog("20-zh-jie-xlog.txt", "21-xl-jie-char-clear.txt", "21-xl-jie-char-freqs.txt");
            //filterClear(100, "21-xl-jie-char-clear.txt", "21-xl-jie-char-freqs.txt", "22-xl-jie-char-filtered.txt", "22-xl-jie-char-dropped.txt");


            // Collocation extraction
            //countCollocs(); // Needs readFreqsStems()
            //scoreCollocs();
            //getCollocCounts("14-colloc-ll.txt", "14-colloc-ll-freqs-zh.txt", "14-colloc-ll-freqs-hu.txt");
            //getCollocCounts("14-colloc-mi.txt", "14-colloc-mi-freqs-zh.txt", "14-colloc-mi-freqs-hu.txt");
            //getBestCollocs("14-colloc-ll.txt", "14-colloc-ll-freqs-zh.txt", "14-colloc-ll-freqs-hu.txt", 40, 200, "15-colloc-ll-filtered.txt");
            //getBestCollocs("14-colloc-mi.txt", "14-colloc-mi-freqs-zh.txt", "14-colloc-mi-freqs-hu.txt", 50, 300, "15-colloc-mi-filtered.txt");
            //collocDictCompare();

            // NOT-REAL - EXPERIMENTAL - W2V
            //getGoodPairs(); // LONG, BRUTE FORCE N*M. Needs loadVects()
            //prunePairs();
            //dimReduce(); // Needs loadVects() and readFreqsStems()
            //testZhHu(new string[] { "羽毛" });

            Console.ReadLine();
        }
    }
}
