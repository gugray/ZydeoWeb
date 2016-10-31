using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Mvc;

using ZDO.CHSite.Entities;
using ZDO.CHSite.Logic;
using ZDO.CHSite.Renderers;
using ZD.Common;
using ZD.LangUtils;

namespace ZDO.CHSite.Controllers
{
    public class NewEntryController : Controller
    {
        // TO-DO: text provider. For that, we need to have session and know language.
        private static readonly string errSpacePunct = "A címszó nem tartalmazhat szóközöket vagy központozást";
        private static readonly string errNotHanzi = "Nem írásjegy, nagybetű vagy számjegy:";
        private static readonly string errUnsupported = "Nem támogatott írásjegy:";
        private static readonly string errNotSimp = "Nem egyszerűsített írásjegy:";


        private readonly LangRepo langRepo;
        private readonly SqlDict dict;

        public NewEntryController(LangRepo langRepo, SqlDict dict)
        {
            this.langRepo = langRepo;
            this.dict = dict;
        }

        private static void addIfNew(List<string> lst, string item)
        {
            if (!lst.Contains(item)) lst.Add(item);
        }

        /// <summary>
        /// Retrieves information about (simplified) hanzi.
        /// </summary>
        public IActionResult ProcessSimp([FromQuery] string simp)
        {
            if (simp == null) simp = "";
            NewEntryProcessSimpResult res = new NewEntryProcessSimpResult();

            // Prepare result: as long as input; empty array for each position
            foreach (char c in simp)
            {
                res.Trad.Add(new List<string>());
                res.Pinyin.Add(new List<string>());
            }

            // Do we have CEDICT headwords for this simplified HW?
            // If yes, put first headword's traditional and pinyin into first layer of result
            // Fill rest of the alternatives with input from additional results
            HeadwordSyll[][] chHeads = langRepo.GetPossibleHeadwords(simp, false);
            for (int i = 0; i != chHeads.Length; ++i)
            {
                HeadwordSyll[] sylls = chHeads[i];
                for (int j = 0; j != simp.Length; ++j)
                {
                    addIfNew(res.Trad[j], sylls[j].Trad.ToString());
                    addIfNew(res.Pinyin[j], sylls[j].Pinyin);
                }
            }
            // Unihan lookup
            UniHanziInfo[] uhis = langRepo.GetUnihanInfo(simp);
            // We had no headword: build from Unihan data, but with a twist
            // Make sure first traditional matches most common pinyin
            if (chHeads.Length == 0)
            {
                for (int i = 0; i != uhis.Length; ++i)
                {
                    UniHanziInfo uhi = uhis[i];
                    if (uhi == null) continue;
                    // Add pinyin readings first
                    foreach (PinyinSyllable syll in uhi.Pinyin) addIfNew(res.Pinyin[i], syll.GetDisplayString(true));
                    // Look up traditional chars for this position
                    UniHanziInfo[] tradUhis = langRepo.GetUnihanInfo(uhi.TradVariants);
                    // Find "best" traditional character: the first one whose pinyin readings include our first pinyin
                    char firstTrad = (char)0;
                    string favoritePinyin = uhi.Pinyin[0].GetDisplayString(true);
                    if (tradUhis != null)
                    {
                        for (int tx = 0; tx != uhi.TradVariants.Length; ++tx)
                        {
                            UniHanziInfo tradUhi = tradUhis[tx];
                            if (tradUhi == null) continue;
                            bool hasFavoritePinyin = false;
                            foreach (PinyinSyllable py in tradUhi.Pinyin)
                            {
                                if (py.GetDisplayString(true) == favoritePinyin)
                                {
                                    hasFavoritePinyin = true;
                                    break;
                                }
                            }
                            if (hasFavoritePinyin)
                            {
                                firstTrad = uhi.TradVariants[tx];
                                break;
                            }
                        }
                    }
                    // Add first traditional, if found
                    if (firstTrad != (char)0) addIfNew(res.Trad[i], firstTrad.ToString());
                    // Add all the remaining traditional variants
                    foreach (char c in uhi.TradVariants) addIfNew(res.Trad[i], c.ToString());
                }
            }
            // We had a headword: fill remaining slots with traditional and pinyin items from Unihan
            else
            {
                res.IsKnownHeadword = true;
                for (int i = 0; i != uhis.Length; ++i)
                {
                    UniHanziInfo uhi = uhis[i];
                    if (uhi == null) continue;
                    foreach (char c in uhi.TradVariants) addIfNew(res.Trad[i], c.ToString());
                    foreach (PinyinSyllable syll in uhi.Pinyin) addIfNew(res.Pinyin[i], syll.GetDisplayString(true));
                }
            }
            // Filter pinyin: only keep those that work with traditional on the first spot
            // Unless intersection is empty - can also happen in this weird world
            for (int i = 0; i != simp.Length; ++i)
            {
                List<string> pyList = res.Pinyin[i];
                if (pyList.Count < 2) continue;
                List<string> tradList = res.Trad[i];
                if (tradList.Count == 0) continue;
                List<string> toRem = new List<string>();
                UniHanziInfo[] tradUhis = langRepo.GetUnihanInfo(new char[] { tradList[0][0] });
                if (tradUhis == null || tradUhis[0] == null) continue;
                List<string> pinyinsOfTrad = new List<string>();
                foreach (var x in tradUhis[0].Pinyin) pinyinsOfTrad.Add(x.GetDisplayString(true));
                // If we had a match, start from second: don't want to remove what just came from CEDICT
                for (int j = res.IsKnownHeadword ? 1 : 0; j < pyList.Count; ++j)
                {
                    string py = pyList[j];
                    if (!pinyinsOfTrad.Contains(py)) toRem.Add(py);
                }
                if (toRem.Count == pyList.Count) continue;
                foreach (string py in toRem) pyList.Remove(py);
            }

            // Check if there are positions where we have no tradition or pinyin
            // For the purposes of this lookup, we just inject character from input there
            for (int i = 0; i != simp.Length; ++i)
            {
                char c = simp[i];
                if (res.Trad[i].Count == 0) res.Trad[i].Add(c.ToString());
                if (res.Pinyin[i].Count == 0) res.Pinyin[i].Add(c.ToString());
            }

            // Tell our caller
            return new ObjectResult(res);
        }

