using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;

using ZD.Common;
using ZDO.CHSite.Entities;

namespace ZDO.CHSite.Renderers
{
    internal class EntryRenderer
    {
        private readonly bool hanim;
        private readonly string query;
        private readonly CedictResult res;
        private readonly CedictAnnotation ann;
        private readonly ICedictEntryProvider prov;
        private readonly UiScript script;
        private readonly UiTones tones;
        private readonly CedictEntry entryToRender;
        private readonly string hwTrad;
        private readonly List<PinyinSyllable> hwPinyin;
        private readonly bool dimIdenticalTrad;

        /// <summary>
        /// Maximum number of Hanzi in HW before breaking HW into two lines
        /// </summary>
        public int OneLineHanziLimit = 6;

        /// <summary>
        /// Ctor: reference entry in new entry editor
        /// </summary>
        /// <param name="entry">Entry to render.</param>
        /// <param name="hwTrad">New headword's traditional variant.</param>
        /// <param name="hwPinyin">New headword's pinyin.</param>
        public EntryRenderer(CedictEntry entry, string hwTrad, List<PinyinSyllable> hwPinyin)
        {
            this.entryToRender = entry;
            this.hwTrad = hwTrad;
            if (hwPinyin != null)
            {
                this.hwPinyin = new List<PinyinSyllable>();
                this.hwPinyin.AddRange(hwPinyin);
            }
            this.script = UiScript.Both;
            this.tones = UiTones.None;
            this.hanim = false;
            this.dimIdenticalTrad = false;
        }

        /// <summary>
        /// Ctor: dictionary entry in change history.
        /// </summary>
        public EntryRenderer(CedictEntry entry)
        {
            this.entryToRender = entry;
            this.script = UiScript.Both;
            this.tones = UiTones.None;
            this.hanim = false;
            this.dimIdenticalTrad = false;
        }

        /// <summary>
        /// Ctor: regular lookup result
        /// </summary>
        public EntryRenderer(CedictResult res, ICedictEntryProvider prov,
            UiScript script, UiTones tones)
        {
            this.res = res;
            this.prov = prov;
            this.script = script;
            this.tones = tones;
            this.hanim = true;
            this.dimIdenticalTrad = true;
        }

        /// <summary>
        /// Ctor: annotated Hanzi
        /// </summary>
        public EntryRenderer(string query, CedictAnnotation ann, ICedictEntryProvider prov,
            UiTones tones)
        {
            this.query = query;
            this.ann = ann;
            this.prov = prov;
            this.tones = tones;
            this.hanim = true;
        }

        public void Render(StringBuilder sb)
        {
            if (res != null || entryToRender != null) renderResult(sb);
            else renderAnnotation(sb);
        }

        private void renderAnnotation(StringBuilder sb)
        {
            CedictEntry entry = prov.GetEntry(ann.EntryId);
            string entryClass = "entry";
            if (tones == UiTones.Pleco) entryClass += " toneColorsPleco";
            else if (tones == UiTones.Dummitt) entryClass += " toneColorsDummitt";
            sb.Append("div class='" + entryClass + "'>"); // <div class="entry">

            sb.Append("<span class='hw-simp'>");// <span class="hw-simp">
            renderHanzi(query, entry, ann.StartInQuery, ann.LengthInQuery, sb);
            sb.Append("</span>"); // <span class="hw-ann">

            sb.Append("<span class='hw-pinyin'>");// <span class="hw-pinyin">
            bool firstSyll = true;
            foreach (var pinyin in entry.Pinyin)
            {
                if (!firstSyll) sb.Append(" ");
                firstSyll = false;
                sb.Append(HtmlEncoder.Default.Encode(pinyin.GetDisplayString(true)));
            }
            sb.Append("</span>"); // <span class="hw-pinyin">

            sb.Append("<div class='senses'>"); // <div class="senses">
            for (int i = 0; i != entry.SenseCount; ++i)
                renderSense(sb, entry.GetSenseAt(i), i, null);
            sb.Append("</div>"); // <div class="senses">

            sb.Append("</div>"); // <div class="entry">
        }

