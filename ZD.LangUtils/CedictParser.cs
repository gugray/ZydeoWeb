using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

using ZD.Common;

namespace ZD.LangUtils
{
    /// <summary>
    /// Parses Cedict text file and compiles indexed dictionary in binary format.
    /// </summary>
    public partial class CedictParser
    {
        /// <summary>
        /// Ctor: initialize.
        /// </summary>
        public CedictParser()
        {
        }

        /// <summary>
        /// Parse a single entry. Return null if rejected for whatever reason.
        /// </summary>
        /// <param name="line">Line to parse.</param>
        /// <param name="lineNum">Line number in input.</param>
        /// <param name="swLog">Stream to log warnings. Can be null.</param>
        /// <param name="swDrop">Stream to record dropped entries (failed to parse). Can be null.</param>
        public CedictEntry ParseEntry(string line, int lineNum, StreamWriter swLog)
        {
            // Cannot handle code points about 0xffff
            if (!surrogateCheck(line, swLog, lineNum)) return null;
            // Sanitization and initial split
            string strHead, strBody;
            // Initial split: header vs body
            int firstSlash = line.IndexOf('/');
            strHead = line.Substring(0, firstSlash - 1);
            strBody = line.Substring(firstSlash);
            // Parse entry. If failed > null.
            CedictEntry entry = null;
            try { entry = parseEntry(strHead, strBody, swLog, lineNum); }
            catch (Exception ex)
            {
                string msg = "Line {0}: ERROR: {1}: {2}";
                msg = string.Format(msg, lineNum, ex.GetType().Name, ex.Message);
                if (swLog != null) swLog.WriteLine(msg);
            }
            return entry;
        }

        /// <summary>
        /// Verifies that line contains no Unicode surrogates. Needed for data hygiene if input is dirty.
        /// </summary>
        private static bool surrogateCheck(string line, StreamWriter logStream, int lineNum)
        {
            bool surrFound = false;
            foreach (char c in line)
            {
                int val = (int)c;
                if (val >= 0xd800 && val <= 0xdfff) { surrFound = true; break; }
            }
            if (!surrFound) return true;
            if (logStream != null)
            {
                string msg = "Line {0}: ERROR: Unicode surrogate found";
                msg = string.Format(msg, lineNum);
                logStream.WriteLine(msg);
            }
            return false;
        }

        /// <summary>
        /// Decomposes headword: hanzi and pinyin.
        /// </summary>
        private Regex reHead = new Regex(@"^([^ ]+) ([^ ]+) \[([^\]]+)\]$");

