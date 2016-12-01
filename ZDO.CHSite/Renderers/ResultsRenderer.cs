using System.Text;

using ZD.Common;
using ZDO.CHSite.Entities;

namespace ZDO.CHSite.Renderers
{
    public class ResultsRenderer
    {
        private const int maxResults = int.MaxValue;

        private readonly CedictLookupResult lr;
        private readonly UiScript uiScript;
        private readonly UiTones uiTones;

        public ResultsRenderer(CedictLookupResult lr, UiScript uiScript, UiTones uiTones)
        {
            this.lr = lr;
            this.uiScript = uiScript;
            this.uiTones = uiTones;
        }

        public void Render(StringBuilder sb, string uiLang)
        {
            sb.AppendLine("<div id='results'>");
            for (int i = 0; i != lr.Results.Count && i < maxResults; ++i)
            {
                string entryIdStr = EntryId.IdToString(lr.Results[i].Entry.StableId);
                EntryRenderer er = new EntryRenderer(lr.Results[i], uiScript, uiTones, entryIdStr);
                // TO-DO: double-check, also for mobile
                er.OneLineHanziLimit = 9;
                er.Render(sb, uiLang);
                if (i != lr.Results.Count - 1) sb.AppendLine("<div class='resultSep'></div>");
            }
            if (lr.Annotations.Count != 0)
            {
                sb.Append("<div class='notice'>");
                sb.Append("<i class='fa fa-umbrella'></i>");
                sb.Append("<span>" + TextProvider.Instance.GetString(uiLang, "search.annotationNotice") + "</span>");
                sb.AppendLine("</div>");
                for (int i = 0; i != lr.Annotations.Count; ++i)
                {
                    EntryRenderer er = new EntryRenderer(lr.Query, lr.Annotations[i], uiTones);
                    er.Render(sb, uiLang);
                    if (i != lr.Annotations.Count - 1) sb.AppendLine("<div class='resultSep'></div>");
                }
            }
            sb.AppendLine("</div>");
        }
    }
}
