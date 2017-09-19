using System;
using System.Collections.Generic;
using System.IO;

namespace ZD.AlignTool
{
    class Histogram
    {
        public class LenVals
        {
            public int L_001_005;
            public List<double> R_001_005 = new List<double>();
            public List<double> PR_001_005 = new List<double>();
            public int L_006_020;
            public List<double> R_006_020 = new List<double>();
            public List<double> PR_006_020 = new List<double>();
            public int L_021_040;
            public List<double> R_021_040 = new List<double>();
            public List<double> PR_021_040 = new List<double>();
            public int L_041_100;
            public List<double> R_041_100 = new List<double>();
            public List<double> PR_041_100 = new List<double>();
            public int L_101_200;
            public List<double> R_101_200 = new List<double>();
            public List<double> PR_101_200 = new List<double>();
            public int L_201_300;
            public List<double> R_201_300 = new List<double>();
            public List<double> PR_201_300 = new List<double>();
            public int L_301_400;
            public List<double> R_301_400 = new List<double>();
            public List<double> PR_301_400 = new List<double>();
            public int L_401_X;
            public List<double> R_401_X = new List<double>();
            public List<double> PR_401_X = new List<double>();
        }

        private LenVals lvZh = new LenVals();
        private LenVals lvHu = new LenVals();

        public void File(int zhLen, int huLen, int zhPuncts)
        {
            double ratio = ((double)huLen) / zhLen;
            double pratio = ((double)zhPuncts) / zhLen;
            // File ZH length
            // Ratios go here
            if (zhLen < 6) { ++lvZh.L_001_005; lvZh.R_001_005.Add(ratio); lvZh.PR_001_005.Add(pratio); }
            else if (zhLen < 21) { ++lvZh.L_006_020; lvZh.R_006_020.Add(ratio); lvZh.PR_006_020.Add(pratio); }
            else if (zhLen < 41) { ++lvZh.L_021_040; lvZh.R_021_040.Add(ratio); lvZh.PR_021_040.Add(pratio); }
            else if (zhLen < 101) { ++lvZh.L_041_100; lvZh.R_041_100.Add(ratio); lvZh.PR_041_100.Add(pratio); }
            else if (zhLen < 201) { ++lvZh.L_101_200; lvZh.R_101_200.Add(ratio); lvZh.PR_101_200.Add(pratio); }
            else if (zhLen < 301) { ++lvZh.L_201_300; lvZh.R_201_300.Add(ratio); lvZh.PR_201_300.Add(pratio); }
            else if (zhLen < 401) { ++lvZh.L_301_400; lvZh.R_301_400.Add(ratio); lvZh.PR_301_400.Add(pratio); }
            else { ++lvZh.L_401_X; lvZh.R_401_X.Add(ratio); lvZh.PR_401_X.Add(pratio); }
            // File HU length
            if (huLen < 6) ++lvHu.L_001_005;
            else if (huLen < 21) ++lvHu.L_006_020;
            else if (huLen < 41) ++lvHu.L_021_040;
            else if (huLen < 101) ++lvHu.L_041_100;
            else if (huLen < 201) ++lvHu.L_101_200;
            else if (huLen < 301) ++lvHu.L_201_300;
            else if (huLen < 401) ++lvHu.L_301_400;
            else ++lvHu.L_401_X;
        }

        private void calc(List<double> ratios, out double avg, out double stddev, out double mean)
        {
            if (ratios.Count == 0)
            {
                avg = stddev = mean = 0;
                return;
            }
            ratios.Sort();
            double sum = 0;
            foreach (double d in ratios) sum += d;
            avg = sum / ratios.Count;
            double devsum = 0;
            foreach (double d in ratios) devsum += (avg - d) * (avg - d);
            stddev = Math.Sqrt(devsum / (ratios.Count - 1));
            mean = ratios[ratios.Count / 2];
        }

