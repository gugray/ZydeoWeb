using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Text;

using ZDO.CHSite.Entities;

namespace ZDO.CHSite.Logic
{
    public class QueryLogger
    {
        public enum SearchMode
        {
            Source,
            Target,
            Annotate,
        }

        private const string fmtTime = "{0}-{1:00}-{2:00}!{3:00}:{4:00}:{5:00}";
        internal static string FormatTime(DateTime dt)
        {
            return string.Format(fmtTime, dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
        }

        private interface IAuditItem
        {
            string LogLine { get; }
        }

        private class HanziItem : IAuditItem
        {
            private readonly DateTime time;
            private readonly string countryCode;
            private readonly char hanzi;
            private readonly bool found;

            public HanziItem(string countryCode, char hanzi, bool found)
            {
                this.time = DateTime.UtcNow;
                this.countryCode = countryCode;
                this.hanzi = hanzi;
                this.found = found;
            }
            public string LogLine
            {
                get
                {
                    string country = countryCode + "-HANZI";

                    StringBuilder sb = new StringBuilder();
                    sb.Append(QueryLogger.FormatTime(time));
                    sb.Append('\t');
                    sb.Append(country);
                    sb.Append('\t');
                    sb.Append('\t');
                    sb.Append('\t');
                    sb.Append('\t');
                    sb.Append(found ? "1" : "0");
                    sb.Append('\t');
                    sb.Append(hanzi);

                    return sb.ToString();
                }
            }
        }

        private class QueryItem : IAuditItem
        {
            private readonly DateTime time;
            private readonly string countryCode;
            private readonly bool isMobile;
            private readonly string uiLang;
            private readonly UiScript script;
            private readonly UiTones tones;
            private readonly int resCount;
            private readonly int msecLookup;
            private readonly int msecTotal;
            private readonly SearchMode smode;
            private readonly string query;

            public QueryItem(string countryCode, bool isMobile, string uiLang,
                UiScript script, UiTones tones,
                int resCount, int msecLookup, int msecTotal,
                SearchMode smode, string query)
            {
                this.countryCode = countryCode;
                this.time = DateTime.UtcNow;
                this.isMobile = isMobile;
                this.uiLang = uiLang;
                this.script = script;
                this.tones = tones;
                this.resCount = resCount;
                this.msecLookup = msecLookup;
                this.msecTotal = msecTotal;
                this.smode = smode;
                this.query = query;
            }

            public string LogLine
            {
                get
                {
                    string country = countryCode + "-";
                    if (isMobile) country += 'M';
                    else country += 'D';
                    if (uiLang == "en") country += 'E';
                    else if (uiLang == "de") country += 'D';
                    else if (uiLang == "hu") country += 'H';
                    else if (uiLang == "jian") country += 'J';
                    else if (uiLang == "fan") country += 'F';
                    else country += 'X';
                    if (script == UiScript.Simp) country += 'S';
                    else if (script == UiScript.Trad) country += 'T';
                    else country += 'B';
                    if (tones == UiTones.None) country += 'N';
                    else if (tones == UiTones.Dummitt) country += 'D';
                    else country += 'P';

                    StringBuilder sb = new StringBuilder();
                    sb.Append(QueryLogger.FormatTime(time));
                    sb.Append('\t');
                    sb.Append(country);
                    sb.Append('\t');
                    string sModeStr;
                    if (smode == SearchMode.Source) sModeStr = "ZHO";
                    else if (smode == SearchMode.Target) sModeStr = "TRG";
                    else sModeStr = "ANN";
                    sb.Append(sModeStr);
                    sb.Append('\t');
                    int sec = msecTotal / 1000;
                    int ms = msecTotal - sec * 1000;
                    sb.Append(string.Format("{0:00}.{1:000}", sec, ms));
                    sb.Append('\t');
                    sec = msecLookup / 1000;
                    ms = msecLookup - sec * 1000;
                    sb.Append(string.Format("{0:00}.{1:000}", sec, ms));
                    sb.Append('\t');
                    sb.Append(resCount.ToString());
                    sb.Append('\t');
                    sb.Append(query);

                    return sb.ToString();
                }
            }
        }

        private readonly string logFileName;
        private Thread thr;
        private AutoResetEvent evt = new AutoResetEvent(false);
        private readonly List<IAuditItem> ilist = new List<IAuditItem>();
        private bool closing = false;

        public QueryLogger(string logFileName)
        {
            this.logFileName = logFileName;
            thr = new Thread(threadFun);
            thr.IsBackground = true;
            thr.Start();
        }

        public void Shutdown()
        {
            closing = true;
            evt.Set();
            thr.Join(1000);
        }

        private void threadFun(object ctxt)
        {
            List<IAuditItem> myList = new List<IAuditItem>();
            while (!closing)
            {
                evt.WaitOne(500);
                lock (ilist)
                {
                    myList.Clear();
                    myList.AddRange(ilist);
                    ilist.Clear();
                }
                if (myList.Count == 0) continue;
                using (FileStream fsQueryLog = new FileStream(logFileName, FileMode.Append, FileAccess.Write))
                using (StreamWriter swQueryLog = new StreamWriter(fsQueryLog))
                {
                    foreach (IAuditItem itm in myList)
                    {
                        if (itm is QueryItem) swQueryLog.WriteLine(itm.LogLine);
                        else if (itm is HanziItem) swQueryLog.WriteLine(itm.LogLine);
                    }
                    swQueryLog.Flush();
                }
            }
        }

        public void LogQuery(string countryCode, bool isMobile, string uiLang, UiScript script, UiTones tones,
            int resCount, int msecLookup, int msecTotal, SearchMode smode, string query)
        {
            QueryItem itm = new QueryItem(countryCode, isMobile, uiLang, script, tones,
                resCount, msecLookup, msecTotal, smode, query);
            lock (ilist)
            {
                ilist.Add(itm);
                evt.Set();
            }
        }

        public void LogHanzi(string countryCode, char hanzi, bool found)
        {
            HanziItem itm = new HanziItem(countryCode, hanzi, found);
            lock (ilist)
            {
                ilist.Add(itm);
                evt.Set();
            }
        }
    }
}