using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using ZDO.CHSite.Entities;
using ZDO.CHSite.Logic;
using ZDO.CHSite.Renderers;
using ZD.Common;
using ZD.LangUtils;

namespace ZDO.CHSite.Controllers
{
    public class EditController : Controller
    {
        private readonly LangRepo langRepo;
        private readonly SqlDict dict;
        private readonly Auth auth;

        public EditController(LangRepo langRepo, SqlDict dict, Auth auth)
        {
            this.langRepo = langRepo;
            this.dict = dict;
            this.auth = auth;
        }

        public IActionResult SaveEntryTrg([FromForm] string entryId, [FromForm] string trg, [FromForm] string note)
        {
            if (entryId == null || trg == null || note == null) return StatusCode(400, "Missing parameter(s).");
            // Must be authenticated user
            int userId;
            string userName;
            auth.CheckSession(HttpContext.Request.Headers, out userId, out userName);
            if (userId < 0) return StatusCode(401, "Request must contain authentication token.");

            EditEntryResult res = new EditEntryResult();

            int idVal = EntryId.StringToId(entryId);
            trg = trg.Replace("\r\n", "\n");
            trg = trg.Replace('/', '\\');
            trg = trg.Replace('\n', '/');
            trg = "/" + trg + "/";
            using (SqlDict.SimpleBuilder builder = dict.GetSimpleBuilder(userId))
            {
                builder.ChangeTarget(userId, idVal, trg, note);
            }
            // Refresh cached contrib score
            auth.RefreshUserInfo(userId);
            // Tell our caller we dun it
            res.Success = true;
            return new ObjectResult(res);
        }

        public IActionResult SaveFullEntry([FromForm] string entryId, [FromForm] string hw, [FromForm] string trg,
            [FromForm] string note, [FromForm] string lang)
        {
            if (entryId == null || hw == null || trg == null || note == null || lang == null)
                return StatusCode(400, "Missing parameter(s).");

            // Must be authenticated user
            int userId;
            string userName;
            auth.CheckSession(HttpContext.Request.Headers, out userId, out userName);
            if (userId < 0) return StatusCode(401, "Request must contain authentication token.");

            EditEntryResult res = new EditEntryResult();

            int idVal = EntryId.StringToId(entryId);
            trg = trg.Replace("\r\n", "\n");
            trg = trg.Replace('/', '\\');
            trg = trg.Replace('\n', '/');
            trg = "/" + trg + "/";
            CedictParser parser = new CedictParser();
            CedictEntry entry = null;
            try { entry = parser.ParseEntry(hw + " " + trg, 0, null); }
            catch { }
            if (entry == null)
            {
                res.Error = TextProvider.Instance.GetString(lang, "editEntry.badDataOnSave");
                return new ObjectResult(res);
            }

            bool persisted;
            using (SqlDict.SimpleBuilder builder = dict.GetSimpleBuilder(userId))
            {
                persisted = builder.ChangeHeadAndTarget(userId, idVal, hw, trg, note);
            }
            // Not persisted: violates uniqueness constraint
            if (!persisted)
            {
                res.Error = TextProvider.Instance.GetString(lang, "editEntry.duplicateOnSave");
                return new ObjectResult(res);
            }

            // Refresh cached contrib score
            auth.RefreshUserInfo(userId);
            // Tell our caller we dun it
            res.Success = true;
            return new ObjectResult(res);
        }