        /// <summary>
        /// Verifies if string can serve as a simplified headword.
        /// </summary>
        public IActionResult VerifySimp([FromQuery] string simp)
        {
            if (simp == null) return StatusCode(400, "Missing 'simp' parameter.");
            NewEntryVerifySimpResult res = new NewEntryVerifySimpResult();
            UniHanziInfo[] uhis = langRepo.GetUnihanInfo(simp);

            // Has WS or punctuation
            bool hasSpaceOrPunct = false;
            // Chars that are neither hanzi nor A-Z0-9
            List<char> notHanziOrLD = new List<char>();
            // Unsupported hanzi: no Unihan info
            List<char> unsupportedHanzi = new List<char>();
            // Not simplified
            List<char> notSimp = new List<char>();

            // Check each character
            for (int i = 0; i != simp.Length; ++i)
            {
                char c = simp[i];
                UniHanziInfo uhi = uhis[i];

                // Space or punct
                if (char.IsWhiteSpace(c) || char.IsPunctuation(c)) { hasSpaceOrPunct = true; continue; }
                // Is it even a Hanzi or A-Z0-9?
                bool isHanziOrLD = Utils.IsHanzi(c) || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
                if (!isHanziOrLD) { notHanziOrLD.Add(c); continue; }
                // No info?
                if (uhi == null) { unsupportedHanzi.Add(c); continue; }
                // Cannot be simplified?
                if (!uhi.CanBeSimp) { notSimp.Add(c); continue; }
            }

            // Passed or not
            if (!hasSpaceOrPunct && notHanziOrLD.Count == 0 && unsupportedHanzi.Count == 0 && notSimp.Count == 0)
                res.Passed = true;
            else
            {
                // Compile our errors
                res.Passed = false;
                res.Errors = new List<string>();
                if (hasSpaceOrPunct) res.Errors.Add(errSpacePunct);
                if (notHanziOrLD.Count != 0)
                {
                    string err = errNotHanzi;
                    foreach (char c in notHanziOrLD) { err += ' '; err += c; }
                    res.Errors.Add(err);
                }
                if (unsupportedHanzi.Count != 0)
                {
                    string err = errUnsupported;
                    foreach (char c in unsupportedHanzi) { err += ' '; err += c; }
                    res.Errors.Add(err);
                }
                if (notSimp.Count != 0)
                {
                    string err = errNotSimp;
                    foreach (char c in notSimp) { err += ' '; err += c; }
                    res.Errors.Add(err);
                }
            }

            // Tell our caller
            return new ObjectResult(res);
        }