        /// <summary>
        /// Render HW's Hanzi in annotation mode
        /// </summary>
        private void renderHanzi(string query, CedictEntry entry, int annStart, int annLength, StringBuilder sb)
        {
            for (int i = 0; i != query.Length; ++i)
            {
                char c = query[i];
                PinyinSyllable py = null;
                if (i >= annStart && i < annStart + annLength)
                {
                    int pyIx = entry.HanziPinyinMap[i - annStart];
                    if (pyIx != -1) py = entry.Pinyin[pyIx];
                }
                // Class to put on hanzi
                string cls = "";
                // We mark up tones if needed
                if (tones != UiTones.None && py != null)
                {
                    if (py.Tone == 1) cls = "tone1";
                    else if (py.Tone == 2) cls = "tone2";
                    else if (py.Tone == 3) cls = "tone3";
                    else if (py.Tone == 4) cls = "tone4";
                    // -1 for unknown, and 0 for neutral: we don't mark up anything
                }
                // Whatever's outside annotation is faint
                if (i < annStart || i >= annStart + annLength) cls += " faint";
                // Mark up character for stroke order animation
                if (hanim) cls += " hanim";
                // Render with enclosing span if we have a relevant class
                if (!string.IsNullOrEmpty(cls))
                {
                    sb.Append("<span class='" + cls + "'>");
                }
                sb.Append(HtmlEncoder.Default.Encode(c.ToString()));
                if (!string.IsNullOrEmpty(cls)) sb.Append("</span");
            }
        }

        private void renderResult(StringBuilder sb)
        {
            CedictEntry entry = entryToRender;
            if (entry == null) entry = prov.GetEntry(res.EntryId);

            Dictionary<int, CedictTargetHighlight> senseHLs = new Dictionary<int, CedictTargetHighlight>();
            if (res != null)
                foreach (CedictTargetHighlight hl in res.TargetHilites)
                    senseHLs[hl.SenseIx] = hl;

            string entryClass = "entry";
            if (tones == UiTones.Pleco) entryClass += " toneColorsPleco";
            else if (tones == UiTones.Dummitt) entryClass += " toneColorsDummitt";
            sb.Append("<div class='" + entryClass + "'>"); // <div class="entry">

            if (script != UiScript.Trad)
            {
                sb.Append("<span class='hw-simp'>"); // <span class="hw-simp">
                renderHanzi(entry, true, false, sb);
                sb.Append("</span>"); // <span class="hw-simp">
            }
            if (script == UiScript.Both)
            {
                // Up to N hanzi: on a single line
                if (entry.ChSimpl.Length <= OneLineHanziLimit)
                {
                    string clsSep = "hw-sep";
                    if (tones != UiTones.None) clsSep = "hw-sep faint";
                    sb.Append("<span class='" + clsSep + "'>"); // <span class="hw-sep">
                    sb.Append("•");
                    sb.Append("</span>"); // <span class="hw-sep">
                }
                // Otherwise, line break
                else sb.Append("<bs/>");
            }
            if (script != UiScript.Simp)
            {
                string clsTrad = "hw-trad";
                // Need special class so traditional floats left after line break
                if (script == UiScript.Both && entry.ChSimpl.Length > OneLineHanziLimit)
                    clsTrad = "hw-trad break";
                sb.Append("<span class='" + clsTrad + "'>"); // <span class="hw-trad">
                renderHanzi(entry, false, dimIdenticalTrad && script == UiScript.Both, sb);
                sb.Append("</span>"); // <span class="hw-trad">
            }
            sb.Append("<span class='hw-pinyin'>"); // <span class="hw-pinyin">
            bool firstSyll = true;
            foreach (var pinyin in entry.Pinyin)
            {
                if (!firstSyll) sb.Append(" ");
                firstSyll = false;
                sb.Append(HtmlEncoder.Default.Encode(pinyin.GetDisplayString(true)));
            }
            sb.Append("</span>"); // <span class="hw-pinyin">

            sb.Append("<div class='senses'>"); // <div class="senses">
            for (int i = 0; i != entry.SenseCount; ++i)
            {
                CedictTargetHighlight thl = null;
                if (senseHLs.ContainsKey(i)) thl = senseHLs[i];
                renderSense(sb, entry.GetSenseAt(i), i, thl);
            }
            sb.Append("</div>"); // <div class="senses">

            sb.Append("</div>"); // <div class="entry">
        }

