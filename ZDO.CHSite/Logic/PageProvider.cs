using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

using ZDO.CHSite.Entities;

namespace ZDO.CHSite.Logic
{
    /// <summary>
    /// Provides dynamic HTML for single-page app's URL-specific elements.
    /// </summary>
    public class PageProvider
    {
        /// <summary>
        /// Matches <see cref="Entities.PageResult"/>, but we don't want to cross-pollute implementation and shared entities. 
        /// </summary>
        public class PageInfo
        {
            public readonly bool NoIndex;
            public readonly string Title;
            public readonly string Keywords;
            public readonly string Description;
            public readonly string Html;
            public PageInfo(bool noIndex, string title, string keywords, string description, string html)
            {
                NoIndex = noIndex;
                Title = title;
                Keywords = keywords;
                Description = description;
                Html = html;
            }
        }

        /// <summary>
        /// The website's base URL: used in sitemap entries.
        /// </summary>
        private const string baseUrl = "https://no.doma.in";

        /// <summary>
        /// My own logger.
        /// </summary>
        private readonly ILogger logger;
        /// <summary>
        /// True if current hosting environment is Development.
        /// </summary>
        private readonly bool isDevelopment;
        /// <summary>
        /// Page cache, keyed by language / relative URLs.
        /// </summary>
        private readonly Dictionary<string, PageInfo> pageCache;

        /// <summary>
        /// Ctor: init; load pages from plain files into cache.
        /// </summary>
        public PageProvider(ILoggerFactory lf, bool isDevelopment)
        {
            logger = lf.CreateLogger(GetType().FullName);
            logger.LogInformation("Page provider initializing...");
            this.isDevelopment = isDevelopment;
            pageCache = new Dictionary<string, PageInfo>();
            initPageCache();
            logger.LogInformation("Page provider initialized.");
        }

        /// <summary>
        /// Loads all pages into cache.
        /// </summary>
        private void initPageCache()
        {
            // Recreate entire cache
            pageCache.Clear();
            var files = Directory.EnumerateFiles("./files/html");
            foreach (var fn in files)
            {
                string name = Path.GetFileName(fn);
                if (!name.EndsWith(".html")) continue;
                string rel;
                PageInfo pi = loadPage(fn, out rel);
                if (rel == null) continue;
                pageCache[rel] = pi;
            }
            // If running in development env, recreate sitemap
            if (isDevelopment)
            {
                using (FileStream fs = new FileStream("wwwroot/sitemap.txt", FileMode.Create, FileAccess.Write))
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    foreach (var pi in pageCache)
                    {
                        if (pi.Value.NoIndex) continue;
                        string line = baseUrl + pi.Key;
                        sw.WriteLine(line);
                    }
                }
            }
        }

        /// <summary>
        /// Regex to identify/extract metainformation included in HTML files as funny SPANs.
        /// </summary>
        private readonly Regex reMetaSpan = new Regex("<span id=\"x\\-([^\"]+)\">([^<]*)<\\/span>");

        /// <summary>
        /// Loads and parses a single page.
        /// </summary>
        private PageInfo loadPage(string fileName, out string rel)
        {
            StringBuilder html = new StringBuilder();
            bool noIndex = false;
            string title = string.Empty;
            string description = string.Empty;
            string keywords = string.Empty;
            string lang = "*";
            rel = null;
            using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            using (StreamReader sr = new StreamReader(fs))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    Match m = reMetaSpan.Match(line);
                    if (!m.Success)
                    {
                        html.AppendLine(line);
                        continue;
                    }
                    string key = m.Groups[1].Value;
                    if (key == "title") title = m.Groups[2].Value;
                    else if (key == "description") description = m.Groups[2].Value;
                    else if (key == "keywords") keywords = m.Groups[2].Value;
                    else if (key == "rel") rel = m.Groups[2].Value;
                    else if (key == "noindex") noIndex = true;
                    else if (key == "lang") lang = m.Groups[2].Value;
                }
            }
            rel = lang + "/" + rel;
            return new PageInfo(noIndex, title, keywords, description, html.ToString());
        }

        /// <summary>
        /// Returns a page by relative URL, or null if not present.
        /// </summary>
        public PageResult GetPage(string lang, string rel, bool direct)
        {
            // At development, we reload entire cache with each request so HTML files can be edited on the fly.
            if (isDevelopment) initPageCache();

            // A bit or normalization on relative URL.
            if (rel == null) rel = "/";
            else
            {
                rel = rel.TrimEnd('/');
                if (rel == string.Empty) rel = "/";
                if (!rel.StartsWith("/")) rel = "/" + rel;
            }
            string key = lang + "/" + rel;
            // Only page with "*" for lang?
            if (!pageCache.ContainsKey(key)) key = "*/" + rel;
            // Page or null.
            if (!pageCache.ContainsKey(key)) return null;
            PageInfo pi = pageCache[key];
            // If page only allows direct requests, but current request is in-page: null
            if (pi.NoIndex && !direct) return null;
            PageResult pr = new PageResult
            {
                NoIndex = pi.NoIndex,
                RelNorm = rel,
                Title = pi.Title,
                Description = pi.Description,
                Keywords = pi.Keywords,
                Html = pi.Html,
            };
            return pr;
        }
    }
}
