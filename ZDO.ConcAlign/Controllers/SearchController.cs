using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.IO;

using ZD.Common;

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

            SphinxResult sres = Sphinx.Query(query, true, 100);
            if (sres == null) return StatusCode(500);
            using (BinReader br = new BinReader("zhhu-data.bin"))
            {
                foreach (int pos in sres.SegPositionsZh)
                {
                    br.Position = pos;
                    CorpusSegment cseg = new CorpusSegment(br);
                    SearchHit hit = new SearchHit();
                    hit.Source = cseg.ZhSurf;
                    hit.Target = cseg.TrgSurf;
                    hit.SrcHiStart = hit.Source.IndexOf(query);
                    hit.SrcHiLen = query.Length;
                    hit.TrgHilights = buildHilites(hit.SrcHiStart, hit.SrcHiLen, cseg);
                    res.Add(hit);
                }
            }
            return new ObjectResult(res);
        }

        private List<TrgHi> buildHilites(int srcStart, int srcLen, CorpusSegment cseg)
        {
            List<TrgHi> res = new List<TrgHi>();
            // Parse alignments
            Dictionary<int, List<CorpusSegment.AlignPair>> alms = new Dictionary<int, List<CorpusSegment.AlignPair>>();
            foreach (var alm in cseg.ZhToTrgAlign)
            {
                int srcIx = alm.Ix1;
                if (!alms.ContainsKey(srcIx)) alms[srcIx] = new List<CorpusSegment.AlignPair>();
                alms[srcIx].Add(alm);
            }
            // Find source tokens that overlap with query
            List<int> srcIxs = new List<int>();
            for (int i = 0; i < cseg.ZhTokMap.Length; ++i)
            {
                var ptr = cseg.ZhTokMap[i];
                bool keeper = false;
                if (ptr.A <= srcStart && ptr.A + ptr.B > srcStart) keeper = true;
                if (ptr.A < srcStart + srcLen && ptr.A + ptr.B >= srcStart + srcLen) keeper = true;
                if (keeper) srcIxs.Add(i);
            }
            // Target tokens with score: token ix -> score
            Dictionary<int, float> trgToks = new Dictionary<int, float>();
            foreach (int srcIx in srcIxs)
            {
                if (!alms.ContainsKey(srcIx)) continue;
                foreach (var almt in alms[srcIx])
                {
                    // Keep better score
                    if (!trgToks.ContainsKey(almt.Ix2)) trgToks[almt.Ix2] = almt.Score;
                    else if (almt.Score > trgToks[almt.Ix2]) trgToks[almt.Ix2] = almt.Score;
                }
            }
            // Produce highlights for character ranges
            foreach (var x in trgToks)
            {
                //if (x.Key >= huTokMap.Count) continue; // ???
                TrgHi trgHi = new TrgHi
                {
                    Start = cseg.TrgTokMap[x.Key].A,
                    Len = cseg.TrgTokMap[x.Key].B,
                    Score = (int)(x.Value * 10000),
                };
                res.Add(trgHi);
            }
            return res;
        }
    }
}
