using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net;

using ZDO.CHSite.Entities;
using ZDO.CHSite.Logic;
using ZDO.CHSite.Renderers;
using ZD.Common;
using ZD.LangUtils;

namespace ZDO.CHSite.Controllers
{
    /// <summary>
    /// Provides smarts such as prefix-based search suggestions, character animations etc.
    /// </summary>
    public class SmartsController : Controller
    {
        /// <summary>
        /// SQL dictionary engine.
        /// </summary>
        private readonly SqlDict dict;
        /// <summary>
        /// Provides stroke order animations.
        /// </summary>
        private readonly LangRepo langRepo;

        /// <summary>
        /// Ctor: init controller within app environment.
        /// </summary>
        public SmartsController(LangRepo langRepo, SqlDict dict, IConfiguration config)
        {
            this.langRepo = langRepo;
            this.dict = dict;
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
            if (hanzi != null) hanzi = WebUtility.UrlDecode(hanzi);
            if (hanzi == null || hanzi.Length != 1) return StatusCode(400, "A single Hanzi expected.");
            HanziStrokes strokes = langRepo.GetStrokes(hanzi[0]);
            if (strokes == null) return new ObjectResult(null);
            CharStrokes res = new CharStrokes();
            res.Strokes = new string[strokes.Strokes.Count];
            res.Medians = new short[strokes.Strokes.Count][][];
            for (int i = 0; i != strokes.Strokes.Count; ++i)
            {
                var stroke = strokes.Strokes[i];
                res.Strokes[i] = stroke.SVG;
                res.Medians[i] = new short[stroke.Median.Count][];
                for (int j = 0; j != stroke.Median.Count; ++j)
                {
                    res.Medians[i][j] = new short[2];
                    res.Medians[i][j][0] = stroke.Median[j].Item1;
                    res.Medians[i][j][1] = stroke.Median[j].Item2;
                }
            }
            return new ObjectResult(res);
        }

    }
}
