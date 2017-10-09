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
                    foreach (string zh in parts)
                    {
                        if (isZhBad(zh, 10)) continue;
                        Dictionary<string, int> huCounts;
                        if (zhToHuCounts.ContainsKey(zh)) huCounts = zhToHuCounts[zh];
                        else
                        {
                            huCounts = new Dictionary<string, int>();
                            zhToHuCounts[zh] = huCounts;
                        }
                        foreach (string hu in hus)
                        {
                            if (isHuBad(hu, 5, true)) continue;
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
                    res.Add(si);
                }
            }
            res.Sort((x, y) => y.ScoreLL.CompareTo(x.ScoreLL));
            using (StreamWriter sw = wopen("14-colloc-ll.txt"))
            {
                foreach (var si in res)
                {
                    sw.Write(si.ScoreLL.ToString("0.0000"));
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
                    sw.Write(si.ScoreMI.ToString("0.0000"));
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
                            //if (!huFreqs.ContainsKey(x.Key)) continue;
                            //if (huFreqs[x.Key] < minHuFreq) continue;
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
                    sw.Write(bestScore.ToString("0.000"));
                    sw.Write('\t');
                    sw.Write(parts[1]);
                    sw.Write('\t');
                    sw.Write(parts[2]);
                    sw.Write('\t');
                    sw.Write(parts[3]);
                    foreach (string kh in keptHints)
                    {
                        sw.Write('\t');
                        sw.Write(kh);
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

        public static void xMain(string[] args)
        {
            // Word embedding vectors
            //loadVects("10-jiestem-wv.txt");
            //readFreqsStems();
            //getDictBests("11-jiestem-dict-wvsims.txt", 40, 3);

            //analyzeHints("11-jiestem-dict-wvsims", 100);

            getForDictTrans();


            //getGoodPairs(); // LONG, BRUTE FORCE N*M. Needs loadVects()
            //prunePairs();
            //dimReduce(); // Needs loadVects() and readFreqsStems()
            //testZhHu(new string[] { "羽毛" });

            // Collocation extraction
            //countCollocs(); // Needs readFreqsStems()
            //scoreCollocs();

            Console.ReadLine();
        }
    }
}
