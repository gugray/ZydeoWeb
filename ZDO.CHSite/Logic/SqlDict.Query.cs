using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Data;
using MySql.Data.MySqlClient;

using ZD.Common;

namespace ZDO.CHSite.Logic
{
    public partial class SqlDict
    {
        public class Query : IDisposable
        {
            /// <summary>
            /// DB connection I'll be using throughout lookup. Owned.
            /// </summary>
            private MySqlConnection conn;
            /// <summary>
            /// Transaction: upheld throughout lookup to ensure consistency across queries.
            /// </summary>
            private MySqlTransaction tr;

            // Reused commands
            private MySqlCommand cmdSelBinaryEntry;
            private MySqlCommand cmdSelHanziInstances;
            // ---------------

            private class EntryProvider : ICedictEntryProvider
            {
                private readonly Dictionary<int, CedictEntry> entryDict = new Dictionary<int, CedictEntry>();

                public void AddEntry(int entryId, CedictEntry entry)
                {
                    entryDict[entryId] = entry;
                }

                public CedictEntry GetEntry(int entryId)
                {
                    return entryDict[entryId];
                }

                public void Dispose() { }
            }

            /// <summary>
            /// A lookup result with its loaded entry; needed to be able to sort results before throwing away entry itself.
            /// </summary>
            private struct ResWithEntry
            {
                public readonly CedictResult Res;
                public readonly CedictEntry Entry;
                public ResWithEntry(CedictResult res, CedictEntry entry)
                {
                    Res = res;
                    Entry = entry;
                }
            }

            private static void log(string msg)
            {
                //string line = "{0}:{1}:{2}.{3:D3} ";
                //DateTime d = DateTime.Now;
                //line = string.Format(line, d.Hour, d.Minute, d.Second, d.Millisecond);
                //DiagLogger.LogError(line + msg);
                //System.Diagnostics.Debug.WriteLine(line + msg);
            }

            public Query()
            {
                conn = DB.GetConn();
                tr = conn.BeginTransaction(IsolationLevel.Serializable);
                cmdSelBinaryEntry = DB.GetCmd(conn, "SelBinaryEntry");
                cmdSelHanziInstances = DB.GetCmd(conn, "SelHanziInstances");
            }

            public void Dispose()
            {
                if (cmdSelHanziInstances != null) cmdSelHanziInstances.Dispose();
                if (cmdSelBinaryEntry != null) cmdSelBinaryEntry.Dispose();
                if (tr != null) tr.Dispose();
                if (conn != null) conn.Dispose();
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
            private static bool doMatchSylls(string str, int pos, List<int> ends)
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
            private static List<PinyinParseSyllable> syllList = new List<PinyinParseSyllable>();

            /// <summary>
            /// Loads known pinyin syllables from embedded resource.
            /// </summary>
            private static void loadSyllabary()
            {
                // TO-DO: Load syllabary
                // TO-DO: Move this to LangUtils
                //Assembly a = typeof(DictEngine).GetTypeInfo().Assembly;
                //using (Stream s = a.GetManifestResourceStream("ZD.CedictEngine.Resources.pinyin.txt"))
                //using (StreamReader sr = new StreamReader(s))
                //{
                //    string line;
                //    while ((line = sr.ReadLine()) != null)
                //    {
                //        if (line == string.Empty) continue;
                //        string[] parts = line.Split(new char[] { '\t' });
                //        PinyinParseSyllable ps = new PinyinParseSyllable(parts[0], parts[1] == "v");
                //        syllList.Add(ps);
                //    }
                //}
            }

            /// <summary>
            /// Split string into possible multiple pinyin syllables, or return as whole if not possible.
            /// </summary>
            private static List<string> doPinyinSplitSyllables(string str)
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
            private static List<PinyinSyllable> parsePinyinQuery(string query)
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

            private static void interpretPinyin(string query, out List<PinyinSyllable> qsylls,
                out List<PinyinSyllable> qnorm)
            {
                qsylls = parsePinyinQuery(query);
                // Get every syllable once - we ignore repeats
                // If a syllable occurs with unspecified tone once, or if it occurs with multiple tone marks
                // -> We only take it as one item with unspecified tone
                // Otherwise, take it as is, with tone mark
                Dictionary<string, int> syllDict = new Dictionary<string, int>();
                foreach (var syll in qsylls)
                {
                    if (!syllDict.ContainsKey(syll.Text)) syllDict[syll.Text] = syll.Tone;
                    else if (syllDict[syll.Text] != syll.Tone) syllDict[syll.Text] = -1;
                }
                qnorm = new List<PinyinSyllable>();
                foreach (var x in syllDict)
                    qnorm.Add(new PinyinSyllable(x.Key, x.Value));
            }

            private Dictionary<string, HashSet<int>> getPinyinCandidates(List<PinyinSyllable> sylls)
            {
                // Prepare
                Dictionary<string, HashSet<int>> res = new Dictionary<string, HashSet<int>>();
                Dictionary<int, string> hashToText = new Dictionary<int, string>();

                // Build custom, single query to get *all* instance that
                // are relevant for any requested syllable
                // Also initialize result dictionary with pinyin keys
                StringBuilder sb = new StringBuilder();
                sb.Append("SELECT pinyin_hash, tone, syll_count, blob_id FROM pinyin_instances WHERE");
                bool first = true;
                foreach (PinyinSyllable syll in sylls)
                {
                    // Init dictionary
                    string key = syll.Tone == -1 ? syll.Text : syll.GetDisplayString(false);
                    res[key] = new HashSet<int>();
                    hashToText[CedictEntry.Hash(syll.Text)] = syll.Text;
                    // Build our custom query
                    if (!first) sb.Append(" OR");
                    else first = false;
                    sb.Append(" (pinyin_hash=");
                    sb.Append(CedictEntry.Hash(syll.Text).ToString());
                    if (syll.Tone != -1)
                    {
                        sb.Append(" AND tone=");
                        sb.Append(syll.Tone.ToString());
                    }
                    sb.Append(")");
                }
                sb.Append(";");
                // Compile and execute SQL command
                using (MySqlCommand cmd = new MySqlCommand(sb.ToString(), conn))
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        // Which query syllable is this for?
                        // With or without tone mark.
                        HashSet<int> cands = null;
                        string text = hashToText[rdr.GetInt32(0)];
                        if (res.ContainsKey(text)) cands = res[text];
                        else
                        {
                            text += rdr.GetInt32(1).ToString();
                            cands = res[text];
                        }
                        // Store blob ID
                        cands.Add(rdr.GetInt32(3));
                    }
                }

