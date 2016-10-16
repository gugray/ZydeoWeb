using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using ZD.Common;
using ZDO.CHSite.Entities;
using ZDO.CHSite.Logic;
using ZDO.CHSite.Renderers;

namespace ZDO.CHSite.Controllers
{
    public class DynpageController : Controller
    {
        /// <summary>
        /// Provides content HTML based on relative URL of request.
        /// </summary>
        private readonly PageProvider pageProvider;

        /// <summary>
        /// Configuration.
        /// </summary>
        private readonly IConfiguration config;

        public DynpageController(PageProvider pageProvider, IConfiguration config)
        {
            this.pageProvider = pageProvider;
            this.config = config;
        }

        public IActionResult GetPage([FromQuery] string rel, [FromQuery] string lang,
            [FromQuery] string searchScript = null, [FromQuery] string searchTones = null)
        {
            return new ObjectResult(GetPageResult(rel, lang, searchScript, searchTones));
        }

        internal PageResult GetPageResult(string rel, string lang, string searchScript = null, string searchTones = null)
        {
            if (rel == null) rel = "";
            if (rel.StartsWith("search/")) return doSearch(rel, lang, searchScript, searchTones);
            if (rel.StartsWith("edit/history")) return doHistory(rel, lang);
            PageResult pr = pageProvider.GetPage(lang, rel, false);
            if (pr == null) pr = pageProvider.GetPage(lang, "404", false);
            return pr;
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
            PageResult pr = new PageResult
            {
                RelNorm = rel,
                Title = "Változások története", // TO-DO: Loca
                Html = sb.ToString(),
            };
            return pr;
        }

        private PageResult doSearch(string rel, string lang, string searchScript = null, string searchTones = null)
        {
            if (rel == "" || rel == "search/") return pageProvider.GetPage(lang, "", false);

            UiScript uiScript = UiScript.Both;
            if (searchScript == "simp") uiScript = UiScript.Simp;
            else if (searchScript == "trad") uiScript = UiScript.Trad;
            UiTones uiTones = UiTones.Pleco;
            if (searchTones == "dummitt") uiTones = UiTones.Dummitt;
            else if (searchTones == "none") uiTones = UiTones.None;

            // Perform query
            string query = rel.Replace("search/", "");
            CedictLookupResult lr;
            using (SqlDict.Query q = new SqlDict.Query())
            {
                lr = q.Lookup(query);
            }
            // No results
            if (lr.Results.Count == 0 && lr.Annotations.Count == 0)
                return pageProvider.GetPage(lang, "/?noresults", false);
            // Render results
            StringBuilder sb = new StringBuilder();
            ResultsRenderer rr = new ResultsRenderer(lr, uiScript, uiTones);
            rr.Render(sb);
            // Wrap up
            PageResult pr = new PageResult
            {
                RelNorm = rel,
                Title = "Keresési eredmények", // TO-DO: Loca
                Html = sb.ToString(),
                Data = query,
            };
            return pr;
        }
    }
}