        public IActionResult VerifyHead([FromQuery] string simp, [FromQuery] string trad, [FromQuery] string pinyin)
        {
            if (simp == null) return StatusCode(400, "Missing 'simp' parameter.");
            if (trad == null) return StatusCode(400, "Missing 'trad' parameter.");
            if (pinyin == null) return StatusCode(400, "Missing 'pinyin' parameter.");

            NewEntryVerifyHeadResult res = new NewEntryVerifyHeadResult();
            res.Passed = true;

            // DBG
            if (simp == "大家" || simp == "污染") res.Passed = false;

            // Prepare pinyin as list of proper syllables
            List<PinyinSyllable> pyList = new List<PinyinSyllable>();
            string[] pyRawArr = pinyin.Split(' ');
            foreach (string pyRaw in pyRawArr) pyList.Add(PinyinSyllable.FromDisplayString(pyRaw));

            // Return all entries, CEDICT and HanDeDict, rendered as HTML
            CedictEntry[] ced, hdd;
            langRepo.GetEntries(simp, out ced, out hdd);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div id='newEntryRefCED'>");
            foreach (CedictEntry entry in ced)
            {
                EntryRenderer er = new EntryRenderer(entry, trad, pyList);
                er.Render(sb);
            }
            sb.Append("</div>");
            sb.Append("<div id='newEntryRefHDD'>");
            foreach (CedictEntry entry in hdd)
            {
                EntryRenderer er = new EntryRenderer(entry, trad, pyList);
                er.Render(sb);
            }
            sb.Append("</div>");
            res.RefEntries = sb.ToString();

            // Tell our caller
            return new ObjectResult(res);
        }

        public IActionResult VerifyFull([FromQuery] string simp, [FromQuery] string trad,
            [FromQuery] string pinyin, [FromQuery] string trg)
        {
            // Mucho TO-DO in this action:
            // - Escape slashes in senses
            // - Proper checking for all sorts of stuff

            if (simp == null) return StatusCode(400, "Missing 'simp' parameter.");
            if (trad == null) return StatusCode(400, "Missing 'trad' parameter.");
            if (pinyin == null) return StatusCode(400, "Missing 'pinyin' parameter.");
            if (trg == null) return StatusCode(400, "Missing 'trg' parameter.");

            NewEntryVerifyFullResult res = new NewEntryVerifyFullResult();
            res.Passed = true;

            CedictEntry entry = Utils.BuildEntry(simp, trad, pinyin, trg);
            StringBuilder sb = new StringBuilder();
            EntryRenderer er = new EntryRenderer(entry, null, null);
            er.Render(sb);
            res.Preview = sb.ToString();

            // Tell our caller
            return new ObjectResult(res);
        }