        public IActionResult GetEntryPreview([FromQuery] string origHw,
            [FromQuery] string trad, [FromQuery] string simp, [FromQuery] string pinyin,
            [FromQuery] string trgTxt, [FromQuery] string lang)
        {
            if (origHw == null || trgTxt == null || lang == null) return StatusCode(400, "Missing parameter(s).");
            if (trad == null) trad = "";
            if (simp == null) simp = "";
            if (pinyin == null) pinyin = "";
            EditEntryPreviewResult res = new EditEntryPreviewResult();

            // Ugly try-catch, but some incorrect input just generates an exception.
            // We still want to return a meaningful "no preview" response.
            try
            {
                // DBG
                if (trgTxt.Contains("micu-barf")) throw new Exception("barf");

                // Validate current headword
                bool hwParses =  validateHeadword(lang, simp, trad, pinyin,
                    res.ErrorsSimp, res.ErrorsTrad, res.ErrorsPinyin);

                trgTxt = trgTxt.Replace("\r\n", "\n");
                trgTxt = trgTxt.Replace('/', '\\');
                trgTxt = trgTxt.Replace('\n', '/');
                trgTxt = "/" + trgTxt + "/";

                // Headword: use original if current headword has errors
                string hw = trad + " " + simp + " [" + pinyin + "]";
                if (!hwParses) hw = origHw;

                CedictParser parser = new CedictParser();
                CedictEntry entry = parser.ParseEntry(hw + " " + trgTxt, 0, null);
                if (entry != null)
                {
                    EntryRenderer er = new EntryRenderer(lang, entry, true, "mainEntry");
                    er.OneLineHanziLimit = 12;
                    StringBuilder sb = new StringBuilder();
                    er.Render(sb, null);
                    res.PreviewHtml = sb.ToString();
                }
            }
            catch { }
            return new ObjectResult(res);
        }

        private bool validateHeadword(string lang, string simp, string trad, string pinyin,
            List<HeadwordProblem> errorsSimp, List<HeadwordProblem> errorsTrad, List<HeadwordProblem> errorsPinyin)
        {
            var tprov = TextProvider.Instance;
            string msg;
            // Check each simplified: is it really simplified?
            UniHanziInfo[] uhiSimp = langRepo.GetUnihanInfo(simp);
            for (int i = 0; i != uhiSimp.Length; ++i)
            {
                var uhi = uhiSimp[i];
                if (!uhi.CanBeSimp)
                {
                    msg = tprov.GetString(lang, "editEntry.hwProblemNotSimplified");
                    msg = string.Format(msg, simp[i]);
                    errorsSimp.Add(new HeadwordProblem(false, msg));
                }
            }
            // Check each traditional: is it really traditional?
            UniHanziInfo[] uhiTrad = langRepo.GetUnihanInfo(trad);
            for (int i = 0; i != uhiTrad.Length; ++i)
            {
                var uhi = uhiTrad[i];
                // Traditional chars are listed as their own traditional variant
                if (Array.IndexOf(uhi.TradVariants, trad[i]) < 0)
                {
                    msg = tprov.GetString(lang, "editEntry.hwProblemNotTraditional");
                    msg = string.Format(msg, trad[i]);
                    errorsTrad.Add(new HeadwordProblem(false, msg));
                }
            }
            // Check each traditional against its simplified friend
            if (trad.Length != simp.Length)
                errorsTrad.Add(new HeadwordProblem(true, tprov.GetString(lang, "editEntry.hwProblemSimpTradCounts")));
            else
            {
                for (int i = 0; i != uhiSimp.Length; ++i)
                {
                    var uhi = uhiSimp[i];
                    if (Array.IndexOf(uhi.TradVariants, trad[i]) < 0)
                    {
                        msg = tprov.GetString(lang, "editEntry.hwProblemNotTradForSimp");
                        msg = string.Format(msg, simp[i], trad[i]);
                        errorsTrad.Add(new HeadwordProblem(false, msg));
                    }
                }
            }
            // Normalize pinyin (multiple spaces, leading/trailing spaces)
            string pyNorm = pinyin;
            while (true)
            {
                string x = pyNorm.Replace("  ", " ");
                if (x == pyNorm) break;
                pyNorm = x;
            }
            pyNorm = pyNorm.Trim();
            if (pyNorm != pinyin) errorsPinyin.Add(new HeadwordProblem(true, tprov.GetString(lang, "editEntry.hwProblemExtraSpacesPinyin")));
            // Try to match up normalized pinyin with simplified Hanzi
            CedictParser parser = new CedictParser();
            CedictEntry ee = null;
            try { ee = parser.ParseEntry(trad + " " + simp + " [" + pyNorm + "] /x/", 0, null); }
            catch { }
            if (ee == null) errorsPinyin.Add(new HeadwordProblem(true, tprov.GetString(lang, "editEntry.hwProblemInvalidPinyin")));
            else
            {
                if (simp.Length == ee.ChSimpl.Length)
                {
                    for (int i = 0; i != uhiSimp.Length; ++i)
                    {
                        var uhi = uhiSimp[i];
                        var py = ee.GetPinyinAt(i);
                        var cnt = uhi.Pinyin.Count(x => x.GetDisplayString(false) == py.GetDisplayString(false));
                        if (cnt == 0)
                        {
                            msg = tprov.GetString(lang, "editEntry.hwProblemWrongPinyin");
                            msg = string.Format(msg, py.GetDisplayString(false), simp[i]);
                            errorsPinyin.Add(new HeadwordProblem(false, msg));
                        }
                    }
                }
            }
            return ee != null;
        }