                // Done
                return res;
            }

            private List<int> intersectCandidates(Dictionary<string, HashSet<int>> candsBySyll)
            {
                List<int> res = new List<int>();
                if (candsBySyll.Count == 0) return res;
                if (candsBySyll.Count == 1)
                {
                    foreach (var x in candsBySyll)
                        res.AddRange(x.Value);
                    return res;
                }
                // Put hash sets in an array; shorter ones first
                HashSet<int>[] sets = new HashSet<int>[candsBySyll.Count];
                int pos = 0;
                foreach (var x in candsBySyll)
                {
                    sets[pos] = x.Value;
                    ++pos;
                }
                Array.Sort(sets, (x, y) => x.Count.CompareTo(y.Count));
                // Look for intersection from left (shorter) to right (longer)
                foreach (int id in sets[0])
                {
                    bool failed = false;
                    for (int i = 1; i < sets.Length; ++i)
                    {
                        if (!sets[i].Contains(id)) { failed = true; break; }
                    }
                    if (!failed) res.Add(id);
                }
                return res;
            }

            private readonly byte[] buf = new byte[32768];

            private CedictEntry loadFromBlob(int blobId)
            {
                cmdSelBinaryEntry.Parameters["@blob_id"].Value = blobId;
                using (MySqlDataReader rdr = cmdSelBinaryEntry.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        int len = (int)rdr.GetBytes(0, 0, buf, 0, buf.Length);
                        using (BinReader br = new BinReader(buf, len))
                        {
                            return new CedictEntry(br);
                        }
                    }
                }
                return null;
            }

            private List<ResWithEntry> retrieveVerifyPinyin(List<int> cands, List<PinyinSyllable> qsylls)
            {
                List<ResWithEntry> resList = new List<ResWithEntry>();
                foreach (int blobId in cands)
                {
                    // Load entry from DB
                    CedictEntry entry = loadFromBlob(blobId);

                    // Find query syllables in entry
                    int syllStart = -1;
                    for (int i = 0; i <= entry.PinyinCount - qsylls.Count; ++i)
                    {
                        int j;
                        for (j = 0; j != qsylls.Count; ++j)
                        {
                            PinyinSyllable syllEntry = entry.GetPinyinAt(i + j);
                            PinyinSyllable syllQuery = qsylls[j];
                            if (syllEntry.Text.ToLowerInvariant() != syllQuery.Text) break;
                            if (syllQuery.Tone != -1 && syllEntry.Tone != syllQuery.Tone) break;
                        }
                        if (j == qsylls.Count)
                        {
                            syllStart = i;
                            break;
                        }
                    }
                    // Entry is a keeper if query syllables found
                    if (syllStart == -1) continue;

                    // Keeper!
                    CedictResult cres = new CedictResult(blobId, entry.HanziPinyinMap, syllStart, qsylls.Count);
                    ResWithEntry resWE = new ResWithEntry(cres, entry);
                    resList.Add(resWE);
                }
                return resList;
            }