        /// <summary>
        /// Render HW's Hanzi in normal lookup result
        /// </summary>
        private void renderHanzi(CedictEntry entry, bool simp, bool faintIdentTrad, StringBuilder sb)
        {
            string hzStr = simp ? entry.ChSimpl : entry.ChTrad;
            for (int i = 0; i != hzStr.Length; ++i)
            {
                char c = hzStr[i];
                int pyIx = entry.HanziPinyinMap[i];
                PinyinSyllable py = null;
                if (pyIx != -1) py = entry.Pinyin[pyIx];
                // Class to put on hanzi
                string cls = "";
                // We mark up tones if needed
                if (tones != UiTones.None && py != null)
                {
                    if (py.Tone == 1) cls = "tone1";
                    else if (py.Tone == 2) cls = "tone2";
                    else if (py.Tone == 3) cls = "tone3";
                    else if (py.Tone == 4) cls = "tone4";
                    // -1 for unknown, and 0 for neutral: we don't mark up anything
                }
                // If we're rendering both scripts, then show faint traditional chars where same as simp
                if (faintIdentTrad && c == entry.ChSimpl[i]) cls += " faint";
                // Mark up character for stroke order animation
                if (hanim) cls += " hanim";
                // Render with enclosing span if we have a relevant class
                if (!string.IsNullOrEmpty(cls))
                {
                    sb.Append("<span class='" + cls + "'>");
                }
                sb.Append(HtmlEncoder.Default.Encode(c.ToString()));
                if (!string.IsNullOrEmpty(cls)) sb.Append("</span>");
            }
        }

        private static string[] ixStrings = new string[]
        {
            "1", "2", "3", "4", "5", "6", "7", "8", "9",
            "a", "b", "c", "d", "e", "f", "g", "h", "i",
            "j", "k", "l", "m", "n", "o", "p", "q", "r",
            "s", "t", "u", "v", "w", "x", "y", "z"
        };

        private static string getIxString(int ix)
        {
            return ixStrings[ix % 35];
        }

        private static string[] splitFirstWord(string str)
        {
            int i = str.IndexOf(' ');
            if (i == -1) return new string[] { str };
            string[] res = new string[2];
            res[0] = str.Substring(0, i);
            res[1] = str.Substring(i);
            return res;
        }

        private void renderSense(StringBuilder sb, CedictSense sense, int ix, CedictTargetHighlight hl)
        {
            sb.Append("<span class='sense'>"); // <span class="sense">
            sb.Append("<span class='sense-nobr'>"); // <span class="sense-nobr">
            sb.Append("<span class='sense-ix'>"); // <span class="sense-ix">
            sb.Append(getIxString(ix));
            sb.Append("</span>"); // <span class="sense-ix">
            sb.Append(" ");

            bool needToSplit = true;
            string domain = sense.Domain.GetPlainText();
            string equiv = sense.Equiv.GetPlainText();
            string note = sense.Note.GetPlainText();
            if (domain != string.Empty)
            {
                sb.Append("<span class='sense-meta'>");
                string[] firstAndRest = splitFirstWord(domain);
                sb.Append(HtmlEncoder.Default.Encode(firstAndRest[0]));
                sb.Append("</span>"); // sense-meta
                sb.Append("</span>"); // sense-nobr
                if (firstAndRest.Length > 1)
                {
                    sb.Append("<span class='sense-meta'>");
                    sb.Append(HtmlEncoder.Default.Encode(firstAndRest[1]));
                    sb.Append("</span>"); // sense-meta
                }
                needToSplit = false;
            }
            if (equiv != string.Empty)
            {
                if (domain != string.Empty) sb.Append(" ");
                renderEquiv(sb, sense.Equiv, hl, needToSplit);
                needToSplit = false;
            }
            if (note != string.Empty)
            {
                if (domain != string.Empty || equiv != string.Empty) sb.Append(" ");
                sb.Append("<span class='sense-meta'>");
                if (needToSplit)
                {
                    string[] firstAndRest = splitFirstWord(note);
                    sb.Append(HtmlEncoder.Default.Encode(firstAndRest[0]));
                    sb.Append("</span>"); // sense-meta
                    sb.Append("</span>"); // sense-nobr
                    if (firstAndRest.Length > 1)
                    {
                        sb.Append("<span class='sense-meta'>");
                        sb.Append(HtmlEncoder.Default.Encode(firstAndRest[1]));
                        sb.Append("</span>"); // sense-meta
                    }
                    needToSplit = false;
                }
                else
                {
                    sb.Append(HtmlEncoder.Default.Encode(note));
                    sb.Append("</span>"); // sense-meta
                }
            }

            sb.Append("</span>"); // <span class="sense">
        }

