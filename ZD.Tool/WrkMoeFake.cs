using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;

namespace ZD.Tool
{
    public class WrkMoeFake : IWorker
    {
        public void Work()
        {
            using (FileStream fsInS = new FileStream("moedict-heads-simp.txt", FileMode.Open, FileAccess.Read))
            using (StreamReader srs = new StreamReader(fsInS))
            using (FileStream fsInT = new FileStream("moedict-heads-trad.txt", FileMode.Open, FileAccess.Read))
            using (StreamReader srt = new StreamReader(fsInT))
            using (FileStream fsOut = new FileStream("moedict-fake.u8", FileMode.Create, FileAccess.ReadWrite))
            using (StreamWriter sw = new StreamWriter(fsOut))
            {
                while (true)
                {
                    string sline = srs.ReadLine();
                    string tline = srt.ReadLine();
                    if (sline == null || tline == null) break;
                    string res = tline + " " + sline + " [pin yin] /sense/";
                    sw.WriteLine(res);
                }
            }
        }

        public void Init()
        { }

        public void Dispose()
        { }

        public void Finish()
        {
        }
    }
}
