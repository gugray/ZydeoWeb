using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using ZD.Common;
using ZD.LangUtils;

namespace ZDO.CHSite.Logic
{
    public class Utils
    {
        public static string ChinesDateStr(DateTime dtUtc)
        {
            DateTime dt = dtUtc.ToLocalTime();
            return dt.Year + "年" + dt.Month + "月" + dt.Day + "日";
        }

        public static bool IsHanzi(char c)
        {
            return (c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3400 && c <= 0x4DFF) ||
                (c >= 0xF900 && c <= 0xFAFF);
        }

        public static CedictEntry BuildEntry(string headword, string trg)
        {
            Regex re = new Regex(@"([^ ]+) ([^ ]+) \[([^\]]+)\]");
            var m = re.Match(headword);
            return BuildEntry(m.Groups[2].Value, m.Groups[1].Value, m.Groups[3].Value, trg.Trim('/'));
        }

        public static CedictEntry BuildEntry(string simp, string trad, string pinyin, string trg)
        {
            // Prepare pinyin as list of proper syllables
            List<PinyinSyllable> pyList = new List<PinyinSyllable>();
            string[] pyRawArr = pinyin.Split(' ');
            foreach (string pyRaw in pyRawArr)
            {
                PinyinSyllable ps = PinyinSyllable.FromDisplayString(pyRaw);
                if (ps == null) ps = new PinyinSyllable(pyRaw, -1);
                pyList.Add(ps);
            }

            // Build TRG entry in "canonical" form; parse; render
            trg = trg.Replace("\r\n", "\n");
            string[] senses = trg.Split('\n');
            string can = trad + " " + simp + " [";
            for (int i = 0; i != pyList.Count; ++i)
            {
                if (i != 0) can += " ";
                can += pyList[i].GetDisplayString(false);
            }
            can += "] /";
            foreach (string str in senses) can += str.Replace('/', '\\') + "/";
            CedictParser parser = new CedictParser();
            return parser.ParseEntry(can, 0, null);
        }
    }
}