        public IActionResult GetEditEntryData([FromQuery] string entryId, [FromQuery] string lang)
        {
            if (entryId == null || lang == null) return StatusCode(400, "Missing parameter(s).");

            // The data we'll return.
            EditEntryData res = new EditEntryData();

            // Is this an authenticated user?
            int userId;
            string userName;
            auth.CheckSession(HttpContext.Request.Headers, out userId, out userName);
            // Can she approve entries?
            if (userId != -1) res.CanApprove = auth.CanApprove(userId);

            // Retrieve entry
            int idVal = EntryId.StringToId(entryId);
            string hw, trg;
            EntryStatus status;
            SqlDict.GetEntryById(idVal, out hw, out trg, out status);
            CedictParser parser = new CedictParser();
            CedictEntry entry = parser.ParseEntry(hw + " " + trg, 0, null);

            res.Status = status.ToString().ToLowerInvariant();
            res.HeadSimp = entry.ChSimpl;
            res.HeadTrad = entry.ChTrad;
            res.HeadPinyin = "";
            for (int i = 0; i != entry.PinyinCount; ++i)
            {
                if (res.HeadPinyin.Length > 0) res.HeadPinyin += " ";
                var pys = entry.GetPinyinAt(i);
                res.HeadPinyin += pys.GetDisplayString(false);
            }
            res.TrgTxt = trg.Trim('/').Replace('/', '\n').Replace('\\', '/');

            // Entry HTML
            entry.Status = status;
            EntryRenderer er = new EntryRenderer(lang, entry, true, "mainEntry");
            er.OneLineHanziLimit = 12;
            StringBuilder sb = new StringBuilder();
            er.Render(sb, null);
            res.EntryHtml = sb.ToString();

            // Entry history
            List<ChangeItem> changes = SqlDict.GetEntryChanges(idVal);
            sb.Clear();
            HistoryRenderer.RenderEntryChanges(sb, hw, trg, status, changes, lang);
            res.HistoryHtml = sb.ToString();

            return new ObjectResult(res);
        }

        public IActionResult GetHistoryItem([FromQuery] string entryId, [FromQuery] string lang)
        {
            if (entryId == null || lang == null) return StatusCode(400, "Missing parameter(s).");
            int idVal = EntryId.StringToId(entryId);

            string hw, trg;
            EntryStatus status;
            SqlDict.GetEntryById(idVal, out hw, out trg, out status);
            StringBuilder sb = new StringBuilder();
            List<ChangeItem> changes = SqlDict.GetEntryChanges(idVal);
            ChangeItem ci = changes[0];
            ci.EntryBody = trg;
            ci.EntryHead = hw;
            ci.EntryStatus = status;
            HistoryRenderer.RenderItem(sb, trg, status, changes[0], lang);
            return new ObjectResult(sb.ToString());
        }