        /// <summary>
        /// Parses an entry (line) that has been separated into headword and rest.
        /// </summary>
        private CedictEntry parseEntry(string strHead, string strBody, StreamWriter logStream, int lineNum)
        {
            // Decompose head
            Match hm = reHead.Match(strHead);
            if (!hm.Success)
            {
                string msg = "Line {0}: ERROR: Invalid header syntax: {1}";
                msg = string.Format(msg, lineNum, strHead);
                if (logStream != null) logStream.WriteLine(msg);
                return null;
            }

            // Split pinyin by spaces
            string[] pinyinParts = hm.Groups[3].Value.Split(new char[] { ' ' });

            // Convert pinyin to our normalized format
            PinyinSyllable[] pinyinSylls;
            List<int> pinyinMap;
            normalizePinyin(pinyinParts, out pinyinSylls, out pinyinMap);
            //// Weird syllables found > warning
            //if (Array.FindIndex(pinyinSylls, x => x.Tone == -1) != -1)
            //{
            //    string msg = "Line {0}: Warning: Weird pinyin syllable: {1}";
            //    msg = string.Format(msg, lineNum, strHead);
            //    if (logStream != null) logStream.WriteLine(msg);
            //}
            // Trad and simp MUST have same # of chars, always
            if (hm.Groups[1].Value.Length != hm.Groups[2].Value.Length)
            {
                string msg = "Line {0}: ERROR: Trad/simp char count mismatch: {1}";
                msg = string.Format(msg, lineNum, strHead);
                if (logStream != null) logStream.WriteLine(msg);
                return null;
            }
            // Transform map so it says, for each hanzi, which pinyin syllable it corresponds to
            // Some chars in hanzi may have no pinyin: when hanzi includes a non-ideagraphic character
            short[] hanziToPinyin = transformPinyinMap(hm.Groups[1].Value, pinyinMap);
            // Headword MUST have same number of ideo characters as non-weird pinyin syllables
            if (hanziToPinyin == null)
            {
                string msg = "Line {0}: ERROR: Failed to match hanzi to pinyin: {1}";
                msg = string.Format(msg, lineNum, strHead);
                if (logStream != null) logStream.WriteLine(msg);
                return null;
            }
            // Check for trailing slash
            if (!strBody.EndsWith("/"))
            {
                string msg = "Line {0}: ERROR: Missing trailing slash";
                msg = string.Format(msg, lineNum);
                if (logStream != null) logStream.WriteLine(msg);
                return null;
            }
            // Split meanings by slash
            if (strBody.Length < 3 || strBody[0] != '/' || strBody[strBody.Length - 1] != '/')
            {
                string msg = "Line {0}: ERROR: Invalid body (too short, or has no trailing/ending slash): {1}";
                msg = string.Format(msg, lineNum, strBody);
                if (logStream != null) logStream.WriteLine(msg);
                return null;
            }
            string[] senses = strBody.Substring(1, strBody.Length - 2).Split(new char[] { '/' });
            bool hasEmptySense = false;
            for (int i = 0; i != senses.Length; ++i) if (senses[i] == "") hasEmptySense = true;
            if (hasEmptySense)
            {
                string msg = "Line {0}: Warning: Empty sense in entry: {1}";
                msg = string.Format(msg, lineNum, strBody);
                if (logStream != null) logStream.WriteLine(msg);
            }
            // Separate domain, equiv and note in each sense
            List<CedictSense> cedictSenses = new List<CedictSense>();
            for (int i = 0; i != senses.Length; ++i)
            {
                string s = senses[i];
                string domain, equiv, note;
                parseSense(s, out domain, out equiv, out note);
                // TO-DO: Recognize embedded Chinese
                cedictSenses.Add(new CedictSense(domain, equiv, note));

                ////// Equiv is empty: merits at least a warning
                ////if (equiv == "")
                ////{
                ////    string msg = "Line {0}: Warning: No equivalent in sense, only domain/notes: {1}";
                ////    msg = string.Format(msg, lineNum, s);
                ////    if (logStream != null) logStream.WriteLine(msg);
                ////}
                //// Convert all parts of sense to hybrid text
                //HybridText hDomain = plainTextToHybrid(domain, lineNum, logStream);
                //HybridText hEquiv = plainTextToHybrid(equiv, lineNum, logStream);
                //HybridText hNote = plainTextToHybrid(note, lineNum, logStream);
                //// Store new sense - unless we failed to parse anything properly
                //if (hDomain != null && hEquiv != null && hNote != null)
                //{
                //    cedictSenses.Add(new CedictSense(hDomain, hEquiv, hNote));
                //}
            }
            // If there are no senses, we failed. But that will have been logged before, so just return null.
            if (cedictSenses.Count == 0) return null;
            // Done with entry
            CedictEntry res = new CedictEntry(hm.Groups[2].Value, hm.Groups[1].Value,
                new ReadOnlyCollection<PinyinSyllable>(pinyinSylls),
                new ReadOnlyCollection<CedictSense>(cedictSenses),
                hanziToPinyin, null);
            return res;
        }

        /// <summary>
        /// Returns true if character is ideographic (Hanzi).
        /// </summary>
        private static bool isIdeo(char c)
        {
            // VERY rough "definition" but if works for out purpose
            int cval = (int)c;
            return cval >= 0x2e80;
        }

        /// <summary>
        /// Returns true if string has ideographic (Hanzi) characters.
        /// </summary>
        private static bool hasIdeo(string str)
        {
            foreach (char c in str)
                if (isIdeo(c)) return true;
            return false;
        }

        /// <summary>
        /// <para>Receives hanzi and pinyin map from <see cref="normalizePinyin"/>.</para>
        /// <para>Returns list as long as hanzi. Each number in list is info for a hanzi,</para>
        /// <para>identifying the corresponding pinyin syllable.</para>
        /// <para>Non-ideo chars in hanzi have no pinyin syllable.</para>
        /// </summary>
        private static short[] transformPinyinMap(string hanzi, List<int> mapIn)
        {
            if (hanzi.Length >= short.MaxValue || mapIn.Count >= short.MaxValue)
                throw new Exception("Hanzi too long, or too many pinyin syllables.");

            short[] res = new short[hanzi.Length];
            short ppos = 0; // position in incoming map; that map has an entry for each normal pinyin syllable
            for (int i = 0; i != hanzi.Length; ++i)
            {
                char c = hanzi[i];
                // Character is not ideographic: no corresponding pinyin
                if (!isIdeo(c))
                {
                    res[i] = -1;
                    continue;
                }
                // We have run out of pinyin map: BAD
                if (ppos >= mapIn.Count) return null;
                // We've got this hanzi's pinyin syllable
                res[i] = (short)mapIn[ppos];
                ++ppos;
            }
            // At this stage we must have consumed incoming map
            // Otherwise: BAD
            if (ppos != mapIn.Count) return null;
            return res;
        }

