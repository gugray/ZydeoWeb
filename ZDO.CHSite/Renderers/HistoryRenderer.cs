using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;

using ZD.Common;
using ZD.LangUtils;
using ZDO.CHSite.Entities;
using ZDO.CHSite.Logic;

namespace ZDO.CHSite.Renderers
{
    public class HistoryRenderer
    {
        private readonly CedictParser parser = new CedictParser();
        private readonly string lang;
        private readonly TextProvider tprov;
        private readonly int histPageSize;
        private readonly int histPageIX;
        private readonly int histPageCount;
        private readonly List<ChangeItem> histChanges;

        public HistoryRenderer(string lang, int histPageSize, int histPageIX, int histPageCount,
            List<ChangeItem> histChanges)
        {
            this.lang = lang;
            tprov = TextProvider.Instance;
            this.histPageSize = histPageSize;
            this.histPageIX = histPageIX;
            this.histPageCount = histPageCount;
            this.histChanges = histChanges;
        }

        public void Render(StringBuilder sb)
        {
            sb.AppendLine("<div id='pager'>");
            sb.AppendLine("<div id='lblPage'>" + tprov.GetString(lang, "history.lblPage") + "</div>");
            sb.AppendLine("<div id='pageLinks'>");
            buildHistoryLinks(sb);
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");
            for (int i = 0; i != histChanges.Count; ++i)
            {
                ChangeItem ci = histChanges[i];
                histRenderChange(sb, ci, i != histChanges.Count - 1, lang, parser);
            }
        }

        public static void RenderItem(StringBuilder sb, string currTrg, EntryStatus currStatus,
            ChangeItem change, string lang)
        {
            CedictParser parser = new CedictParser();
            histRenderChange(sb, change, false, lang, parser, "reloaded");
        }

        public static void RenderPastChanges(StringBuilder sb, string entryId, 
            string currHead, string currTrg, EntryStatus currStatus,
            List<ChangeItem> changes, string lang)
        {
            sb.Append("<div class='pastChanges' data-entry-id='" + entryId + "'>");
            sb.AppendLine("<div class='pastInner'>");
            CedictParser parser = new CedictParser();
            string headNow = currHead;
            string trgNow = currTrg;
            EntryStatus statusNow = currStatus;
            foreach (var ci in changes) renderPastChange(sb, parser, ref currHead, ref trgNow, ref statusNow, ci, lang);
            sb.AppendLine("</div></div>"); // <div class='pastChanges'><div class='pastInner'>
        }

        public static void RenderEntryChanges(StringBuilder sb, 
            string currHead, string currTrg, EntryStatus currStatus,
            List<ChangeItem> changes, string lang)
        {
            sb.AppendLine("<div class='pastChanges'><div class='pastInner'>");
            CedictParser parser = new CedictParser();
            string headNow = currHead;
            string trgNow = currTrg;
            EntryStatus statusNow = currStatus;
            foreach (var ci in changes) renderPastChange(sb, parser, ref headNow, ref trgNow, ref statusNow, ci, lang);
            sb.AppendLine("</div></div>"); // <div class='pastChanges'><div class='pastInner'>
        }

        private static string getChangeTypeStr(ChangeType ct, int countB, EntryStatus entryStatus, string lang)
        {
            string changeMsg;
            if (ct == ChangeType.New) changeMsg = TextProvider.Instance.GetString(lang, "history.changeNewEntry");
            else if (ct == ChangeType.Edit) changeMsg = TextProvider.Instance.GetString(lang, "history.changeEdited");
            else if (ct == ChangeType.Note) changeMsg = TextProvider.Instance.GetString(lang, "history.changeCommented");
            else if (ct == ChangeType.BulkImport) changeMsg = TextProvider.Instance.GetString(lang, "history.changeBulkImport");
            else if (ct == ChangeType.StatusChange)
            {
                if (entryStatus == EntryStatus.Approved) changeMsg = TextProvider.Instance.GetString(lang, "history.changeApproved");
                else if (entryStatus == EntryStatus.Flagged) changeMsg = TextProvider.Instance.GetString(lang, "history.changeFlagged");
                else  changeMsg = TextProvider.Instance.GetString(lang, "history.changeStatusNeutral");
            }
            else changeMsg = ct.ToString();
            return changeMsg;
        }

