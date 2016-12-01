using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Collections.Generic;
using System.Net;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Countries;
using ZD.Common;
using ZDO.CHSite.Entities;
using ZDO.CHSite.Logic;
using ZDO.CHSite.Renderers;

namespace ZDO.CHSite.Controllers
{
    public class DynpageController : Controller
    {
        /// <summary>
        /// Relative URLs that are never returned as "static" initial content.
        /// </summary>
        private readonly static string[] dynOnlyPrefixes = new string[]
        {
            "search", "edit/history", "edit/new", "edit/existing", "user"
        };

        private readonly CountryResolver cres;
        private readonly PageProvider pageProvider;
        private readonly SqlDict dict;
        private readonly IConfiguration config;
        private readonly ILogger logger;
        private readonly QueryLogger qlog;
        private readonly Auth auth;

        /// <summary>
        /// Ctor: Infuse app services.
        /// </summary>
        /// <remarks>
        /// Default null values make controller accessible to <see cref="IndexController"/>.
        /// That way, functionality is limited to serving static pages.
        /// </remarks>
        public DynpageController(PageProvider pageProvider, IConfiguration config, ILoggerFactory loggerFactory,
            Auth auth, CountryResolver cres = null, SqlDict dict = null, QueryLogger qlog = null)
        {
            this.cres = cres;
            this.pageProvider = pageProvider;
            this.dict = dict;
            this.qlog = qlog;
            this.config = config;
            this.logger = loggerFactory.CreateLogger("DynpageController");
            this.auth = auth;
        }

        /// <summary>
        /// Invoked by single-page app to request current page content.
        /// </summary>
        public IActionResult GetPage([FromQuery] string rel, [FromQuery] string lang, [FromQuery] bool isMobile = false,
            [FromQuery] string searchScript = null, [FromQuery] string searchTones = null)
        {
            return new ObjectResult(GetPageResult(lang, rel, true, isMobile, searchScript, searchTones));
        }

        internal PageResult GetPageResult(string lang, string rel, bool dynamic, bool isMobile,
            string searchScript = null, string searchTones = null)
        {
            if (rel == null) rel = "";
            // Dynamic-only pages
            bool isDynOnly = false;
            foreach (var x in dynOnlyPrefixes) if (rel.StartsWith(x)) { isDynOnly = true; break; }
            if (isDynOnly)
            {
                if (dynamic)
                {
                    if (rel.StartsWith("search/")) return doSearch(rel, lang, searchScript, searchTones, isMobile);
                    else if (rel.StartsWith("edit/history")) return doHistory(rel, lang);
                    else if (rel.StartsWith("edit/new")) return doNewEntry(rel, lang);
                    else if (rel.StartsWith("edit/existing")) return doEditExisting(rel, lang);
                    else if (rel.StartsWith("user/confirm/")) return doUserConfirm(rel, lang);
                    else if (rel.StartsWith("user/users")) return doUserList(rel, lang, isMobile);
                    else if (rel.StartsWith("user/profile")) return doUserProfile(rel, lang);
                }
                else
                {
                    PageResult xpr = pageProvider.GetPage(lang, "404", false);
                    xpr.Html = xpr.Keywords = xpr.Title = xpr.Description = "";
                    return xpr;
                }
            }
            PageResult pr = pageProvider.GetPage(lang, rel, false);
            if (pr == null) pr = pageProvider.GetPage(lang, "404", false);
            return pr;
        }

        private PageResult doUserProfile(string rel, string lang)
        {
            int userId;
            string userName;
            auth.CheckSession(HttpContext.Request.Headers, out userId, out userName);
            if (userId < 0) return pageProvider.GetPage(lang, "?/privatepage", false);

            Auth.UserInfo ui = auth.GetUserInfo(userId);
            string registered = Utils.ChinesDateStr(ui.Registered);
            PageResult res = pageProvider.GetPage(lang, "?/profile", false);
            res.Html = string.Format(res.Html,
                HtmlEncoder.Default.Encode(userName),
                HtmlEncoder.Default.Encode(registered),
                HtmlEncoder.Default.Encode("0"),
                HtmlEncoder.Default.Encode(ui.Email),
                HtmlEncoder.Default.Encode(ui.Location),
                HtmlEncoder.Default.Encode(ui.About)
                );
            return res;
        }

