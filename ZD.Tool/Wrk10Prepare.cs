using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;

using ZD.Common;
using ZD.LangUtils;

namespace ZD.Tool
{
    public class Wrk10Prepare : IWorker
    {
        public void Work()
        {
            Random rnd = new Random(0);
            CedictParser parser = new CedictParser();
            HashSet<int> idSet = new HashSet<int>();

            using (FileStream fsIn = new FileStream("handedict.u8", FileMode.Open, FileAccess.Read))
            using (StreamReader sr = new StreamReader(fsIn))
            using (FileStream fsOut = new FileStream("x-10-handedict.txt", FileMode.Create, FileAccess.ReadWrite))
            using (StreamWriter sw = new StreamWriter(fsOut))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.StartsWith("#")) continue;
                    CedictEntry entry = parser.ParseEntry(line, 0, null);
                    if (entry == null) continue;
                    int id = rnd.Next();
                    while (idSet.Contains(id)) id = rnd.Next();
                    idSet.Add(id);
                    string strId = EntryId.IdToString(id);
                    bool isVerif = isVerified(entry);
                    // Line with ID
                    sw.WriteLine("# ID-" + strId);
                    // First version metainfo
                    string statStr = isVerif ? "Stat-Verif" : "Stat-New";
                    sw.WriteLine("# Ver-1 2011-05-28T01:27:49Z HanDeDict " + statStr + " >Originalversion HanDeDict-Datei");
                    // The entry itself
                    sw.WriteLine(CedictWriter.Write(entry));
                    // Empty line between entries
                    sw.WriteLine();
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
        { }
    }
}
