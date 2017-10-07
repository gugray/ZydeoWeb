using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

using ZD.Common;

namespace ZDO.ConcAlign.Controllers
{
    public class SearchController : Controller
    {
        public IActionResult Go([FromQuery] string query)
        {
            SearchResult res = new SearchResult();
            query = query.Trim();
            res.ActualQuery = query;
            if (query.Length == 0) return new ObjectResult(res);

            bool isZhoSearch = hasHanzi(query);
            SphinxResult sres = Sphinx.Query(query, isZhoSearch, 100);
            if (sres == null) return StatusCode(500);
            res.ActualQuery = sres.ActualQuery;
            using (BinReader br = new BinReader("zhhu-data.bin"))
            {
                List<float> trgHilites = new List<float>();
                List<float> srcHilites = new List<float>();
                foreach (int pos in sres.SegPositionsZh)
                {
                    br.Position = pos;
                    CorpusSegment cseg = new CorpusSegment(br);
                    if (isZhoSearch) buildHilitesZhoToHu(sres.ActualQuery, cseg, trgHilites, srcHilites);
                    else buildHilitesHuLoToZho(sres.ActualQuery, cseg, trgHilites, srcHilites);
                    res.SrcSegs.Add(renderSegment(cseg.ZhSurf, srcHilites, isZhoSearch));
                    res.TrgSegs.Add(renderSegment(cseg.TrgSurf, trgHilites, isZhoSearch));
                }
            }
            return new ObjectResult(res);
        }

        private static bool hasHanzi(string str)
        {
            foreach (char c in str)
                if ((c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3400 && c <= 0x4DFF) || (c >= 0xF900 && c <= 0xFAFF))
                    return true;
            return false;
        }

        private static bool needsEnc(char c)
        {
            return c == '<' || c == '>' || c == '&';
        }

        private static string enc(char c)
        {
            if (c == '<') return "&lt;";
            else if (c == '>') return "&gt;";
            else if (c == '&') return "&amp;";
            else return c.ToString();
        }

        private static string getHlSpan(float val)
        {
            if (val >= 0.001 && val < 0.01) return "<span class='hlLo'>";
            else if (val >= 0.01 && val < 0.03) return "<span class='hlMid'>";
            else if (val >= 0.03) return "<span class='hlHi'>";
            else return "<span>";
        }

        private string renderSegment(string surf, List<float> hls, bool isZhoSearch)
        {
            StringBuilder sb = new StringBuilder();
            float curr = 0;
            for (int i = 0; i != surf.Length; ++i)
            {
                if (hls[i] != curr)
                {
                    if (curr != 0) sb.Append("</span>");
                    if (hls[i] != 0) sb.Append(getHlSpan(hls[i]));
                }
                curr = hls[i];
                if (!needsEnc(surf[i])) sb.Append(surf[i]);
                else sb.Append(enc(surf[i]));
            }
            if (curr != 0) sb.Append("</span>");
            return sb.ToString();
        }

        private static string[] trgStops = new string[] { "a", "az", "és" };