            private static int pyComp(ResWithEntry a, ResWithEntry b)
            {
                // Shorter entry comes first
                int lengthCmp = a.Entry.PinyinCount.CompareTo(b.Entry.PinyinCount);
                if (lengthCmp != 0) return lengthCmp;
                // Between equally long headwords where match starts sooner comes first
                int startCmp = a.Res.PinyinHiliteStart.CompareTo(b.Res.PinyinHiliteStart);
                if (startCmp != 0) return startCmp;
                // Order equally long entries by pinyin lexicographical order
                return a.Entry.PinyinCompare(b.Entry);
            }

            private void lookupPinyin(string query,
                EntryProvider ep, List<CedictResult> res)
            {
                // Interpret query string
                List<PinyinSyllable> qsylls, qnorm;
                interpretPinyin(query, out qsylls, out qnorm);
                // Get instance vectors
                Dictionary<string, HashSet<int>> candsBySyll = getPinyinCandidates(qnorm);
                // Intersect candidates
                List<int> cands = intersectCandidates(candsBySyll);
                // Retrieve all candidates; verify on the fly
                List<ResWithEntry> rl = retrieveVerifyPinyin(cands, qsylls);
                // Sort pinyin results
                rl.Sort((a, b) => pyComp(a, b));
                // Done.
                res.Capacity = rl.Count;
                for (int i = 0; i != rl.Count; ++i)
                {
                    ResWithEntry rwe = rl[i];
                    res.Add(rwe.Res);
                    ep.AddEntry(rwe.Res.EntryId, rwe.Entry);
                }
            }