        public IActionResult ProcessSimpTrad([FromQuery] string simp, [FromQuery] string trad)
        {
            if (simp == null) return StatusCode(400, "Missing 'simp' parameter.");
            if (trad == null) return StatusCode(400, "Missing 'trad' parameter.");
            if (simp.Length != trad.Length) return StatusCode(400, "'simp' and 'trad' must be of equal length.");
            NewEntryProcessSimpTradResult res = new NewEntryProcessSimpTradResult();

            // Prepare result: as long as input; empty array for each position
            foreach (char c in simp)
            {
                res.Pinyin.Add(new List<string>());
            }

            // Do we have a CEDICT headword with this simplified and traditional?
            // If yes, fill in pinyin from these
            HeadwordSyll[][] chHeads = langRepo.GetPossibleHeadwords(simp, false);
            for (int i = 0; i != chHeads.Length; ++i)
            {
                HeadwordSyll[] sylls = chHeads[i];
                bool matches = true;
                for (int j = 0; j != trad.Length; ++j)
                {
                    if (sylls[j].Simp != simp[j] || sylls[j].Trad != trad[j])
                    { matches = false; break; }
                }
                if (matches)
                {
                    res.IsKnownHeadword = true;
                    for (int j = 0; j != trad.Length; ++j)
                        addIfNew(res.Pinyin[j], sylls[j].Pinyin);
                }
            }
            // At each position, add missing pinyins that match both simplified and traditional
            UniHanziInfo[] suhis = langRepo.GetUnihanInfo(simp);
            UniHanziInfo[] tuhis = langRepo.GetUnihanInfo(trad);
            for (int i = 0; i != simp.Length; ++i)
            {
                UniHanziInfo suhi = suhis[i];
                UniHanziInfo tuhi = tuhis[i];
                string[] spyarr = new string[suhi.Pinyin.Length];
                for (int j = 0; j != suhi.Pinyin.Length; ++j) spyarr[j] = suhi.Pinyin[j].GetDisplayString(true);
                string[] tpyarr = new string[tuhi.Pinyin.Length];
                for (int j = 0; j != tuhi.Pinyin.Length; ++j) tpyarr[j] = tuhi.Pinyin[j].GetDisplayString(true);
                foreach (string py in spyarr)
                {
                    if (Array.IndexOf(tpyarr, py) >= 0) addIfNew(res.Pinyin[i], py);
                }
            }

            // Check if there are positions where we have no tradition or pinyin
            // For the purposes of this lookup, we just inject character from input there
            for (int i = 0; i != simp.Length; ++i)
            {
                char c = simp[i];
                if (res.Pinyin[i].Count == 0) res.Pinyin[i].Add(c.ToString());
            }

            // Tell our caller
            return new ObjectResult(res);
        }

        public IActionResult Submit([FromForm] string simp, [FromForm] string trad,
            [FromForm] string pinyin, [FromForm] string trg, [FromForm] string note)
        {
            if (simp == null) return StatusCode(400, "Missing 'simp' parameter.");
            if (trad == null) return StatusCode(400, "Missing 'trad' parameter.");
            if (pinyin == null) return StatusCode(400, "Missing 'pinyin' parameter.");
            if (trg == null) return StatusCode(400, "Missing 'trg' parameter.");
            if (note == null) return StatusCode(400, "Missing 'note' parameter.");

            NewEntrySubmitResult res = new NewEntrySubmitResult { Success = true };
            SqlDict.SimpleBuilder builder = null;
            try
            {
                builder = dict.GetSimpleBuilder(0);
                CedictEntry entry = Utils.BuildEntry(simp, trad, pinyin, trg);
                builder.NewEntry(entry, note);
            }
            catch (Exception ex)
            {
                // TO-DO: Log
                //DiagLogger.LogError(ex);
                res.Success = false;
            }
            finally
            {
                if (builder != null) builder.Dispose();
            }

            // Tell our caller
            return new ObjectResult(res);
        }
    }
}