        private class HybridTextConsumer
        {
            private readonly HybridText txt;
            private readonly CedictTargetHighlight hl;
            private int runIX = 0;
            private int runPos = 0;
            private string runTxt;
            public HybridTextConsumer(HybridText txt, CedictTargetHighlight hl)
            {
                this.txt = txt;
                this.hl = hl;
                runTxt = txt.GetRunAt(0).GetPlainText();
            }
            public void GetNext(out char c, out bool inHL)
            {
                if (runPos >= runTxt.Length)
                {
                    ++runIX;
                    runPos = 0;
                    if (runIX < txt.RunCount) runTxt = txt.GetRunAt(runIX).GetPlainText();
                    else runTxt = null;
                }
                if (runTxt == null)
                {
                    c = (char)0;
                    inHL = false;
                    return;
                }
                c = runTxt[runPos];
                if (hl == null || hl.RunIx != runIX) inHL = false;
                else inHL = runPos >= hl.HiliteStart && runPos < hl.HiliteStart + hl.HiliteLength;
                ++runPos;
            }
            public bool IsNextSpaceInHilite()
            {
                int nextSpaceIX = -1;
                for (int i = runIX; i < runTxt.Length; ++i)
                {
                    if (runTxt[i] == ' ') { nextSpaceIX = i; break; }
                }
                if (nextSpaceIX == -1) return false;
                return nextSpaceIX >= hl.HiliteStart && nextSpaceIX < hl.HiliteStart + hl.HiliteLength;
            }
        }

        private void renderEquiv(StringBuilder sb, HybridText equiv, CedictTargetHighlight hl, bool nobr)
        {
            HybridTextConsumer htc = new HybridTextConsumer(equiv, hl);
            bool firstWordOver = false;
            bool hlOn = false;
            char c;
            bool inHL;
            while (true)
            {
                htc.GetNext(out c, out inHL);
                if (c == (char)0) break;
                // Highlight starts?
                if (inHL && !hlOn)
                {
                    // Very first word gets special highlight if hilite goes beyond first space, and we're in nobr mode
                    if (!firstWordOver && nobr && htc.IsNextSpaceInHilite())
                    {
                        sb.Append("<span class='sense-hl-start'>");
                    }
                    // Plain old hilite start everywhere else
                    else
                    {
                        sb.Append("<span class='sense-hl'>");
                    }
                    hlOn = true;
                }
                // Highlight ends?
                else if (!inHL && hlOn)
                {
                    sb.Append("</span>");
                    hlOn = false;
                }
                // Space - close "nobr" span if first word's just over
                if (c == ' ' && !firstWordOver && nobr)
                {
                    firstWordOver = true;
                    sb.Append("</span>");
                    if (hlOn)
                    {
                        sb.Append("</span>");
                        sb.Append("<span class='sense-hl-end'>");
                    }
                }
                // Render character
                sb.Append(HtmlEncoder.Default.Encode(c.ToString()));
            }
            // Close hilite and nobr that we may have open
            if (!firstWordOver && nobr) sb.Append("</span>");
            if (hlOn) sb.Append("</span>");
        }
   }
}