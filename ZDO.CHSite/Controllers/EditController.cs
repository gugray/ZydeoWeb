using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
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

        public IActionResult CommentEntry([FromForm] string entryId, [FromForm] string note)
        {
            if (entryId == null || note == null) return StatusCode(400, "Missing parameter(s).");

            // Must be authenticated user
            int userId;
            string userName;
            auth.CheckSession(HttpContext.Request.Headers, out userId, out userName);
            if (userId < 0) return StatusCode(401, "Request must not contain authentication token.");

            bool success = false;
            SqlDict.SimpleBuilder builder = null;
            try
            {
                int idVal = EntryId.StringToId(entryId);
                builder = dict.GetSimpleBuilder(userId);
                builder.CommentEntry(idVal, note);
                success = true;
            }
            catch (Exception ex)
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
            }
            catch (Exception ex)
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
