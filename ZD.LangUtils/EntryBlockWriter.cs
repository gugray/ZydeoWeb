using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using ZD.Common;

namespace ZD.LangUtils
{
    public class EntryBlockWriter
    {
        private readonly StreamWriter sw;
        private readonly StringBuilder sb = new StringBuilder();

        public EntryBlockWriter(StreamWriter sw)
        {
            this.sw = sw;
        }

        public void WriteBlock(int entryId, List<EntryVersion> vers)
        {
            sw.WriteLine();
            sw.Write("# ID-");
            sw.WriteLine(EntryId.IdToString(entryId));
            for (int i = 0; i != vers.Count; ++i)
            {
                EntryVersion ver = vers[i];
                sw.Write("# Ver ");
                sw.Write(formatDate(ver.Timestamp));
                sw.Write(' ');
                sw.Write(ver.User);
                sw.Write(" Stat-");
                if (ver.Status == EntryStatus.Neutral) sw.Write("New");
                else if (ver.Status == EntryStatus.Approved) sw.Write("Verif");
                else if (ver.Status == EntryStatus.Flagged) sw.Write("Flagged");
                else throw new Exception("Forgotten entry status: " + ver.Status);
                sw.Write(' ');
                if (ver.BulkRef > 0) sw.Write(ver.BulkRef.ToString("000"));
                sw.Write('>');
                string cmtEsc = ver.Comment;
                if (cmtEsc.Contains(@"\")) cmtEsc = cmtEsc.Replace(@"\", @"\\");
                if (cmtEsc.Contains("\n")) cmtEsc = cmtEsc.Replace("\n", "\\n");
                sw.WriteLine(cmtEsc);
                if (ver.Entry != null)
                {
                    string entryStr = CedictWriter.Write(ver.Entry);
                    if (i != vers.Count - 1) sw.Write("# ");
                    sw.WriteLine(entryStr);
                }
            }
        }

        private string formatDate(DateTime dt)
        {
            sb.Clear();
            sb.Append(dt.Year.ToString());
            sb.Append('-');
            sb.Append(dt.Month.ToString("00"));
            sb.Append('-');
            sb.Append(dt.Day.ToString("00"));
            sb.Append('T');
            sb.Append(dt.Hour.ToString("00"));
            sb.Append(':');
            sb.Append(dt.Minute.ToString("00"));
            sb.Append(':');
            sb.Append(dt.Second.ToString("00"));
            sb.Append('Z');
            return sb.ToString();
        }
    }
}