        public void Write(string fileName)
        {
            using (FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite))
            using (StreamWriter sw = new StreamWriter(fs))
            {
                sw.WriteLine("Category\t1-5\t6-20\t21-40\t41-100\t101-200\t201-300\t301-400\t400+");
                sw.WriteLine("HU-len\t" + lvHu.L_001_005 + "\t" + lvHu.L_006_020 + "\t" + lvHu.L_021_040 + "\t" + lvHu.L_041_100 + "\t" + lvHu.L_101_200 + "\t" + lvHu.L_201_300 + "\t" + lvHu.L_301_400 + "\t" + lvHu.L_401_X);
                sw.WriteLine("ZH-len\t" + lvZh.L_001_005 + "\t" + lvZh.L_006_020 + "\t" + lvZh.L_021_040 + "\t" + lvZh.L_041_100 + "\t" + lvZh.L_101_200 + "\t" + lvZh.L_201_300 + "\t" + lvZh.L_301_400 + "\t" + lvZh.L_401_X);

                double[] avg = new double[8];
                double[] stddev = new double[8];
                double[] mean = new double[8];

                calc(lvZh.R_001_005, out avg[0], out stddev[0], out mean[0]);
                calc(lvZh.R_006_020, out avg[1], out stddev[1], out mean[1]);
                calc(lvZh.R_021_040, out avg[2], out stddev[2], out mean[2]);
                calc(lvZh.R_041_100, out avg[3], out stddev[3], out mean[3]);
                calc(lvZh.R_101_200, out avg[4], out stddev[4], out mean[4]);
                calc(lvZh.R_201_300, out avg[5], out stddev[5], out mean[5]);
                calc(lvZh.R_301_400, out avg[6], out stddev[6], out mean[6]);
                calc(lvZh.R_401_X, out avg[7], out stddev[7], out mean[7]);
                sw.WriteLine("LR-avg\t" + avg[0].ToString("0.00") + "\t" + avg[1].ToString("0.00") + "\t" + avg[2].ToString("0.00") + "\t" + avg[3].ToString("0.00") + "\t" + avg[4].ToString("0.00") + "\t" + avg[5].ToString("0.00") + "\t" + avg[6].ToString("0.00") + "\t" + avg[7].ToString("0.00"));
                sw.WriteLine("LR-stddev\t" + stddev[0].ToString("0.00") + "\t" + stddev[1].ToString("0.00") + "\t" + stddev[2].ToString("0.00") + "\t" + stddev[3].ToString("0.00") + "\t" + stddev[4].ToString("0.00") + "\t" + stddev[5].ToString("0.00") + "\t" + stddev[6].ToString("0.00") + "\t" + stddev[7].ToString("0.00"));
                sw.WriteLine("LR-mean\t" + mean[0].ToString("0.00") + "\t" + mean[1].ToString("0.00") + "\t" + mean[2].ToString("0.00") + "\t" + mean[3].ToString("0.00") + "\t" + mean[4].ToString("0.00") + "\t" + mean[5].ToString("0.00") + "\t" + mean[6].ToString("0.00") + "\t" + mean[7].ToString("0.00"));

                calc(lvZh.PR_001_005, out avg[0], out stddev[0], out mean[0]);
                calc(lvZh.PR_006_020, out avg[1], out stddev[1], out mean[1]);
                calc(lvZh.PR_021_040, out avg[2], out stddev[2], out mean[2]);
                calc(lvZh.PR_041_100, out avg[3], out stddev[3], out mean[3]);
                calc(lvZh.PR_101_200, out avg[4], out stddev[4], out mean[4]);
                calc(lvZh.PR_201_300, out avg[5], out stddev[5], out mean[5]);
                calc(lvZh.PR_301_400, out avg[6], out stddev[6], out mean[6]);
                calc(lvZh.PR_401_X, out avg[7], out stddev[7], out mean[7]);
                sw.WriteLine("PR-avg\t" + avg[0].ToString("0.000") + "\t" + avg[1].ToString("0.000") + "\t" + avg[2].ToString("0.000") + "\t" + avg[3].ToString("0.000") + "\t" + avg[4].ToString("0.000") + "\t" + avg[5].ToString("0.000") + "\t" + avg[6].ToString("0.000") + "\t" + avg[7].ToString("0.000"));
                sw.WriteLine("PR-stddev\t" + stddev[0].ToString("0.000") + "\t" + stddev[1].ToString("0.000") + "\t" + stddev[2].ToString("0.000") + "\t" + stddev[3].ToString("0.000") + "\t" + stddev[4].ToString("0.000") + "\t" + stddev[5].ToString("0.000") + "\t" + stddev[6].ToString("0.000") + "\t" + stddev[7].ToString("0.000"));
                sw.WriteLine("PR-mean\t" + mean[0].ToString("0.000") + "\t" + mean[1].ToString("0.000") + "\t" + mean[2].ToString("0.000") + "\t" + mean[3].ToString("0.000") + "\t" + mean[4].ToString("0.000") + "\t" + mean[5].ToString("0.000") + "\t" + mean[6].ToString("0.000") + "\t" + mean[7].ToString("0.000"));
            }
        }
    }
}