        public IActionResult GetPastChanges([FromQuery] string entryId, [FromQuery] string lang)
        {
            if (entryId == null || lang == null) return StatusCode(400, "Missing parameter(s).");
            int idVal = EntryId.StringToId(entryId);
            string hw, trg;
            EntryStatus status;
            SqlDict.GetEntryById(idVal, out hw, out trg, out status);
            var changes = SqlDict.GetEntryChanges(idVal);
            // Remove first item (most recent change). But first, backprop potential trg and status change
            if (changes[0].HeadBefore != null) hw = changes[0].HeadBefore;
            if (changes[0].BodyBefore != null) trg = changes[0].BodyBefore;
            if (changes[0].StatusBefore != 99) status = (EntryStatus)changes[0].StatusBefore;
            changes.RemoveAt(0);
            StringBuilder sb = new StringBuilder();
            HistoryRenderer.RenderPastChanges(sb, entryId, hw, trg, status, changes, lang);
            return new ObjectResult(sb.ToString());
        }

        public IActionResult CommentEntry([FromForm] string entryId, [FromForm] string note, [FromForm] string statusChange)
        {
            if (entryId == null || note == null || statusChange == null) return StatusCode(400, "Missing parameter(s).");
            // Supported/expected status changes
            SqlDict.Builder.StatusChange change;
            if (statusChange == "none") change = SqlDict.Builder.StatusChange.None;
            else if (statusChange == "approve") change = SqlDict.Builder.StatusChange.Approve;
            else if (statusChange == "flag") change = SqlDict.Builder.StatusChange.Flag;
            else if (statusChange == "unflag") change = SqlDict.Builder.StatusChange.Unflag;
            else return StatusCode(400, "Invalid statusChange parameter.");

            // Must be authenticated user
            int userId;
            string userName;
            auth.CheckSession(HttpContext.Request.Headers, out userId, out userName);
            if (userId < 0) return StatusCode(401, "Request must contain authentication token.");

            // If status change is approve: is user entitled to do it?
            bool canApprove = false;
            if (change == SqlDict.Builder.StatusChange.Approve)
            {
                canApprove = auth.CanApprove(userId);
                if (!canApprove) return StatusCode(401, "User is not authorized to approve entries.");
            }

            bool success = false;
            SqlDict.SimpleBuilder builder = null;
            try
            {
                int idVal = EntryId.StringToId(entryId);
                builder = dict.GetSimpleBuilder(userId);
                builder.CommentEntry(idVal, note, change);
                // Refresh cached contrib score
                auth.RefreshUserInfo(userId);
                success = true;
            }
            catch (Exception)
            {
                // TO-DO: Log
                //DiagLogger.LogError(ex);
            }
            finally { if (builder != null) builder.Dispose(); }

            // Tell our caller
            return new ObjectResult(success);
        }

        public IActionResult CreateEntry([FromForm] string simp, [FromForm] string trad,
            [FromForm] string pinyin, [FromForm] string trg, [FromForm] string note)
        {
            if (simp == null) return StatusCode(400, "Missing 'simp' parameter.");
            if (trad == null) return StatusCode(400, "Missing 'trad' parameter.");
            if (pinyin == null) return StatusCode(400, "Missing 'pinyin' parameter.");
            if (trg == null) return StatusCode(400, "Missing 'trg' parameter.");
            if (note == null) return StatusCode(400, "Missing 'note' parameter.");

            // Must be authenticated user
            int userId;
            string userName;
            auth.CheckSession(HttpContext.Request.Headers, out userId, out userName);
            if (userId < 0) return StatusCode(401, "Request must not contain authentication token.");

            NewEntrySubmitResult res = new NewEntrySubmitResult { Success = true };
            SqlDict.SimpleBuilder builder = null;
            try
            {
                builder = dict.GetSimpleBuilder(userId);
                CedictEntry entry = Utils.BuildEntry(simp, trad, pinyin, trg);
                builder.NewEntry(entry, note);
                // Refresh cached contrib score
                auth.RefreshUserInfo(userId);
            }
            catch (Exception)
            {
                // TO-DO: Log
                //DiagLogger.LogError(ex);
                res.Success = false;
            }
            finally { if (builder != null) builder.Dispose(); }

            // Tell our caller
            return new ObjectResult(res);
        }
    }
}

