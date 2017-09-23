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
            query = query.Trim();
            if (query.Length == 0)
                return new ObjectResult(res);

            List<SphinxResult> srs = Sphinx.Query(query, 100);
            foreach (var sr in srs)
            {
                SearchHit hit = new SearchHit();
                hit.Source = sr.Zh;
                hit.Target = sr.Hu;
                // !! DBG
                //hit.Target = sr.HuTok;
                hit.SrcHiStart = hit.Source.IndexOf(query);
                hit.SrcHiLen = query.Length;
                hit.TrgHilights = buildHilites(hit.SrcHiStart, hit.SrcHiLen, sr);
                res.Add(hit);
            }
            return new ObjectResult(res);
        }

        private struct Almt
        {
            public int TrgTokIx;
            public double Score;
        }

        private List<TrgHi> buildHilites(int srcStart, int srcLen, SphinxResult sr)
        {
            List<TrgHi> res = new List<TrgHi>();
            string[] parts;
            // Parse ZH token map
            List<int[]> zhTokMap = new List<int[]>();
            parts = sr.ZhTokMap.Split(' ');
            foreach (string part in parts)
            {
                string[] subs = part.Split('/');
                zhTokMap.Add(new int[] { int.Parse(subs[0]), int.Parse(subs[1]) });
            }
            // Parse HU token map
            List<int[]> huTokMap = new List<int[]>();
            parts = sr.HuTokMap.Split(' ');
            foreach (string part in parts)
            {
                string[] subs = part.Split('/');
                huTokMap.Add(new int[] { int.Parse(subs[0]), int.Parse(subs[1]) });
            }
            // Parse alignments
            Dictionary<int, List<Almt>> alms = new Dictionary<int, List<Almt>>();
            parts = sr.Align.Split(' ');
            foreach (string part in parts)
            {
                int pos1 = part.IndexOf('-');
                int pos2 = part.IndexOf('!');
                int srcIx = int.Parse(part.Substring(0, pos1));
                int trgIX = int.Parse(part.Substring(pos1 + 1, pos2 - pos1 - 1));
                double score = double.Parse(part.Substring(pos2 + 1));
                Almt almt = new Almt { TrgTokIx = trgIX, Score = score };
                if (!alms.ContainsKey(srcIx)) alms[srcIx] = new List<Almt>();
                alms[srcIx].Add(almt);
            }
            // Find source tokens that overlap with query
            List<int> srcIxs = new List<int>();
            for (int i = 0; i < zhTokMap.Count; ++i)
            {
                int[] ptr = zhTokMap[i];
                bool keeper = false;
                if (ptr[0] <= srcStart && ptr[0] + ptr[1] > srcStart) keeper = true;
                if (ptr[0] < srcStart + srcLen && ptr[0] + ptr[1] >= srcStart + srcLen) keeper = true;
                if (keeper) srcIxs.Add(i);
            }
            // Target tokens with score: token ix -> score
            Dictionary<int, double> trgToks = new Dictionary<int, double>();
            foreach (int srcIx in srcIxs)
            {
                if (!alms.ContainsKey(srcIx)) continue;
                foreach (Almt almt in alms[srcIx])
                {
                    // Keep better score
                    if (!trgToks.ContainsKey(almt.TrgTokIx)) trgToks[almt.TrgTokIx] = almt.Score;
                    else if (almt.Score > trgToks[almt.TrgTokIx]) trgToks[almt.TrgTokIx] = almt.Score;
                }
            }

            //// !! DBG
            //// Highlights for char ranges, if display text is tokenized target
            //huTokMap.Clear();
            //parts = sr.HuTok.Split(' ');
            //string recon = "";
            //foreach (string part in parts)
            //{
            //    if (recon != "") recon += ' ';
            //    huTokMap.Add(new int[] { recon.Length, part.Length });
            //    recon += part;
            //}

            // Produce highlights for character ranges
            foreach (var x in trgToks)
            {
                //if (x.Key >= huTokMap.Count) continue; // ???
                TrgHi trgHi = new TrgHi
                {
                    Start = huTokMap[x.Key][0],
                    Len = huTokMap[x.Key][1],
                    Score = (int)(x.Value * 10000),
                };
                res.Add(trgHi);
            }
            return res;
        }
    }
}
