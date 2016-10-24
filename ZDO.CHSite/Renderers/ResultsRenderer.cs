using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;

using ZD.Common;
using ZDO.CHSite.Entities;

namespace ZDO.CHSite.Renderers
{
    public class ResultsRenderer
    {
        private readonly CedictLookupResult lr;
        private readonly UiScript uiScript;
        private readonly UiTones uiTones;

        public ResultsRenderer(CedictLookupResult lr, UiScript uiScript, UiTones uiTones)
        {
            this.lr = lr;
            this.uiScript = uiScript;
            this.uiTones = uiTones;
        }

        public void Render(StringBuilder sb)
        {
            sb.AppendLine("<div id='results'>");
            int max = Math.Min(lr.Results.Count, 256);
            for (int i = 0; i != max; ++i)
            {
                var lres = lr.Results[i];
                EntryRenderer er = new EntryRenderer(lres, uiScript, uiTones);
                er.OneLineHanziLimit = 9;
                er.Render(sb);
                if (i != max - 1) sb.AppendLine("<div class='resultSep'></div>");
            }
            sb.AppendLine("</div>");
        }
    }
}
