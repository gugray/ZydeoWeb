using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;

using ZD.Common;

namespace ZD.LangUtils
{
    public class CedictWriter
    {
        public static string Write(CedictEntry entry)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(entry.ChTrad);
            sb.Append(' ');
            sb.Append(entry.ChSimpl);
            sb.Append(" [");
            for (int i = 0; i != entry.PinyinCount; ++i)
            {
                if (i != 0) sb.Append(' ');
                sb.Append(entry.GetPinyinAt(i).GetDisplayString(false));
            }
            sb.Append("] /");
            for (int i = 0; i != entry.SenseCount; ++i)
            {
                string sense = entry.GetSenseAt(i).GetPlainText();
                if (sense.Contains('/')) sense = sense.Replace('/', '\\');
                sb.Append(sense);
                sb.Append('/');
            }
            return sb.ToString();
        }

        public static void Write(CedictEntry entry, out string head, out string trg)
        {
            StringBuilder sbHead = new StringBuilder();
            sbHead.Append(entry.ChTrad);
            sbHead.Append(' ');
            sbHead.Append(entry.ChSimpl);
            sbHead.Append(" [");
            for (int i = 0; i != entry.PinyinCount; ++i)
            {
                if (i != 0) sbHead.Append(' ');
                sbHead.Append(entry.GetPinyinAt(i).GetDisplayString(false));
            }
            sbHead.Append("]");
            head = sbHead.ToString();
            StringBuilder sbTrg = new StringBuilder();
            sbTrg.Append('/');
            for (int i = 0; i != entry.SenseCount; ++i)
            {
                sbTrg.Append(entry.GetSenseAt(i).GetPlainText());
                sbTrg.Append('/');
            }
            trg = sbTrg.ToString();
        }
    }
}
