using System;
using System.Text.RegularExpressions;
using System.IO;

namespace ZD.Tool
{
    public class WrkMoeEntries : IWorker
    {
        public void Work()
        {
            Regex reCode = new Regex(@"\{\[(.{4})\]\}");
            using (FileStream fsIn = new FileStream("moedict-entries.txt", FileMode.Open, FileAccess.Read))
            using (StreamReader sr = new StreamReader(fsIn))
            using (FileStream fsOut = new FileStream("moedict-heads-trad.txt", FileMode.Create, FileAccess.ReadWrite))
            using (StreamWriter sw = new StreamWriter(fsOut))
            {
                string line;
                sr.ReadLine();
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    string head = parts[1];
                    while (true)
                    {
                        Match m = reCode.Match(head);
                        if (!m.Success) break;
                        char c = (char)Convert.ToInt32(m.Groups[1].Value, 16);
                        head = head.Replace(m.Value, c.ToString());
                    }
                    sw.WriteLine(head);
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
