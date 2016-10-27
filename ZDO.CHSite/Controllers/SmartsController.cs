using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

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
    public class SmartsController
    {
        /// <summary>
        /// SQL dictionary engine.
        /// </summary>
        private readonly SqlDict dict;

        /// <summary>
        /// Ctor: init controller within app environment.
        /// </summary>
        public SmartsController(SqlDict dict, IConfiguration config)
        {
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
    }
}
