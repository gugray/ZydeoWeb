using System;
using System.Text;
using System.Collections.Generic;
using System.IO;

using ZD.Common;
using ZD.LangUtils;

namespace ZD.Tool
{
    public class Wrk10Prepare : IWorker
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

            using (FileStream fsIn = new FileStream("handedict.u8", FileMode.Open, FileAccess.Read))
            using (StreamReader sr = new StreamReader(fsIn))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.StartsWith("#")) continue;
                    CedictEntry entry = parser.ParseEntry(line, 0, null);
                    if (entry == null) continue;
                    if (entry.ChSimpl.Length > 16) continue;

                    int id = rnd.Next();
                    while (idSet.Contains(id)) id = rnd.Next();
                    idSet.Add(id);
                    string strId = EntryId.IdToString(id);
                    bool isVerif = isVerified(entry);

                    sb.Clear();
                    // Line with ID
                    sb.AppendLine("# ID-" + strId);
                    // First version metainfo
                    string statStr = isVerif ? "Stat-Verif" : "Stat-New";
                    sb.AppendLine("# Ver 2011-05-28T01:27:49Z HanDeDict " + statStr + " 001>Originalversion HanDeDict-Datei");
                    // The entry itself
                    sb.AppendLine(CedictWriter.Write(entry));

                    items.Add(new ResItem { ID = id, Lines = sb.ToString() });
                }
            }
        }

        /// <summary>
        /// Checks if entry contains "(u.E.)"
        /// </summary>
        private static bool isVerified(CedictEntry entry)
        {
            bool isUE = false;
            foreach (var sense in entry.Senses)
            {
                if (sense.GetPlainText().Contains("(u.E.)")) { isUE = true; break; }
            }
            return !isUE;
        }

        public void Init()
        { }

        public void Dispose()
        { }

        public void Finish()
        {
            items.Sort((x, y) => x.ID.CompareTo(y.ID));
            using (FileStream fsOut = new FileStream("x-10-handedict.txt", FileMode.Create, FileAccess.ReadWrite))
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
