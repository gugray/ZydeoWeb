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
            public readonly bool NoSitemap;
            public readonly string Title;
            public readonly string Keywords;
            public readonly string Description;
            public readonly string Html;
            public PageInfo(bool noIndex, bool noSitemap, string title, string keywords, string description, string html)
            {
                NoIndex = noIndex;
                NoSitemap = noSitemap;
                Title = title;
                Keywords = keywords;
                Description = description;
                Html = html;
            }
        }

        /// <summary>
        /// My own logger.
        /// </summary>
        private readonly ILogger logger;
        /// <summary>
        /// True if current hosting environment is Development.
        /// </summary>
        private readonly bool isDevelopment;
        /// <summary>
        /// ZDO mutation.
        /// </summary>
        private readonly Mutation mut;
        /// <summary>
        /// The website's base URL: used in sitemap entries.
        /// </summary>
        private readonly string baseUrl;
        /// <summary>
        /// Page cache, keyed by language / relative URLs.
        /// </summary>
        private readonly Dictionary<string, PageInfo> pageCache;

        /// <summary>
        /// Gets the site's mutation (CHD or HDD).
        /// </summary>
        public Mutation Mut { get { return mut; } }

        /// <summary>
        /// Ctor: init; load pages from plain files into cache.
        /// </summary>
        public PageProvider(ILoggerFactory lf, bool isDevelopment, Mutation mut, string baseUrl)
        {
            logger = lf.CreateLogger(GetType().FullName);
            logger.LogInformation("Page provider initializing...");
            this.isDevelopment = isDevelopment;
            this.mut = mut;
            this.baseUrl = baseUrl;
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
                if (rel == null || pi == null) continue;
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
                        // Pages we don't want showing up in sitemap:
                        // - noindex pages
                        // - explicitly marked as "nositemap"
                        // - relative URL contains "?", i.e., fetched as snippet, not as full page
                        if (pi.Value.NoIndex || pi.Value.NoSitemap || pi.Key.Contains("?")) continue;
                        string line = baseUrl + pi.Key;
                        sw.WriteLine(line);
                    }
                }
            }
        }

        /// <summary>
        /// Regex to identify/extract metainformation included in HTML files as funny SPANs.
        /// </summary>
        private readonly Regex reMetaSpan = new Regex("<div id=\"x\\-([^\"]+)\">([^<]*)<\\/div>");

        /// <summary>
        /// Loads and parses a single page.
        /// </summary>
        private PageInfo loadPage(string fileName, out string rel)
        {
            StringBuilder html = new StringBuilder();
            bool noIndex = false;
            bool noSitemap = false;
            string mutation = null;
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
                    // Regular content line
                    if (!m.Success)
                    {
                        // Replace {lang} placeholders - needed to make localization work smoothly
                        line = line.Replace("{lang}", lang);
                        html.AppendLine(line);
                        continue;
                    }
                    // Resolve special <div id="x-whatever"> directives
                    string key = m.Groups[1].Value;
                    string value = m.Groups[2].Value;
                    if (key == "mutation") mutation = value.ToLowerInvariant();
                    else if (key == "title") title = value;
                    else if (key == "title-hdd" && mut == Mutation.HDD) title = value;
                    else if (key == "title-chd" && mut == Mutation.CHD) title = value;
                    else if (key == "description") description = value;
                    else if (key == "keywords") keywords = value;
                    else if (key == "rel") rel = value;
                    else if (key == "noindex") noIndex = true;
                    else if (key == "nositemap") noIndex = true;
                    else if (key == "lang") lang = value;
                }
            }
            rel = lang + rel;
            // Wrong mutation: ignore this page
            if (mutation == "hdd" && mut != Mutation.HDD || mutation == "chd" && mut != Mutation.CHD) return null;
            return new PageInfo(noIndex, noSitemap, title, keywords, description, html.ToString());
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
                if (!rel.StartsWith("/") && !rel.StartsWith("?")) rel = "/" + rel;
            }
            string key = lang + rel;
            // Only one variant with "*" for lang?
            if (!pageCache.ContainsKey(key)) key = "*" + rel;
            // Fallback to "en"?
            if (!pageCache.ContainsKey(key)) key = "en" + rel;
            // Page or null.
            if (!pageCache.ContainsKey(key)) return null;
            PageInfo pi = pageCache[key];
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
