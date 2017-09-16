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
            using (FileStream fs = new FileStream("set.txt", FileMode.Open, FileAccess.Read))
            using (StreamReader sr = new StreamReader(fs))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line == string.Empty) continue;
                    SearchHit hit = new SearchHit();
                    string[] parts = line.Split('\t');
                    string[] srcToks = parts[0].Split(' ');
                    foreach (var tok in srcToks) hit.SrcTokens.Add(tok);
                    string[] trgToks = parts[1].Split(' ');
                    foreach (var tok in trgToks) hit.TrgTokens.Add(tok);
                    string[] pairs = parts[2].Split(' ');
                    foreach (var pair in pairs)
                    {
                        string[] a2b = pair.Split('-');
                        int[] mapItem = new int[] { int.Parse(a2b[0]), int.Parse(a2b[1]) };
                        hit.Map.Add(mapItem);
                    }
                    res.Add(hit);
                }
            }
            return new ObjectResult(res);
        }
    }
}