        private void buildHilitesZhoToHu(string actualQuery, CorpusSegment cseg, List<float> thls, List<float> shls)
        {
            thls.Clear();
            for (int i = 0; i < cseg.TrgSurf.Length; ++i) thls.Add(0);
            shls.Clear();
            for (int i = 0; i < cseg.ZhSurf.Length; ++i) shls.Add(0);

            int srcStart = cseg.ZhSurf.IndexOf(actualQuery);
            // Weirdness: search text not found...
            if (srcStart == -1) return;
            int srcLen = actualQuery.Length;
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
            // Prune target tokens: isolated frequent short words
            List<int> toPrune = new List<int>();
            foreach (var x in trgToks)
            {
                // Previous or next token also hilited: no probs.
                if (trgToks.ContainsKey(x.Key - 1)) continue;
                if (trgToks.ContainsKey(x.Key + 1)) continue;
                // Longer than 2 chars: no probs.
                if (cseg.TrgTokMap[x.Key].B > 2) continue;
                // Check if it's on prohibited list
                string lo = cseg.TrgSurf.Substring(cseg.TrgTokMap[x.Key].A, cseg.TrgTokMap[x.Key].B).ToLower();
                if (Array.IndexOf(trgStops, lo) != -1) toPrune.Add(x.Key);
            }
            foreach (int ix in toPrune) trgToks.Remove(ix);
            // Indicate target highlights on matching token's characters
            // Extend highligh right if next token is also lit up
            foreach (var x in trgToks)
            {
                int start = cseg.TrgTokMap[x.Key].A;
                int len = cseg.TrgTokMap[x.Key].B;
                float score = x.Value;
                for (int pos = start; pos < start + len; ++pos) thls[pos] = score;
                if (trgToks.ContainsKey(x.Key + 1))
                {
                    // Bridge always gets lower of the two scores
                    if (trgToks[x.Key + 1] < score) score = trgToks[x.Key + 1];
                    int nextStart = cseg.TrgTokMap[x.Key + 1].A;
                    for (int pos = start + len; pos < nextStart; ++pos) thls[pos] = score;
                }
            }
            // Indicate source highlights on characters
            for (int pos = srcStart; pos < srcStart + srcLen; ++pos) shls[pos] = 1;
        }
        

        private void buildHilitesHuLoToZho(string actualQuery, CorpusSegment cseg, List<float> thls, List<float> shls)
        {
            thls.Clear();
            for (int i = 0; i < cseg.TrgSurf.Length; ++i) thls.Add(0);
            shls.Clear();
            for (int i = 0; i < cseg.ZhSurf.Length; ++i) shls.Add(0);

            int trgStart = cseg.TrgSurf.IndexOf(actualQuery, StringComparison.OrdinalIgnoreCase);
            // Weirdness: search text not found
            if (trgStart == -1) return;
            // TO-DO: Fix for intervening punctuation, multiple WS
            int trgLen = actualQuery.Length;

            // Parse alignments
            Dictionary<int, List<CorpusSegment.AlignPair>> alms = new Dictionary<int, List<CorpusSegment.AlignPair>>();
            foreach (var alm in cseg.TrgToZhAlign)
            {
                int trgIx = alm.Ix1;
                if (!alms.ContainsKey(trgIx)) alms[trgIx] = new List<CorpusSegment.AlignPair>();
                alms[trgIx].Add(alm);
            }
            // Find target tokens that overlap with query
            List<int> trgIxs = new List<int>();
            for (int i = 0; i < cseg.TrgTokMap.Length; ++i)
            {
                var ptr = cseg.TrgTokMap[i];
                bool keeper = false;
                if (ptr.A <= trgStart && ptr.A + ptr.B > trgStart) keeper = true;
                if (ptr.A < trgStart + trgLen && ptr.A + ptr.B >= trgStart + trgLen) keeper = true;
                if (keeper) trgIxs.Add(i);
            }
            // Source tokens with score: token ix -> score
            Dictionary<int, float> srcToks = new Dictionary<int, float>();
            foreach (int trgIx in trgIxs)
            {
                if (!alms.ContainsKey(trgIx)) continue;
                foreach (var almt in alms[trgIx])
                {
                    // Keep better score
                    if (!srcToks.ContainsKey(almt.Ix2)) srcToks[almt.Ix2] = almt.Score;
                    else if (almt.Score > srcToks[almt.Ix2]) srcToks[almt.Ix2] = almt.Score;
                }
            }
            // Indicate source highlights on matching token's characters
            foreach (var x in srcToks)
            {
                int start = cseg.ZhTokMap[x.Key].A;
                int len = cseg.ZhTokMap[x.Key].B;
                float score = x.Value;
                for (int pos = start; pos < start + len; ++pos) shls[pos] = score;
            }
            // Indicate target highlights on characters
            for (int pos = trgStart; pos < trgStart + trgLen; ++pos) thls[pos] = 1;
        }
    }
}
