using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.IO;

namespace ZDO.ConcAlign.Controllers
{
    public class SearchController : Controller
    {
        public IActionResult Go([FromQuery] string query)
        {
            List<SearchHit> res = new List<SearchHit>();
            List<SphinxResult> srs = Sphinx.Query(query, 100);
            foreach (var sr in srs)
            {
                SearchHit hit = new SearchHit();
                hit.Source = sr.Zh;
                hit.Target = sr.Hu;
                res.Add(hit);
            }
            return new ObjectResult(res);
        }
    }
}