        /// <summary>
        /// Delegate so we can access <see cref="normalizePinyin"/> in embedded classes.
        /// </summary>
        private delegate void NormalizePinyinDelegate(string[] parts, out PinyinSyllable[] syllsArr, out List<int> pinyinMap);

        /// <summary>
        /// Normalizes array of Cedict-style pinyin syllables into our format.
        /// </summary>
        private void normalizePinyin(string[] parts, out PinyinSyllable[] syllsArr, out List<int> pinyinMap)
        {
            // What this function does:
            // - Separates tone mark from text (unless it's a "weird" syllable
            // - Replaces "u:" with "v"
            // - Replaces "ü" with "v"
            // - Maps every non-weird input syllable to r5-merged output syllables
            //   List has as many values as there are non-weird input syllables
            //   Values in list point into "sylls" output array
            //   Up to two positions can have same value (for r5 appending)
            pinyinMap = new List<int>();
            List<PinyinSyllable> sylls = new List<PinyinSyllable>();
            foreach (string ps in parts)
            {
                // Does not end with a tone mark (1 thru 5): weird
                char chrLast = ps[ps.Length - 1];
                if (chrLast < '1' || chrLast > '5')
                {
                    sylls.Add(new PinyinSyllable(ps, -1));
                    continue;
                }
                // Separate tone and text
                string text = ps.Substring(0, ps.Length - 1);
                int tone = ((int)chrLast) - ((int)'0');
                // Neutral tone for us is 0, not five
                if (tone == 5) tone = 0;
                // "u:" is for us "v"
                // "ü" is for us "v"
                text = text.Replace("u:", "v");
                text = text.Replace("U:", "V");
                text = text.Replace("ü", "v");
                text = text.Replace("Ü", "V");
                // Store new syllable
                sylls.Add(new PinyinSyllable(text, tone));
                // Add to map
                pinyinMap.Add(sylls.Count - 1);
            }
            // Result: the syllables as an array.
            syllsArr = sylls.ToArray();
        }

        /// <summary>
        /// <para>Parses one sense, to separate domain, equivalent, and note.</para>
        /// <para>In input, sense comes like this, with domain/note optional:</para>
        /// <para>(domain) (domain) equiv, equiv, equiv (note) (note)</para>
        /// </summary>
        private static void parseSense(string sense, out string domain, out string equiv, out string note)
        {
            // TO-DO: Handle all special cases, including classifier
            //// Special case: sense starts with "CL:"
            //// --> This is a classifier. Put "CL:" in domain and leave only Chinese in equiv
            //if (sense.StartsWith("CL:"))
            //{
            //    equiv = sense.Substring(3);
            //    domain = "CL:";
            //    note = "";
            //    return;
            //}
            // Array with parenthesis depths and content/non-content flags for chars in sense
            // -1: WS or parenthesis
            // 0 or greater: parenthesis depth
            int[] flags = new int[sense.Length];
            int depth = 0;
            for (int i = 0; i != sense.Length; ++i)
            {
                char c = sense[i];
                if (char.IsWhiteSpace(c)) flags[i] = -1;
                else if (c == '(')
                {
                    flags[i] = -1;
                    ++depth;
                }
                else if (c == ')')
                {
                    flags[i] = -1;
                    --depth;
                }
                else flags[i] = depth;
            }
            // Find first char that is depth 0, from left
            int equivStart = -1;
            for (int i = 0; i != flags.Length; ++i)
            {
                if (flags[i] == 0)
                {
                    equivStart = i;
                    break;
                }
            }
            // No real equiv, just domain
            if (equivStart == -1)
            {
                domain = sense;
                equiv = note = "";
                return;
            }
            domain = sense.Substring(0, equivStart);
            // Find first char that is depth 0, from right
            int equivEnd = -1;
            for (int i = flags.Length - 1; i >= 0; --i)
            {
                if (flags[i] == 0)
                {
                    equivEnd = i;
                    break;
                }
            }
            // Cannot be -1: we found at least one depth=0 char before
            // No note
            if (equivEnd == flags.Length - 1)
            {
                equiv = sense.Substring(equivStart);
                note = "";
                return;
            }
            equiv = sense.Substring(equivStart, equivEnd - equivStart + 1);
            note = sense.Substring(equivEnd + 1);
        }
    }
}
