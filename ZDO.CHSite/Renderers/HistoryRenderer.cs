using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;

using ZD.Common;
using ZDO.CHSite.Entities;
using ZDO.CHSite.Logic;

namespace ZDO.CHSite.Renderers
{
    public class HistoryRenderer
    {
        private readonly string lang;
        private readonly int histPageSize;
        private readonly int histPageIX;
        private readonly int histPageCount;
        private readonly List<ChangeItem> histChanges;

        public HistoryRenderer(string lang, int histPageSize, int histPageIX, int histPageCount,
            List<ChangeItem> histChanges)
        {
            this.lang = lang;
            this.histPageSize = histPageSize;
            this.histPageIX = histPageIX;
            this.histPageCount = histPageCount;
            this.histChanges = histChanges;
        }

        public void Render(StringBuilder sb)
        {
            sb.AppendLine("<div id='pager'>");
            sb.AppendLine("<div id='lblPage'>Oldal</div>");
            sb.AppendLine("<div id='pageLinks'>");
            buildHistoryLinks(sb);
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");
            for (int i = 0; i != histChanges.Count; ++i)
            {
                ChangeItem ci = histChanges[i];
                histRenderChange(sb, ci, i != histChanges.Count - 1);
            }
        }

        private void histRenderChange(StringBuilder sb, ChangeItem ci, bool trailingSeparator)
        {
            sb.AppendLine();
            sb.AppendLine("<div class='historyItem'>");
            sb.AppendLine("<div class='changeHead'>");

            sb.Append("<i class='fa fa-lightbulb-o ctNew' />");
            sb.Append("<div class='changeSummary'>");

            string changeMsg;
            string changeCls = "changeType";
            // TO-DO: Loca
            if (ci.ChangeType == ChangeType.New) changeMsg = "Új szócikk";
            else if (ci.ChangeType == ChangeType.Edit) changeMsg = "Szerkesztve";
            else if (ci.ChangeType == ChangeType.Note) changeMsg = "Megjegyzés";
            else changeMsg = ci.ChangeType.ToString();
            changeMsg += ": ";
            sb.Append("<span class='" + changeCls + "'>");
            sb.Append(HtmlEncoder.Default.Encode(changeMsg));
            sb.Append("</span>");

            sb.Append("<span class='changeUser'>");
            sb.Append(HtmlEncoder.Default.Encode(ci.User));
            sb.Append("</span>");
            sb.Append(" &bull; ");

            sb.Append("<span class='changeTime'>");
            // TO-DO: convert to user's time zone
            //DateTime dt = TimeZoneInfo.ConvertTimeFromUtc(ci.When, Global.TimeZoneInfo);
            DateTime dt = ci.When;
            string dtFmt = "{0}-{1:00}-{2:00} {3:00}:{4:00}";
            dtFmt = string.Format(dtFmt, dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute);
            sb.Append(HtmlEncoder.Default.Encode(dtFmt));
            sb.Append("</span>");

            sb.Append("</div>"); // <div class='changeSummary'>

            sb.AppendLine("<div class='changeNote'>");
            sb.Append(HtmlEncoder.Default.Encode(ci.Note));
            sb.Append("</div>");

            sb.Append("</div>"); // <div class='changeHead'>

            sb.AppendLine("<div class='changeEntry'>");
            sb.AppendLine("<div class='histEntryOps'>");
            sb.Append("<i class='opHistEdit fa fa-pencil-square-o' />");
            sb.Append("<i class='opHistComment fa fa-commenting-o' />");
            sb.Append("<i class='opHistFlag fa fa-flag-o' />");
            sb.Append("</div>"); // <div class='histEntryOps'>

            CedictEntry entry = Utils.BuildEntry(ci.EntryHead, ci.EntryBody);
            EntryRenderer er = new EntryRenderer(entry);
            er.OneLineHanziLimit = 12;
            er.Render(sb);
            sb.Append("</div>"); // <div class='changeEntry'>

            sb.Append("</div>"); // <div class='changeHead'>
            sb.Append("</div>"); // <div class='historyItem'>
            sb.AppendLine();

            if (trailingSeparator) sb.AppendLine("<div class='historySep'></div>");
        }

        private string buildHistPageLink(string lang, int ix)
        {
            string astr = "<a href='{0}' class='{1}'>{2}</a>\r\n";
            string strClass = "pageLink ajax";
            if (ix == histPageIX) strClass += " selected";
            string strUrl = "/" + lang + "/edit/history";
            if (ix > 0) strUrl += "/page-" + (ix + 1).ToString();
            string strText = (ix + 1).ToString();
            astr = string.Format(astr, strUrl, strClass, strText);
            return astr;
        }

        private void buildHistoryLinks(StringBuilder sb)
        {
            // Two main strategies. Not more than 10 page links: throw them all in.
            // Otherwise, improvise gaps; pattern:
            // 1 2 ... (n-1) *n* (n+1) ... (max-1) (max)
            // Omit gap if no numbers are skipped
            int lastRenderedIX = 0;
            for (int i = 0; i != histPageCount; ++i)
            {
                // Few pages: dump all
                if (histPageCount < 11)
                {
                    sb.Append(buildHistPageLink(lang, i));
                    continue;
                }
                // Otherwise: get smart
                // 1, 2,  (n-1), n, (n+1),  (max-1), (max) only
                if (i == 0 || i == 1 || i == histPageCount - 2 || i == histPageCount - 1 ||
                    i == histPageIX - 1 || i == histPageIX || i == histPageIX + 1)
                {
                    // If we just skipped a page, render dot-dot-dot
                    if (i > lastRenderedIX + 1)
                    {
                        string strSpan = "<span class='pageSpacer'>&middot; &middot; &middot;</span>\r\n";
                        sb.Append(strSpan);
                    }
                    // Render page link
                    sb.Append(buildHistPageLink(lang, i));
                    // Remember last rendered
                    lastRenderedIX = i;
                    continue;
                }
            }
        }

    }
}