        //private static readonly string dtFmtHu = "{0}-{1:00}-{2:00} {3:00}:{4:00}";
        //private static readonly string dtFmtDe = "{2:00}.{1:00}.{0} {3:00}:{4:00}";
        //private static readonly string dtFmtEn = "{1:00}/{2:00}/{0} {3:00}:{4:00}";

        private static string getTimeStr(DateTime dtUtc, string lang)
        {
            string dateStr = Utils.ChinesDateStr(dtUtc);
            DateTime dt = dtUtc.ToLocalTime();
            string dateTimeStr = string.Format("{0} {1}:{2}", dateStr, dt.Hour.ToString("00"), dt.Minute.ToString("00"));
            return dateTimeStr;

            //string dtFmt = dtFmtEn;
            //if (lang == "hu") dtFmt = dtFmtHu;
            //else if (lang == "de") dtFmt = dtFmtDe;
            //// US time gets special treatment (12-hour, AM/PM)
            //if (lang != "en")
            //    dtFmt = string.Format(dtFmt, dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute);
            //else
            //{
            //    string ampm = " AM";
            //    int hour = dt.Hour;
            //    if (hour > 12) { hour -= 12; ampm = " PM"; }
            //    if (hour == 12) ampm = " PM";
            //    dtFmt = string.Format(dtFmt, dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute) + ampm;
            //}
            //return dtFmt;
        }

        private static void renderPastChange(StringBuilder sb, CedictParser parser, 
            ref string headNow, ref string trgNow, ref EntryStatus statusNow,
            ChangeItem ci, string lang)
        {
            sb.AppendLine("<div class='pastItem'>");

            sb.Append("<div class='changeSummary'>");
            sb.Append("<span class='changeType'>");
            sb.Append(HtmlEncoder.Default.Encode(getChangeTypeStr(ci.ChangeType, ci.CountB, statusNow, lang)));
            sb.Append(" &bull; </span>");
            sb.Append("<span class='changeUser'>");
            sb.Append(HtmlEncoder.Default.Encode(ci.User));
            sb.Append("</span>");
            sb.Append("<span class='changeTime'>");
            sb.Append(HtmlEncoder.Default.Encode(getTimeStr(ci.When, lang)));
            sb.Append("</span>");
            sb.AppendLine("</div>"); // <div class='changeSummary'>

            sb.AppendLine("<div class='changeNote'>");
            sb.Append("<span class='changeNoteText'>");

            if (ci.BulkRef != -1)
            {
                sb.Append("<span class='bulkLink'>[");
                sb.Append("<a href='/" + lang + "/read/details/change-" + ci.BulkRef.ToString("000") + "' target='_blank'>");
                sb.Append(TextProvider.Instance.GetString(lang, "history.bulkLink") + "</a>");
                sb.Append("]</span> ");
            }

            string note = HtmlEncoder.Default.Encode(ci.Note);
            note = note.Replace("&#xA;", "<br/>");
            sb.Append(note);
            sb.Append("</span>");
            sb.AppendLine("</div>"); // <div class='changeNote'>

            if (ci.HeadBefore != null)
            {
                CedictEntry eCurr = parser.ParseEntry(headNow + " /x/", -1, null);
                CedictEntry eOld = parser.ParseEntry(ci.HeadBefore + " /x/", -1, null);
                bool simpChanged = eCurr.ChSimpl != eOld.ChSimpl;
                bool tradChanged = eCurr.ChTrad != eOld.ChTrad;
                bool pinyinChanged = CedictWriter.WritePinyin(eCurr) != CedictWriter.WritePinyin(eOld);
                // Render in parts
                sb.AppendLine("<div class='entry'>");
                // Let's not dim identical chars if anything changed in HW
                EntryRenderer rCurr = new EntryRenderer(lang, eCurr, !simpChanged && !tradChanged);
                EntryRenderer rOld = new EntryRenderer(lang, eOld, !simpChanged && !tradChanged);
                rCurr.OneLineHanziLimit = rOld.OneLineHanziLimit = 12;
                if (simpChanged || tradChanged)
                {
                    rCurr.XRenderHanzi(sb, simpChanged ? "new" : "", tradChanged ? "new" : "");
                    rOld.XRenderHanzi(sb, simpChanged ? "old" : "", tradChanged ? "old" : "");
                }
                if (pinyinChanged)
                {
                    rCurr.XRenderPinyin(sb, pinyinChanged ? "new" : "");
                    rOld.XRenderPinyin(sb, "old");
                }
                sb.AppendLine("</div>"); // <div class='entry'>
                // Propagate change
                headNow = ci.HeadBefore;
            }

            if (ci.BodyBefore != null)
            {
                CedictEntry entryNew = parser.ParseEntry("的 的 [de5] " + trgNow, -1, null);
                EntryRenderer er = new EntryRenderer(lang, entryNew, true);
                er.XRenderSenses(sb, "new");
                CedictEntry entryOld = parser.ParseEntry("的 的 [de5] " + ci.BodyBefore, -1, null);
                er = new EntryRenderer(lang, entryOld, true);
                er.XRenderSenses(sb, "old");
                // Propagate change
                trgNow = ci.BodyBefore;
            }
            if (ci.StatusBefore != 99) statusNow = (EntryStatus)ci.StatusBefore;

            sb.AppendLine("</div>"); // <div class='pastItem'>
        }

