using System;
using System.Text;
using System.Collections.Generic;
using System.IO;

using ZD.Common;
using ZD.LangUtils;

namespace ZD.Tool
{
    public class WrkChPrepare : IWorker
    {
        private class ResItem
        {
            public int ID;
            public string Lines;
        }

        private List<ResItem> items = new List<ResItem>();

        public void Work()
        {
            Random rnd = new Random(0);
            CedictParser parser = new CedictParser();
            HashSet<int> idSet = new HashSet<int>();
            StringBuilder sb = new StringBuilder();
            HashSet<char> simpChars = new HashSet<char>();

            using (FileStream fsIn = new FileStream("chdict.u8", FileMode.Open, FileAccess.Read))
            using (StreamReader sr = new StreamReader(fsIn))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.StartsWith("#")) continue;

                    int ix1 = line.IndexOf(" [");
                    int ix2 = line.IndexOf("] /");
                    line = line.Substring(0, ix1) + " [" + line.Substring(ix1 + 2, ix2 - ix1).ToLower() + line.Substring(ix2 + 2);

                    CedictEntry entry = parser.ParseEntry(line, 0, null);
                    if (entry == null) continue;
                    if (entry.ChSimpl.Length > 16) continue;

                    int id = rnd.Next();
                    while (idSet.Contains(id)) id = rnd.Next();
                    idSet.Add(id);
                    string strId = EntryId.IdToString(id);

                    sb.Clear();
                    // Line with ID
                    sb.AppendLine("# ID-" + strId);
                    // First version metainfo
                    string statStr = "Stat-Verif";
                    sb.AppendLine("# Ver 2017-05-02T22:41:05Z gabor " + statStr + " 001>CHDICT törzsanyag");
                    // The entry itself
                    sb.AppendLine(CedictWriter.Write(entry));

                    foreach (char c in entry.ChSimpl) simpChars.Add(c);

                    items.Add(new ResItem { ID = id, Lines = sb.ToString() });
                }
            }
        }

        public void Init()
        { }

        public void Dispose()
        { }

        public void Finish()
        {
            items.Sort((x, y) => x.ID.CompareTo(y.ID));
            using (FileStream fsOut = new FileStream("chdict.txt", FileMode.Create, FileAccess.ReadWrite))
            using (StreamWriter sw = new StreamWriter(fsOut))
            {
                foreach (var x in items)
                {
                    sw.Write(x.Lines);
                    sw.WriteLine();
                }
            }
        }
    }
}
