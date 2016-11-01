using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using ZD.Common;

namespace ZDO.CHSite.Logic
{
    partial class SqlDict
    {
        public class Pinyin
        {
            /// <summary>
            /// Info about a single pinyin syllable for splitting words written w/o spaces
            /// </summary>
            private class PinyinParseSyllable
            {
                /// <summary>
                /// Syllable text (no tone mark, but may include trailing r)
                /// </summary>
                public readonly string Text;
                /// <summary>
                /// True if syllable starts with a vowel (cannot be inside word: apostrophe would be needed)
                /// </summary>
                public readonly bool VowelStart;
                /// <summary>
                /// Ctor: initialize immutable instance.
                /// </summary>
                public PinyinParseSyllable(string text, bool vowelStart)
                {
                    Text = text;
                    VowelStart = vowelStart;
                }
            }

            /// <summary>
            /// List of known pinyin syllables; longer first.
            /// </summary>
            private List<PinyinParseSyllable> syllList = new List<PinyinParseSyllable>();

            private readonly int maxId;

            public int MaxId { get { return maxId; } }

            private Dictionary<string, int> pyToId = new Dictionary<string, int>();

            public Pinyin()
            {
                // Syllable-to-ID
                int maxId = 0;
                Assembly a = typeof(DB).GetTypeInfo().Assembly;
                string fileName = "ZDO.CHSite.files.other.std-pinyin.txt";
                using (Stream s = a.GetManifestResourceStream(fileName))
                using (StreamReader sr = new StreamReader(s))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        string[] parts = line.Split('\t');
                        if (parts.Length != 2) continue;
                        int id = int.Parse(parts[0]);
                        if (id > maxId) maxId = id;
                        pyToId[parts[1]] = id;
                    }
                }
                this.maxId = maxId;
                // Syllables for interpreting query
                using (Stream s = a.GetManifestResourceStream("ZDO.CHSite.files.other.syllabary-pinyin.txt"))
                using (StreamReader sr = new StreamReader(s))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line == string.Empty) continue;
                        string[] parts = line.Split(new char[] { '\t' });
                        PinyinParseSyllable ps = new PinyinParseSyllable(parts[0], parts[1] == "v");
                        syllList.Add(ps);
                    }
                }
            }

            /// <summary>
            /// Returns syllable's ID, or 0 if not a standard syllable.
            /// </summary>
            public int GetId(PinyinSyllable syll)
            {
                string lo = syll.Text.ToLowerInvariant();
                if (!pyToId.ContainsKey(lo)) return 0;
                return pyToId[lo];
            }

            /// <summary>
            /// Split string into assumed pinyin syllables by tone marks
            /// </summary>
            private static List<string> doPinyinSplitDigits(string str)
            {
                List<string> res = new List<string>();
                string syll = "";
                foreach (char c in str)
                {
                    syll += c;
                    bool isToneMark = (c >= '0' && c <= '5');
                    if (isToneMark)
                    {
                        res.Add(syll);
                        syll = "";
                    }
                }
                if (syll != string.Empty) res.Add(syll);
                return res;
            }

            /// <summary>
            /// Recursively match pinyin syllables from start position in string.
            /// </summary>
            private bool doMatchSylls(string str, int pos, List<int> ends)
            {
                // Reach end of string: good
                if (pos == str.Length) return true;
                // Get rest of string to match
                string rest = pos == 0 ? str : str.Substring(pos);
                // Try all syllables in syllabary
                foreach (PinyinParseSyllable ps in syllList)
                {
                    // Syllables starting with a vowel not allowed inside text
                    if (pos != 0 && ps.VowelStart) continue;
                    // Find matching syllable
                    if (rest.StartsWith(ps.Text))
                    {
                        int endPos = pos + ps.Text.Length;
                        // We have a tone mark (digit 1-5) after syllable: got to skip that
                        if (rest.Length > ps.Text.Length)
                        {
                            char nextChr = rest[ps.Text.Length];
                            if (nextChr >= '1' && nextChr <= '5') ++endPos;
                        }
                        // Record end of syllable
                        ends.Add(endPos);
                        // If rest matches, we're done
                        if (doMatchSylls(str, endPos, ends)) return true;
                        // Otherwise, backtrack, move on to next syllable
                        ends.RemoveAt(ends.Count - 1);
                    }
                }
                // If we're here, failed to resolve syllables
                return false;
            }

            /// <summary>
            /// Split string into possible multiple pinyin syllables, or return as whole if not possible.
            /// </summary>
            private List<string> doPinyinSplitSyllables(string str)
            {
                List<string> res = new List<string>();
                // Sanity check
                if (str == string.Empty) return res;
                // Ending positions of syllables
                List<int> ends = new List<int>();
                // Recursive matching
                doMatchSylls(str, 0, ends);
                // Failed to match: return original string in one
                if (ends.Count == 0)
                {
                    res.Add(str);
                    return res;
                }
                // Split
                int pos = 0;
                foreach (int i in ends)
                {
                    string part = str.Substring(pos, i - pos);
                    res.Add(part);
                    pos = i;
                }
                // Done.
                return res;
            }

            /// <summary>
            /// Parses a pinyin query string into normalized syllables.
            /// </summary>
            public List<PinyinSyllable> ParsePinyinQuery(string query)
            {
                // If query is empty string or WS only: no syllables
                query = query.Trim();
                if (query == string.Empty) return new List<PinyinSyllable>();

                // Only deal with lower-case
                query = query.ToLowerInvariant();
                // Convert "u:" > "v" and "ü" > "v"
                query = query.Replace("u:", "v");
                query = query.Replace("ü", "v");

                // Split by syllables and apostrophes
                string[] explicitSplit = query.Split(new char[] { ' ', '\'', '’' });
                // Further split each part, in case input did not have spaces
                List<string> pinyinSplit = new List<string>();
                foreach (string str in explicitSplit)
                {
                    // Find numbers 1 thru 5: tone marks always come at end of syllable
                    // Important: this also eliminates empty syllables
                    List<string> numSplit = doPinyinSplitDigits(str);
                    // Split the rest by matching known pinyin syllables
                    foreach (string str2 in numSplit)
                    {
                        List<string> syllSplit = doPinyinSplitSyllables(str2);
                        pinyinSplit.AddRange(syllSplit);
                    }
                }
                // Create normalized syllable by separating tone mark, if present
                List<PinyinSyllable> res = new List<PinyinSyllable>();
                foreach (string str in pinyinSplit)
                {
                    char c = str[str.Length - 1];
                    int val = (int)(c - '0');
                    // Tone mark here
                    if (val >= 1 && val <= 5 && str.Length > 1)
                    {
                        if (val == 5) val = 0;
                        res.Add(new PinyinSyllable(str.Substring(0, str.Length - 1), val));
                    }
                    // No tone mark: add as unspecified
                    else res.Add(new PinyinSyllable(str, -1));
                }
                // If we have syllables ending in "r", split that into separate "r5"
                for (int i = 0; i < res.Count; ++i)
                {
                    PinyinSyllable ps = res[i];
                    if (ps.Text != "er" && ps.Text.Length > 1 && ps.Text.EndsWith("r"))
                    {
                        PinyinSyllable ps1 = new PinyinSyllable(ps.Text.Substring(0, ps.Text.Length - 1), ps.Tone);
                        PinyinSyllable ps2 = new PinyinSyllable("r", 0);
                        res[i] = ps1;
                        res.Insert(i + 1, ps2);
                    }
                }
                // Done
                return res;
            }
        }
    }
}