        private static void histRenderChange(StringBuilder sb, ChangeItem ci, bool trailingSeparator, string lang,
            CedictParser parser, string extraItemClass = "")
        {
            var tprov = TextProvider.Instance;

            sb.AppendLine();
            string itemClass = "historyItem";
            if (!string.IsNullOrEmpty(extraItemClass)) itemClass += " " + extraItemClass;
            if (ci.EntryId >= 0)
                sb.AppendLine("<div class='" + itemClass + "' data-entry-id='" + EntryId.IdToString(ci.EntryId) + "'>");
            else sb.AppendLine("<div class='" + itemClass + "'>");
            sb.AppendLine("<div class='changeHead'>");

            string iconClass = "";
            if (ci.ChangeType == ChangeType.New) iconClass = "fa fa-lightbulb-o ctNew";
            else if (ci.ChangeType == ChangeType.Edit) iconClass = "fa fa-pencil-square-o ctEdit";
            else if (ci.ChangeType == ChangeType.Note) iconClass = "fa fa-commenting-o ctNote";
            else if (ci.ChangeType == ChangeType.BulkImport) iconClass = "fa fa-newspaper-o ctBulk";
            else if (ci.ChangeType == ChangeType.StatusChange)
            {
                if (ci.EntryStatus == EntryStatus.Approved) iconClass = "fa fa-check-square-o ctApprove";
                else if (ci.EntryStatus == EntryStatus.Flagged) iconClass = "fa fa-flag-o ctFlag";
                else iconClass = "fa fa-flag-o ctUnflag";
            }

            sb.Append("<i class='" + iconClass + "' />");
            sb.Append("<div class='changeSummary'>");

            string changeMsg = getChangeTypeStr(ci.ChangeType, ci.CountB, ci.EntryStatus, lang);
            string changeCls = "changeType";
            sb.Append("<span class='" + changeCls + "'>");
            sb.Append(HtmlEncoder.Default.Encode(changeMsg));
            sb.Append(" &bull; </span>");

            sb.Append("<span class='changeUser'>");
            sb.Append(HtmlEncoder.Default.Encode(ci.User));
            sb.Append("</span>");

            sb.Append("<span class='changeTime'>");
            sb.Append(HtmlEncoder.Default.Encode(getTimeStr(ci.When, lang)));
            sb.Append("</span>");

            if (ci.ChangeType != ChangeType.BulkImport && ci.CountA > 0)
            {
                sb.Append("<span class='revealPast'>+" + ci.CountA.ToString() + "</span>");
            }

            sb.Append("</div>"); // <div class='changeSummary'>

            sb.AppendLine("<div class='changeNote'>");
            if (ci.ChangeType == ChangeType.BulkImport)
            {
                string newCount = null;
                string chgCount = null;
                if (ci.CountA > 0)
                {
                    newCount = tprov.GetString(lang, "history.bulkNewWords");
                    newCount = string.Format(newCount, ci.CountA);
                }
                if (ci.CountB > 0)
                {
                    chgCount = tprov.GetString(lang, "history.bulkChangedWords");
                    chgCount = string.Format(chgCount, ci.CountB);
                }
                if (chgCount != null || newCount != null)
                {
                    sb.Append("<p>");
                    if (newCount != null) sb.Append(HtmlEncoder.Default.Encode(newCount));
                    if (newCount != null && chgCount != null) sb.Append(" &bull; ");
                    if (chgCount != null) sb.Append(HtmlEncoder.Default.Encode(chgCount));
                    sb.Append("</p>");
                }
                sb.Append("<span class='bulkLink'>[");
                sb.Append("<a href='/" + lang + "/read/details/change-" + ci.BulkRef.ToString("000") + "' target='_blank'>");
                sb.Append(tprov.GetString(lang, "history.bulkLink") + "</a>");
                sb.Append("]</span> ");
            }
            sb.Append("<span class='changeNoteText'>");
            string note = HtmlEncoder.Default.Encode(ci.Note);
            note = note.Replace("&#xA;", "<br/>");
            sb.Append(note);
            sb.Append("</span>");
            sb.Append("</div>");

            sb.Append("</div>"); // <div class='changeHead'>

            sb.AppendLine("<div class='changeEntry'>");
            if (ci.ChangeType != ChangeType.BulkImport)
            {
                sb.AppendLine("<div class='histEntryOps'>");
                sb.Append("<a class='ajax' href='/" + lang + "/edit/existing/" + EntryId.IdToString(ci.EntryId) + "'>");
                sb.Append("<i class='opHistEdit fa fa-pencil'></i></a>");
                //sb.Append("<i class='opHistEdit fa fa-pencil' />");
                sb.Append("<i class='opHistComment fa fa-commenting-o' />");
                sb.Append("<i class='opHistFlag fa fa-flag-o' />");
                sb.Append("</div>"); // <div class='histEntryOps'>
            }

            if (ci.ChangeType != ChangeType.BulkImport)
            {
                // NOT edited
                if (ci.BodyBefore == null && ci.HeadBefore == null)
                {
                    CedictEntry entry = parser.ParseEntry(ci.EntryHead + " " + ci.EntryBody, 0, null);
                    entry.Status = ci.EntryStatus;
                    EntryRenderer er = new EntryRenderer(lang, entry, true);
                    er.OneLineHanziLimit = 12;
                    er.Render(sb, null);
                }
                // Entry edited: show "diff" in head and/or body
                else
                {
                    // Current, and comparison base
                    CedictEntry eCurr = parser.ParseEntry(ci.EntryHead + " " + ci.EntryBody, 0, null);
                    eCurr.Status = ci.EntryStatus;
                    string headOld = ci.HeadBefore == null ? ci.EntryHead : ci.HeadBefore;
                    string bodyOld = ci.BodyBefore == null ? ci.EntryBody : ci.BodyBefore;
                    CedictEntry eOld = parser.ParseEntry(headOld + " " + bodyOld, 0, null);
                    bool simpChanged = eCurr.ChSimpl != eOld.ChSimpl;
                    bool tradChanged = eCurr.ChTrad != eOld.ChTrad;
                    bool pinyinChanged = CedictWriter.WritePinyin(eCurr) != CedictWriter.WritePinyin(eOld);
                    bool bodyChanged = ci.BodyBefore != null;
                    // Render in parts
                    sb.AppendLine("<div class='entry'>");
                    // Let's not dim identical chars if anything changed in HW
                    EntryRenderer rCurr = new EntryRenderer(lang, eCurr, !simpChanged && !tradChanged);
                    EntryRenderer rOld = new EntryRenderer(lang, eOld, !simpChanged && !tradChanged);
                    rCurr.OneLineHanziLimit = rOld.OneLineHanziLimit = 12;
                    rCurr.XRenderStatus(sb);
                    rCurr.XRenderHanzi(sb, simpChanged ? "new" : "", tradChanged ? "new" : "");
                    if (simpChanged || tradChanged)
                        rOld.XRenderHanzi(sb, simpChanged ? "old" : "", tradChanged ? "old" : "");
                    rCurr.XRenderPinyin(sb, pinyinChanged ? "new" : "");
                    if (pinyinChanged)
                        rOld.XRenderPinyin(sb, "old");
                    rCurr.XRenderSenses(sb, bodyChanged ? "new" : "");
                    if (bodyChanged)
                        rOld.XRenderSenses(sb, "old");
                    sb.AppendLine("</div>"); // <div class='entry'>
                }
            }
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