        private static int cmpUser(Auth.UserInfo x, Auth.UserInfo y)
        {
            // Larger contribs first
            int cmpContrib = y.ContribScore.CompareTo(x.ContribScore);
            if (cmpContrib != 0) return cmpContrib;
            // Later registered first
            return y.Registered.CompareTo(y.Registered);
        }

        private PageResult doUserList(string rel, string lang, bool isMobile)
        {
            //PageResult res = pageProvider.GetPage(lang, "?/userlist-doodle", false);
            //return res;
            List<Auth.UserInfo> users = auth.GetAllUsers();
            users.Sort(cmpUser);
            StringBuilder sb = new StringBuilder();
            UserListRenderer.Render(sb, lang, users, isMobile);
            PageResult res = pageProvider.GetPage(lang, "?/userlist", false);
            res.Html = sb.ToString();
            return res;
        }

        /// <summary>
        /// Interprets and applies email confirmation code (part of relative URI).
        /// </summary>
        private PageResult doUserConfirm(string rel, string lang)
        {
            string code = rel.Replace("user/confirm/", "");
            Auth.ConfirmedAction action;
            string data;
            int userId;
            auth.CheckTokenCode(code, out action, out data, out userId);
            // Invalid link?
            if (userId == -1 || action == Auth.ConfirmedAction.Bad) return pageProvider.GetPage(lang, "?/badcode", false);
            // Confirm registration
            if (action == Auth.ConfirmedAction.Register)
            {
                if (auth.ConfirmCreateUser(code, userId))
                    return pageProvider.GetPage(lang, "?/registrationconfirmed", false);
                else return pageProvider.GetPage(lang, "?/badcode", false);
            }
            // Confirm new email
            else if (action == Auth.ConfirmedAction.ChangeEmail)
            {
                if (auth.ConfirmChangeEmail(code, userId, data))
                    return pageProvider.GetPage(lang, "?/emailconfirmed", false);
                else return pageProvider.GetPage(lang, "?/badcode", false);
            }
            // Reset password
            else if (action == Auth.ConfirmedAction.PassReset)
            {
                // This has in-page interaction; return that page
                PageResult res = pageProvider.GetPage(lang, "?/passreset", false);
                // Infuse confirmation code as data attribute
                res.Html = string.Format(res.Html, code);
                return res;
            }
            // Others: unexpected
            else return pageProvider.GetPage(lang, "404", false);
        }

        private PageResult doEditExisting(string rel, string lang)
        {
            string entryId = rel.Replace("edit/existing/", "");
            entryId = WebUtility.UrlDecode(entryId);
            int idVal = -1;
            try { idVal = EntryId.StringToId(entryId); }
            catch { }
            if (idVal == -1)
            {
                return pageProvider.GetPage(lang, "?/editnothing", false);
            }

            PageResult pr = pageProvider.GetPage(lang, "?/editexisting", false);
            StringBuilder sb = new StringBuilder(pr.Html);
            var t = TextProvider.Instance;
            sb = sb.Replace("{{entry-id}}", HtmlEncoder.Default.Encode(entryId));
            pr.Html = sb.ToString();
            return pr;
        }

        private PageResult doNewEntry(string rel, string lang)
        {
            int userId;
            string userName;
            auth.CheckSession(HttpContext.Request.Headers, out userId, out userName);
            if (userId < 0) return pageProvider.GetPage(lang, "?/privatepage", false);
            else return pageProvider.GetPage(lang, "?/newentry", false);
        }

        private PageResult doHistory(string rel, string lang)
        {
            // Page size: from config
            int histPageSize = int.Parse(config["historyPageSize"]);

            // Page ID from URL
            int histPageIX = -1;
            rel = rel.Replace("edit/history", "");
            if (rel.StartsWith("/")) rel = rel.Substring(1);
            rel = rel.Replace("page-", "");
            if (rel != "") int.TryParse(rel, out histPageIX);
            if (histPageIX == -1) histPageIX = 0;
            else --histPageIX; // Humans count from 1

            // Retrieve data from DB
            int histPageCount;
            List<ChangeItem> histChanges;
            using (SqlDict.History hist = new SqlDict.History())
            {
                histPageCount = hist.GetChangeCount() / histPageSize + 1;
                histChanges = hist.GetChangePage(histPageIX * histPageSize, histPageSize);
            }
            // Render
            StringBuilder sb = new StringBuilder();
            HistoryRenderer hr = new HistoryRenderer(lang, histPageSize, histPageIX, histPageCount, histChanges);
            hr.Render(sb);
            // Wrap up
            PageResult pr = pageProvider.GetPage(lang, "?/history", false);
            pr.Html = sb.ToString();
            return pr;
        }

