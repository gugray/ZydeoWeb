using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net;

using Countries;
using ZDO.CHSite.Entities;
using ZDO.CHSite.Logic;
using ZD.LangUtils;

namespace ZDO.CHSite.Controllers
{
    /// <summary>
    /// Provides smarts such as prefix-based search suggestions, character animations etc.
    /// </summary>
    public class SmartsController : Controller
    {
        private readonly CountryResolver cres;
        private readonly SqlDict dict;
        private readonly LangRepo langRepo;
        private readonly QueryLogger qlog;

        /// <summary>
        /// Ctor: init controller within app environment.
        /// </summary>
        public SmartsController(CountryResolver cres, LangRepo langRepo, SqlDict dict,
            QueryLogger qlog, IConfiguration config)
        {
            this.cres = cres;
            this.langRepo = langRepo;
            this.dict = dict;
            this.qlog = qlog;
        }

        /// <summary>
        /// Returns target-language search suggestions based on query prefix.
        /// </summary>
        public IActionResult PrefixHints([FromQuery] string prefix)
        {
            List<PrefixHint> res = new List<PrefixHint>();
            if (prefix == null) return new ObjectResult(res);
            List<SqlDict.PrefixHint> hints = dict.GetWordsForPrefix(prefix, 10);
            foreach (var x in hints) res.Add(new PrefixHint { Suggestion = x.Suggestion, PrefixLength = x.PrefixLength });
            return new ObjectResult(res);
        }

        /// <summary>
        /// Returns stroke order animation, it it exists for requested character.
        /// </summary>
        public IActionResult CharStrokes([FromQuery] string hanzi)
        {
            IActionResult res;

            if (hanzi != null) hanzi = WebUtility.UrlDecode(hanzi);
            if (hanzi == null || hanzi.Length != 1)
            {
                // TO-DO: log warning
                return StatusCode(400, "A single Hanzi expected.");
            }
            HanziStrokes strokes = langRepo.GetStrokes(hanzi[0]);
            if (strokes == null) res = new ObjectResult(null);
            else
            {
                CharStrokes cstrokes = new CharStrokes();
                cstrokes.Strokes = new string[strokes.Strokes.Count];
                cstrokes.Medians = new short[strokes.Strokes.Count][][];
                for (int i = 0; i != strokes.Strokes.Count; ++i)
                {
                    var stroke = strokes.Strokes[i];
                    cstrokes.Strokes[i] = stroke.SVG;
                    cstrokes.Medians[i] = new short[stroke.Median.Count][];
                    for (int j = 0; j != stroke.Median.Count; ++j)
                    {
                        cstrokes.Medians[i][j] = new short[2];
                        cstrokes.Medians[i][j][0] = stroke.Median[j].Item1;
                        cstrokes.Medians[i][j][1] = stroke.Median[j].Item2;
                    }
                }
                res = new ObjectResult(cstrokes);
            }
            // Log this request (after resolving country code).
            string country;
            string xfwd = HttpContext.Request.Headers["X-Real-IP"];
            if (xfwd != null) country = cres.GetContryCode(IPAddress.Parse(xfwd));
            else country = cres.GetContryCode(HttpContext.Connection.RemoteIpAddress);
            qlog.LogHanzi(country, hanzi[0], strokes != null);
            // Return result
            return res;
        }

    }
}