            private bool getHanziCandidates(HashSet<char> qhanzi,
                Dictionary<char, HashSet<int>> candsBySimp,
                Dictionary<char, HashSet<int>> candsByTrad)
            {
                bool hadEmptySimp = false;
                bool hadEmptyTrad = false;
                foreach (char c in qhanzi)
                {
                    if (hadEmptySimp && hadEmptyTrad) return false;
                    HashSet<int> simpInstances = new HashSet<int>();
                    HashSet<int> tradInstances = new HashSet<int>();
                    candsBySimp[c] = simpInstances;
                    candsByTrad[c] = tradInstances;
                    cmdSelHanziInstances.Parameters["@hanzi"].Value = (int)c;
                    using (MySqlDataReader rdr = cmdSelHanziInstances.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            int simpTrad = rdr.GetInt32(0);
                            int blobId = rdr.GetInt32(1);
                            if (simpTrad == 1 || simpTrad == 3) simpInstances.Add(blobId);
                            if (simpTrad == 2 || simpTrad == 3) tradInstances.Add(blobId);
                        }
                    }
                    if (simpInstances.Count == 0) hadEmptySimp = true;
                    if (tradInstances.Count == 0) hadEmptyTrad = true;
                }
                return true;
            }

            private HashSet<int> intersectCandidates(Dictionary<char, HashSet<int>> cands)
            {
                if (cands.Count == 1)
                {
                    foreach (var x in cands) return x.Value;
                }
                HashSet<int> resSet = null;
                foreach (var x in cands)
                {
                    if (resSet == null)
                    {
                        resSet = new HashSet<int>();
                        foreach (int i in x.Value) resSet.Add(i);
                        continue;
                    }
                    if (resSet.Count == 0) break;
                    HashSet<int> newSet = new HashSet<int>();
                    foreach (int i in resSet)
                    {
                        if (x.Value.Contains(i)) newSet.Add(i);
                    }
                    resSet = newSet;
                }
                return resSet;
            }

            private List<ResWithEntry> retrieveVerifyHanzi(HashSet<int> cands, string query)
            {
                List<ResWithEntry> resList = new List<ResWithEntry>();
                foreach (int blobId in cands)
                {
                    // Load entry from DB
                    CedictEntry entry = loadFromBlob(blobId);

                    // Figure out position/length of query string in simplified and traditional headwords
                    int hiliteStart = -1;
                    int hiliteLength = 0;
                    hiliteStart = entry.ChSimpl.IndexOf(query);
                    if (hiliteStart != -1) hiliteLength = query.Length;
                    // If not found in simplified, check in traditional
                    if (hiliteLength == 0)
                    {
                        hiliteStart = entry.ChTrad.IndexOf(query);
                        if (hiliteStart != -1) hiliteLength = query.Length;
                    }
                    // Entry is a keeper if either source or target headword contains query
                    if (hiliteLength != 0)
                    {
                        CedictResult res = new CedictResult(CedictResult.SimpTradWarning.None,
                            blobId, entry.HanziPinyinMap,
                            hiliteStart, hiliteLength);
                        ResWithEntry resWE = new ResWithEntry(res, entry);
                        resList.Add(resWE);
                    }
                }
                return resList;
            }

            private static int hrComp(ResWithEntry a, ResWithEntry b)
            {
                // First come those where match starts sooner
                int startCmp = a.Res.HanziHiliteStart.CompareTo(b.Res.HanziHiliteStart);
                if (startCmp != 0) return startCmp;
                // Then, pinyin lexical compare up to shorter's length
                int pyComp = a.Entry.PinyinCompare(b.Entry);
                if (pyComp != 0) return pyComp;
                // Pinyin is identical: shorter comes first
                int lengthCmp = a.Entry.ChSimpl.Length.CompareTo(b.Entry.ChSimpl.Length);
                return lengthCmp;


                //// Shorter entry comes first
                //int lengthCmp = a.Entry.ChSimpl.Length.CompareTo(b.Entry.ChSimpl.Length);
                //if (lengthCmp != 0) return lengthCmp;
                //// Between equally long headwords where match starts sooner comes first
                //int startCmp = a.Res.HanziHiliteStart.CompareTo(b.Res.HanziHiliteStart);
                //if (startCmp != 0) return startCmp;
                //// Order equally long entries by pinyin lexicographical order
                //return a.Entry.PinyinCompare(b.Entry);
            }

            private void lookupHanzi(string query, EntryProvider ep,
                List<CedictResult> res, List<CedictAnnotation> anns)
            {
                // Distinct Hanzi
                query = query.ToUpperInvariant();
                query = query.Trim();
                query = query.Replace(" ", "");
                HashSet<char> qhanzi = new HashSet<char>();
                foreach (char c in query) qhanzi.Add(c);
                // Get instance vectors
                Dictionary<char, HashSet<int>> candsBySimp = new Dictionary<char,HashSet<int>>();
                Dictionary<char, HashSet<int>> candsByTrad = new Dictionary<char,HashSet<int>>();
                if (!getHanziCandidates(qhanzi, candsBySimp, candsByTrad))
                {
                    // If at least one Hanzi doesn't occur in any HW: we're done.
                    return;
                }
                // Intersect candidates
                HashSet<int> candsSimp = intersectCandidates(candsBySimp);
                HashSet<int> candsTrad = intersectCandidates(candsByTrad);
                // Take union
                HashSet<int> cands = new HashSet<int>();
                foreach (int i in candsSimp) cands.Add(i);
                foreach (int i in candsTrad) cands.Add(i);
                // Retrieve all candidates; verify on the fly
                List<ResWithEntry> rl = retrieveVerifyHanzi(cands, query);
                // Sort Hanzi results
                rl.Sort((a, b) => hrComp(a, b));
                // Done.
                res.Capacity = rl.Count;
                for (int i = 0; i != rl.Count; ++i)
                {
                    ResWithEntry rwe = rl[i];
                    res.Add(rwe.Res);
                    ep.AddEntry(rwe.Res.EntryId, rwe.Entry);
                }
            }

            private bool hasHanzi(string query)
            {
                foreach (char c in query)
                    if (Utils.IsHanzi(c))
                        return true;
                return false;
            }

            public CedictLookupResult Lookup(string query)
            {
                // Prepare
                EntryProvider ep = new EntryProvider();
                List<CedictResult> res = new List<CedictResult>();
                List<CedictAnnotation> anns = new List<CedictAnnotation>();
                SearchLang sl = SearchLang.Chinese;

                if (hasHanzi(query)) lookupHanzi(query, ep, res, anns);
                else
                {
                    lookupPinyin(query, ep, res);
                }

                // Done
                return new CedictLookupResult(ep, query, res, anns, sl);
            }
        }
    }
}