        private PageResult doSearch(string rel, string lang, string searchScript, string searchTones, bool isMobile)
        {
            string query = "";
            try
            {
                var res = doSearchInner(rel, lang, searchScript, searchTones, isMobile, out query);
                if (query == "Gasherd")
                {
                    GC.AddMemoryPressure(256000000);
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                    GC.RemoveMemoryPressure(256000000);
                }
                if (Debugger.IsAttached && query == "throw") throw new Exception("Test error.");
                return res;
            }
            catch (Exception ex)
            {
                logger.LogError(new EventId(), ex, "Lookup failed; query: \"" + query + "\"",
                    rel, lang, searchScript, searchTones, isMobile);
                throw;
            }
        }

        private PageResult doSearchInner(string rel, string lang, string searchScript, string searchTones, 
            bool isMobile, out string query)
        {
            query = "";
            if (rel == "" || rel == "search/") return pageProvider.GetPage(lang, "", false);
            PageResult pr;
            Stopwatch swatch = new Stopwatch();
            swatch.Restart();

            // Search settings
            UiScript uiScript = UiScript.Both;
            if (searchScript == "simp") uiScript = UiScript.Simp;
            else if (searchScript == "trad") uiScript = UiScript.Trad;
            UiTones uiTones = UiTones.Pleco;
            if (searchTones == "dummitt") uiTones = UiTones.Dummitt;
            else if (searchTones == "none") uiTones = UiTones.None;

            // Perform query
            query = rel.Replace("search/", "");
            query = query.Trim();
            query = WebUtility.UrlDecode(query);
            CedictLookupResult lr = dict.Lookup(query);
            int msecLookup = (int)swatch.ElapsedMilliseconds;
            int msecFull = msecLookup;
            // No results
            if (lr.Results.Count == 0 && lr.Annotations.Count == 0)
            {
                pr = pageProvider.GetPage(lang, "?/noresults", false);
                pr.Data = query;
            }
            else
            {
                // Render results
                StringBuilder sb = new StringBuilder();
                ResultsRenderer rr = new ResultsRenderer(lr, uiScript, uiTones);
                rr.Render(sb, lang);
                // Title
                string title;
                if (lr.ActualSearchLang == SearchLang.Chinese)
                {
                    title = TextProvider.Instance.Mut == Mutation.CHD ?
                    TextProvider.Instance.GetString(lang, "misc.searchResultTitleCHD") :
                    TextProvider.Instance.GetString(lang, "misc.searchResultTitleHDD");
                }
                else
                {
                    title = TextProvider.Instance.Mut == Mutation.CHD ?
                    TextProvider.Instance.GetString(lang, "misc.searchResultTitleTrgCHD") :
                    TextProvider.Instance.GetString(lang, "misc.searchResultTitleTrgHDD");
                }
                title = string.Format(title, query);
                msecFull = (int)swatch.ElapsedMilliseconds;
                // Wrap up
                pr = new PageResult
                {
                    RelNorm = rel,
                    Title = title,
                    Html = sb.ToString(),
                    Data = query,
                };
            }
            // Query log
            string country;
            string xfwd = HttpContext.Request.Headers["X-Real-IP"];
            if (xfwd != null) country = cres.GetContryCode(IPAddress.Parse(xfwd));
            else country = cres.GetContryCode(HttpContext.Connection.RemoteIpAddress);
            int resCount = lr.Results.Count > 0 ? lr.Results.Count : lr.Annotations.Count;
            QueryLogger.SearchMode smode = QueryLogger.SearchMode.Target;
            if (lr.ActualSearchLang == SearchLang.Target) smode = QueryLogger.SearchMode.Target;
            else if (lr.Results.Count > 0) smode = QueryLogger.SearchMode.Source;
            else if (lr.Annotations.Count > 0) smode = QueryLogger.SearchMode.Annotate;
            qlog.LogQuery(country, isMobile, lang, uiScript, uiTones, resCount, msecLookup, msecFull, smode, query);
            // Done
            return pr;
        }
    }
}
