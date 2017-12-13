using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

using ZD.Common;

namespace ZDO.CHSite.Logic
{
    partial class SqlDict
    {
        /// <summary>
        /// In-memory index with DB persistence.
        /// </summary>
        public class Index
        {
            /// <summary>
            /// One target-lookup candidate: Entry ID plus Sense IX.
            /// </summary>
            public struct TrgCandidate
            {
                public int EntryId;
                public int SenseIx;
            }

            /// <summary>
            /// One annotation candidate: Entry ID, and how long entry must be in query string.
            /// </summary>
            public struct AnnCandidate
            {
                public int EntryId;
                public int Length;
            }

            /// <summary>
            /// One item in list of senses that contain a target-language word.
            /// </summary>
            private struct TrgEntryPtr
            {
                /// <summary>
                /// Pointer: 3 bytes for entry ID, 1 byte (lowest) for sense index.
                /// </summary>
                public uint Ptr;
                /// <summary>
                /// Gets entry ID of instance.
                /// </summary>
                public int EntryID { get { return (int)(Ptr >> 8); } }
                /// <summary>
                /// Gets sense IX of instance.
                /// </summary>
                public byte SenseIx { get { return (byte)(Ptr & 0xff); } }
                /// <summary>
                /// Makes value from entry ID and sense IX.
                /// </summary>
                public static TrgEntryPtr Make(int entryId, byte senseIx)
                {
                    if (entryId > 0xffffff) throw new Exception("Entry ID must fit in 3 bytes.");
                    uint ptr = (uint)entryId;
                    ptr <<= 8;
                    ptr += senseIx;
                    return new TrgEntryPtr { Ptr = ptr };
                }
            }

            /// <summary>
            /// Entry/Sense instances of one normalized word.
            /// </summary>
            private struct TrgInstArr
            {
                /// <summary>
                /// Normalized word whose instances are indexed here.
                /// </summary>
                public string Norm;
                /// <summary>
                /// ID of word in DB's norm_words table.
                /// </summary>
                public int WordId;
                /// <summary>
                /// EntryId+SenseIx instances of normalized word.
                /// </summary>
                public TrgEntryPtr[] Instances;
            }

            /// <summary>
            /// One item in list of headwords that contain a Hanzi.
            /// </summary>
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            private struct IdeoEntryPtr
            {
                /// <summary>
                /// Index/position of the entry.
                /// </summary>
                public int EntryId;
                /// <summary>
                /// Number of *different* Hanzi in simplified headword.
                /// </summary>
                public byte SimpCount;
                /// <summary>
                /// Number of *different* Hanzi in traditional headword.
                /// </summary>
                public byte TradCount;
                /// <summary>
                /// Flags.
                /// </summary>
                public byte Flags;
                /// <summary>
                /// True if Hanzi is present in entry's simplified HW.
                /// </summary>
                public bool IsInSimp { get { return (Flags & 1) == 1; } }
                /// <summary>
                /// True if Hanzi is present in entry's traditional HW.
                /// </summary>
                public bool IsInTrad { get { return (Flags & 2) == 2; } }
                /// <summary>
                /// True if Hanzi is at  start of simplified HW.
                /// </summary>
                public bool IsAtSimpStart { get { return (Flags & 4) == 4; } }
                /// <summary>
                /// True if Hanzi is at  start of traditional HW.
                /// </summary>
                public bool IsAtTradStart { get { return (Flags & 8) == 8; } }
            }

            /// <summary>
            /// Simplified and traditional entry instances for one Hanzi.
            /// </summary>
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            private struct IdeoInstArr
            {
                /// <summary>
                /// The Hanzi whose instances are indexed here.
                /// </summary>
                public char Hanzi;
                /// <summary>
                /// Entries where Hanzi occurs in HW (simp/trad/both).
                /// </summary>
                public IdeoEntryPtr[] Instances;
            }

            /// <summary>
            /// Each Hanzi's instance lists; sorted by Hanzi for binary searching.
            /// </summary>
            private IdeoInstArr[] hanzi = new IdeoInstArr[0];

            /// <summary>
            /// Comparer for Hanzi binary search.
            /// </summary>
            private class HanziCmp : IComparer<IdeoInstArr>
            {
                public int Compare(IdeoInstArr x, IdeoInstArr y) { return x.Hanzi.CompareTo(y.Hanzi); }
            }

            /// <summary>
            /// Hanzi comparer instance. Coz we need one, C# syntax be damned here.
            /// </summary>
            private static readonly HanziCmp hanziCmp = new HanziCmp();

            /// <summary>
            /// Each normalized target word's instance lists; sorted by string hash.
            /// </summary>
            private TrgInstArr[] trg = new TrgInstArr[0];

            /// <summary>
            /// Empty target instance array.
            /// </summary>
            private static readonly TrgEntryPtr[] emptyTrgInstances = new TrgEntryPtr[0];

            /// <summary>
            /// Compararer for target word binary search.
            /// </summary>
            private class TrgCmp : IComparer<TrgInstArr>
            {
                public int Compare(TrgInstArr x, TrgInstArr y) { return x.Norm.GetHashCode().CompareTo(y.Norm.GetHashCode()); }
            }

            /// <summary>
            /// Target word comparer instance. C# syntax be damned.
            /// </summary>
            private static readonly TrgCmp trgCmp = new TrgCmp();

            /// <summary>
            /// Instances for one (standardized, toned) pinyin syllable.
            /// </summary>
            private struct PinyinInstArr
            {
                /// <summary>
                /// Two-byte standard pinyin ID, plus tone at lowest byte.
                /// </summary>
                public int TonedId;
                /// <summary>
                /// Instances. Highest three bytes are always entry ID; lowest byte, entry's (standard) PY syllable count.
                /// </summary>
                public uint[] Instances;
            }

            /// <summary>
            /// Each toned standard PY syllable's instances; sorted by TonedId.
            /// </summary>
            private PinyinInstArr[] syll;

            /// <summary>
            /// Pinyin syllable comparer. C# syntax be damned.
            /// </summary>
            private class SyllCmp : IComparer<PinyinInstArr>
            {
                public int Compare(PinyinInstArr x,  PinyinInstArr y) { return x.TonedId.CompareTo(y.TonedId); }
            }

            /// <summary>
            /// Pinyin syllable comparer instance.
            /// </summary>
            private static readonly SyllCmp syllCmp = new SyllCmp();

            /// <summary>
            /// Pinyin normalizer/helper.
            /// </summary>
            private readonly Pinyin pinyin;

            /// <summary>
            /// Target-language stop words.
            /// </summary>
            private readonly HashSet<string> trgStopWords;

            /// <summary>
            /// Count of non-deleted entries; cached here.
            /// </summary>
            public int EntryCount { get; private set; }

            /// <summary>
            /// RW lock protecting index. Caller must acquire before invoking any function on index.
            /// </summary>
            public readonly ReaderWriterLockSlim Lock = new ReaderWriterLockSlim();

            /// <summary>
            /// Ctor: load (construct) index from DB.
            /// </summary>
            public Index(ILogger logger, Pinyin pinyin, HashSet<string> trgStopWords)
            {
                this.pinyin = pinyin;
                this.trgStopWords = trgStopWords;
                try
                {
                    Reload();
                }
                catch (Exception ex)
                {
                    logger.LogError(new EventId(), ex, "Failed to initialize index.");
                }
            }
            
            /// <summary>
            /// Reloads index from DB. Useful after recreating, i.e., emptying DB.
            /// </summary>
            public void Reload()
            {
                using (MySqlConnection conn = DB.GetConn())
                {
                    // Construct index in memory, first with dictionaries and lists
                    // Compact into sorted arrays
                    // Separately for each index component so we keep excess memory usage low
                    loadHanzi(conn);
                    loadTrg(conn);
                    loadPinyin(conn);
                    RefreshEntryCount(conn);
                }

                // Ugly, but this is one place where it's justified: collect our garbage from temporary index construction
                // Only done once, at startup; better burn this time right now
                GC.Collect();
            }

            public void RefreshEntryCount(MySqlConnection conn)
            {
                using (var cmd = DB.GetCmd(conn, "SelEntryCount"))
                {
                    EntryCount = (int)(long)cmd.ExecuteScalar();
                }
            }

            /// <summary>
            /// Loads pinyin syllable index.
            /// </summary>
            /// <param name="conn"></param>
            private void loadPinyin(MySqlConnection conn)
            {
                // Toned-syllable-to-instance-list
                Dictionary<int, List<uint>> tmp = new Dictionary<int, List<uint>>();
                for (int i = 1; i <= pinyin.MaxId; ++i)
                {
                    int id = i << 8;
                    // Syllable ID is higher; lowest byte: tone 0-4.
                    tmp[id] = new List<uint>();
                    tmp[id + 1] = new List<uint>();
                    tmp[id + 2] = new List<uint>();
                    tmp[id + 3] = new List<uint>();
                    tmp[id + 4] = new List<uint>();
                }
                // Load DB table
                using (var cmdSelSyllInstances = DB.GetCmd(conn, "SelSyllInstances"))
                using (var rdr = cmdSelSyllInstances.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        int syllId = rdr.GetInt32(0);
                        byte syllCnt = rdr.GetByte(1);
                        uint entryId = rdr.GetUInt32(2);
                        uint val = entryId << 8;
                        val += syllCnt;
                        tmp[syllId].Add(val);
                    }
                }
                // List, sort, convert to array
                List<Tuple<int, List<uint>>> tmpList = new List<Tuple<int, List<uint>>>();
                foreach (var x in tmp) tmpList.Add(new Tuple<int, List<uint>>(x.Key, x.Value));
                tmpList.Sort((x, y) => x.Item1.CompareTo(y.Item1));
                syll = new PinyinInstArr[tmpList.Count];
                for (int i = 0; i != tmpList.Count; ++i)
                {
                    syll[i] = new PinyinInstArr
                    {
                        TonedId = tmpList[i].Item1,
                        Instances = tmpList[i].Item2.ToArray()
                    };
                }
            }

            /// <summary>
            /// Mirrors <see cref="TrgInstArr"/> at load time; less compact, more manageable.
            /// </summary>
            private class TrgInstList
            {
                public readonly string Norm;
                public readonly int WordId;
                public readonly List<TrgEntryPtr> Instances = new List<TrgEntryPtr>();
                public TrgInstList(string norm, int wordId) { Norm = norm; WordId = wordId; }
            }

            /// <summary>
            /// Load target-word index from DB.
            /// </summary>
            private void loadTrg(MySqlConnection conn)
            {
                Dictionary<string, TrgInstList> tmpTrg = new Dictionary<string, TrgInstList>();
                // First, select all normalized words. We must have them all in memory, and know their ID
                // Even if some entries got unindexed and no sense uses a normalized word anymore
                // The words themselves never go away; if they're missing from memory, we'd attempt to add them again to DB
                using (var cmdSelNormWords = DB.GetCmd(conn, "SelNormWords"))
                using (var rdr = cmdSelNormWords.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        int id = rdr.GetInt32("id");
                        string norm = rdr.GetString("word");
                        tmpTrg[norm] = new TrgInstList(norm, id);
                    }
                }
                // Populate instance lists
                using (var cmdSelTrgInstances = DB.GetCmd(conn, "SelTrgInstances"))
                using (var rdr = cmdSelTrgInstances.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        string norm = rdr.GetString("word");
                        int id = rdr.GetInt32("norm_word_id");
                        int entryId = rdr.GetInt32("blob_id");
                        byte senseIx = rdr.GetByte("sense_ix");
                        tmpTrg[norm].Instances.Add(TrgEntryPtr.Make(entryId, senseIx));
                    }
                }
                // Sort lists; copy to actual index
                trg = new TrgInstArr[tmpTrg.Count];
                int i = 0;
                foreach (var x in tmpTrg)
                {
                    x.Value.Instances.Sort((a, b) => a.Ptr.CompareTo(b.Ptr));
                    TrgInstArr tia = new TrgInstArr
                    {
                        Norm = x.Value.Norm,
                        WordId = x.Value.WordId,
                        Instances = x.Value.Instances.ToArray(),
                    };
                    trg[i] = tia;
                    ++i;
                }
                Array.Sort(trg, trgCmp);
            }

            /// <summary>
            /// Mirrors <see cref="IdeoInstArr"/> at load time; less compact, more manageable.
            /// </summary>
            private class IdeoInstList
            {
                public readonly char Hanzi;
                public readonly List<IdeoEntryPtr> Instances = new List<IdeoEntryPtr>();
                public IdeoInstList(char hanzi) { Hanzi = hanzi; }
            }

            /// <summary>
            /// Loads Hanzi index from DB.
            /// </summary>
            private void loadHanzi(MySqlConnection conn)
            {
                Dictionary<char, IdeoInstList> tmpHanzi = new Dictionary<char, IdeoInstList>();
                using (var cmdSelHanziInstances = DB.GetCmd(conn, "SelHanziInstances"))
                using (var rdr = cmdSelHanziInstances.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        // Retrieve record
                        char hanzi = (char)rdr.GetInt32("hanzi");
                        byte flags = rdr.GetByte("flags");
                        int simpCount = rdr.GetInt32("simp_count");
                        int tradCount = rdr.GetInt32("trad_count");
                        int blobId = rdr.GetInt32("blob_id");
                        // Instance list for hanzi: new, or seen before
                        IdeoInstList iil;
                        if (!tmpHanzi.ContainsKey(hanzi))
                        {
                            iil = new IdeoInstList(hanzi);
                            tmpHanzi[hanzi] = iil;
                        }
                        else iil = tmpHanzi[hanzi];
                        iil.Instances.Add(new IdeoEntryPtr
                        {
                            EntryId = blobId,
                            Flags = flags,
                            SimpCount = (byte)simpCount,
                            TradCount = (byte)tradCount,
                        });
                    }
                }
                // Build actual index: copy to arrays; sort arrays
                List<IdeoInstArr> lstHanzi = new List<IdeoInstArr>(tmpHanzi.Count);
                foreach (var x in tmpHanzi)
                {
                    var instances = x.Value.Instances;
                    IdeoInstArr iia = new IdeoInstArr
                    {
                        Hanzi = x.Key,
                        Instances = new IdeoEntryPtr[instances.Count],
                    };
                    instances.Sort((a, b) => a.EntryId.CompareTo(b.EntryId));
                    instances.CopyTo(iia.Instances, 0);
                    lstHanzi.Add(iia);
                }
                lstHanzi.Sort((x, y) => x.Hanzi.CompareTo(y.Hanzi));
                hanzi = lstHanzi.ToArray();
            }

            /// <summary>
            /// Gets normalized word's index in trg array, or -1.
            /// </summary>
            private int getNormWordIx(string norm)
            {
                TrgInstArr val = new TrgInstArr { Norm = norm, WordId = -1, Instances = emptyTrgInstances };
                // We can have multiple identical hashes in array.
                // This will get only one index, not necessarily the first.
                int ix = Array.BinarySearch(trg, val, trgCmp);
                if (ix < 0) return -1;
                // Hit already? This will most often be the case.
                if (trg[ix].Norm == norm) return ix;
                // Go down
                int realIx = ix - 1;
                while (realIx >= 0 && trg[realIx].Norm.GetHashCode() == norm.GetHashCode())
                {
                    if (trg[realIx].Norm == norm) return realIx;
                    --realIx;
                }
                // Nah, go up
                realIx = ix + 1;
                while (realIx < trg.Length && trg[realIx].Norm.GetHashCode() == norm.GetHashCode())
                {
                    if (trg[realIx].Norm == norm) return realIx;
                    ++realIx;
                }
                // No joy at all, hash collision of unknown word with something in our array.
                return -1;
            }

            private readonly Dictionary<char, List<IdeoEntryPtr>> hanziToIndex = new Dictionary<char, List<IdeoEntryPtr>>();
            private readonly Dictionary<string, List<TrgEntryPtr>> trgToIndex = new Dictionary<string, List<TrgEntryPtr>>();
            private readonly Dictionary<int, List<uint>> syllToIndex = new Dictionary<int, List<uint>>();
            private readonly HashSet<int> entriesToUnindex = new HashSet<int>();
            private readonly HashSet<char> hanziToUnindex = new HashSet<char>();
            private readonly HashSet<string> trgToUnindex = new HashSet<string>();
            private readonly HashSet<int> syllToUnindex = new HashSet<int>();

            private readonly Dictionary<string, int> prefixDiffs = new Dictionary<string, int>();

            public class StorageCommands
            {
                public MySqlCommand CmdDelEntryHanziInstances;
                public MySqlCommand CmdInsHanziInstance;
                public MySqlCommand CmdDelEntryTrgInstances;
                public MySqlCommand CmdInsNormWord;
                public MySqlCommand CmdInsTrgInstance;
                public MySqlCommand CmdInsUpdPrefixWord;
                public MySqlCommand CmdDelEntrySyllInstances;
                public MySqlCommand CmdInsSyllInstance;
            }

            /// <summary>
            /// Helper to append to key's list, adding key if not present yet.
            /// </summary>
            private static void append(char hanzi, Dictionary<char, List<IdeoEntryPtr>> dict, 
                int entryId, byte flags, byte simpCount, byte tradCount)
            {
                List<IdeoEntryPtr> lst;
                if (!dict.ContainsKey(hanzi))
                {
                    lst = new List<IdeoEntryPtr>();
                    dict[hanzi] = lst;
                }
                else lst = dict[hanzi];
                // Verify ID growth
                if (lst.Count > 0 && lst[lst.Count - 1].EntryId > entryId)
                    throw new Exception("IDs of newly indexed entries must be larger than all previously indexed entry ID.");
                lst.Add(new IdeoEntryPtr
                {
                    EntryId = entryId,
                    Flags= flags,
                    SimpCount = simpCount,
                    TradCount = tradCount,
                });
            }

            /// <summary>
            /// Files an entry for indexing (Hanzi), pending <see cref="ApplyChanges"/>.
            /// </summary>
            public void FileToIndex(int entryId, HashSet<char> hSimp, HashSet<char> hTrad, HashSet<char> hBoth)
            {
                byte simpCount = (byte)hSimp.Count;
                byte tradCount = (byte)hTrad.Count;
                foreach (char c in hSimp) append(c, hanziToIndex, entryId, 1, simpCount, 0);
                foreach (char c in hTrad) append(c, hanziToIndex, entryId, 2, 0, tradCount);
                foreach (char c in hBoth) append(c, hanziToIndex, entryId, 3, simpCount, tradCount);
            }

            /// <summary>
            /// Files an entry for indexing (pinyin), pending <see cref="ApplyChanges"/>.
            /// </summary>
            public void FileToIndex(int entryId, IEnumerable<PinyinSyllable> sylls)
            {
                HashSet<int> tonedSyllSet = new HashSet<int>();
                foreach (var syll in sylls)
                {
                    int id = pinyin.GetId(syll);
                    if (id == 0) continue;
                    int tone = syll.Tone;
                    if (tone == -1) continue;
                    tonedSyllSet.Add((id << 8) + tone);
                }
                byte cnt = (byte)tonedSyllSet.Count;
                uint val = ((uint)entryId); val <<= 8; val += cnt;
                foreach (int tonedSyll in tonedSyllSet)
                {
                    List<uint> instList;
                    if (!syllToIndex.ContainsKey(tonedSyll))
                    {
                        instList = new List<uint>();
                        syllToIndex[tonedSyll] = instList;
                    }
                    else instList = syllToIndex[tonedSyll];
                    instList.Add(val);
                }
            }

            /// <summary>
            /// Helper to append to key's list, adding key if not present yet.
            /// </summary>
            private static void append(string norm, Dictionary<string, List<TrgEntryPtr>> dict, int entryId, byte senseIx)
            {
                List<TrgEntryPtr> lst;
                if (!dict.ContainsKey(norm))
                {
                    lst = new List<TrgEntryPtr>();
                    dict[norm] = lst;
                }
                else lst = dict[norm];
                TrgEntryPtr tep = TrgEntryPtr.Make(entryId, senseIx);
                // Verify ID growth
                if (lst.Count > 0 && lst[lst.Count - 1].Ptr > tep.Ptr)
                    throw new Exception("IDs of newly indexed entries must be larger than all previously indexed entry ID.");
                lst.Add(tep);
            }

            /// <summary>
            /// Files an entry for indexing (target), pending <see cref="ApplyChanges"/>.
            /// </summary>
            public void FileToIndex(int entryId, byte senseIx, List<Token> toks)
            {
                // For indexing, only different normalized words count
                // For prefix hints, all non-number, non-Chinese surface words count
                HashSet<string> norms = new HashSet<string>();
                foreach (var tok in toks)
                {
                    if (tok.Norm == Token.Num || tok.Norm == Token.Zho) continue;
                    // Prefix hints: no pipe
                    if (tok.SplitPosSurf == 0)
                    {
                        if (!prefixDiffs.ContainsKey(tok.Surf)) prefixDiffs[tok.Surf] = 1;
                        else ++prefixDiffs[tok.Surf];
                    }
                    // Prefix hints: pipe in surface form
                    else
                    {
                        string sFull = tok.Surf.Replace("|", "");
                        string sOne = tok.Surf.Substring(0, tok.SplitPosSurf);
                        string sTwo = tok.Surf.Substring(tok.SplitPosSurf + 1);
                        if (!prefixDiffs.ContainsKey(sFull)) prefixDiffs[sFull] = 1;
                        else ++prefixDiffs[sFull];
                        if (!prefixDiffs.ContainsKey(sOne)) prefixDiffs[sOne] = 1;
                        else ++prefixDiffs[sOne];
                        if (!prefixDiffs.ContainsKey(sTwo)) prefixDiffs[sTwo] = 1;
                        else ++prefixDiffs[sTwo];
                    }
                    // Indexing: full normal form
                    if (!norms.Contains(tok.Norm))
                    {
                        append(tok.Norm, trgToIndex, entryId, senseIx);
                        norms.Add(tok.Norm);
                    }
                    // Indexing: pipe: deal with parts
                    if (tok.SplitPosNorm != 0)
                    {
                        string nOne = tok.Norm.Substring(0, tok.SplitPosNorm);
                        string nTwo = tok.Norm.Substring(tok.SplitPosNorm);
                        if (!trgStopWords.Contains(nOne) && !norms.Contains(nOne))
                        {
                            append(nOne, trgToIndex, entryId, senseIx);
                            norms.Add(nOne);
                        }
                        if (!trgStopWords.Contains(nTwo) && !norms.Contains(nTwo))
                        {
                            append(nTwo, trgToIndex, entryId, senseIx);
                            norms.Add(nTwo);
                        }
                    }
                }
            }

            /// <summary>
            /// Files an entry for unindexing, pending <see cref="ApplyChanges"/>.
            /// </summary>
            /// <param name="entryId">ID of entry to unindex.</param>
            /// <param name="hAll">All hanzi from HW (simplified and traditional)</param>
            /// <param name="toks">All tokens from all senses.</param>
            /// <param name="sylls">Entry's pinyin.</param>
            public void FileToUnindex(int entryId, HashSet<char> hAll, List<Token> toks, IEnumerable<PinyinSyllable> sylls)
            {
                entriesToUnindex.Add(entryId);
                foreach (char c in hAll) hanziToUnindex.Add(c);
                foreach (Token tok in toks)
                {
                    if (tok.Norm == Token.Num || tok.Norm == Token.Zho) continue;
                    // Full normal form
                    trgToUnindex.Add(tok.Norm);
                    // Piped parts
                    if (tok.SplitPosNorm != 0)
                    {
                        string nOne = tok.Norm.Substring(0, tok.SplitPosNorm);
                        string nTwo = tok.Norm.Substring(tok.SplitPosNorm);
                        if (!trgStopWords.Contains(nOne)) trgToUnindex.Add(nOne);
                        if (!trgStopWords.Contains(nTwo)) trgToUnindex.Add(nTwo);
                    }
                    // Prefix hints: no pipe
                    if (tok.SplitPosSurf == 0)
                    {
                        if (!prefixDiffs.ContainsKey(tok.Surf)) prefixDiffs[tok.Surf] = -1;
                        else --prefixDiffs[tok.Surf];
                    }
                    // Prefix hints: pipe in surface form
                    else
                    {
                        string sFull = tok.Surf.Replace("|", "");
                        string sOne = tok.Surf.Substring(0, tok.SplitPosSurf);
                        string sTwo = tok.Surf.Substring(tok.SplitPosSurf + 1);
                        if (!prefixDiffs.ContainsKey(sFull)) prefixDiffs[sFull] = -1;
                        else --prefixDiffs[sFull];
                        if (!prefixDiffs.ContainsKey(sOne)) prefixDiffs[sOne] = -1;
                        else --prefixDiffs[sOne];
                        if (!prefixDiffs.ContainsKey(sTwo)) prefixDiffs[sTwo] = -1;
                        else --prefixDiffs[sTwo];
                    }
                }
                HashSet<int> tonedSyllSet = new HashSet<int>();
                foreach (var syll in sylls)
                {
                    int id = pinyin.GetId(syll);
                    if (id == 0) continue;
                    int tone = syll.Tone;
                    if (tone == -1) continue;
                    tonedSyllSet.Add((id << 8) + tone);
                }
                foreach (int tonedSyll in tonedSyllSet) syllToUnindex.Add(tonedSyll);
            }

            /// <summary>
            /// Applies pinyin unindexing, in-memory and DB.
            /// </summary>
            private void applyUnindexSyll(StorageCommands sc)
            {
                // Unindex in DB: simple: just fire off deletes for collected entry IDs
                foreach (int entryId in entriesToUnindex)
                {
                    sc.CmdDelEntrySyllInstances.Parameters["@blob_id"].Value = entryId;
                    sc.CmdDelEntrySyllInstances.ExecuteNonQuery();
                }
                // Comb through each affected instance list; remove deleted entries
                foreach (var tonedId in syllToUnindex)
                {
                    PinyinInstArr pia = new PinyinInstArr { TonedId = tonedId };
                    int ix = Array.BinarySearch(syll, pia, syllCmp);
                    pia = syll[ix];
                    List<uint> instList = new List<uint>(pia.Instances.Length);
                    foreach (uint val in pia.Instances)
                    {
                        uint entryId = val >> 8;
                        if (!entriesToUnindex.Contains((int)entryId)) instList.Add(val);
                    }
                    pia.Instances = instList.ToArray();
                    syll[ix] = pia;
                }
            }

            /// <summary>
            /// Applies filed pinyin indexing work, DB as well as in-memory.
            /// </summary>
            private void applyIndexSyll(StorageCommands sc)
            {
                foreach (var x in syllToIndex)
                {
                    int tonedId = x.Key;
                    // Database: simply fire off inserts
                    sc.CmdInsSyllInstance.Parameters["@toned_syll"].Value = tonedId;
                    foreach (uint val in x.Value)
                    {
                        uint entryId = (val & 0xffffff00);
                        entryId >>= 8;
                        sc.CmdInsSyllInstance.Parameters["@syll_count"].Value = (int)(val & 0xff);
                        sc.CmdInsSyllInstance.Parameters["@blob_id"].Value = (int)entryId;
                        sc.CmdInsSyllInstance.ExecuteNonQuery();
                    }
                    // Index: Append to affected instance lists
                    PinyinInstArr pia = new PinyinInstArr { TonedId = tonedId };
                    int ix = Array.BinarySearch(syll, pia, syllCmp);
                    pia = syll[ix];
                    // Verify ID growth
                    if (pia.Instances.Length > 0 && pia.Instances[pia.Instances.Length - 1] > x.Value[0])
                        throw new Exception("IDs of newly indexed entries must be larger than all previously indexed entry ID.");
                    uint[] newArr = new uint[pia.Instances.Length + x.Value.Count];
                    pia.Instances.CopyTo(newArr, 0);
                    x.Value.CopyTo(newArr, pia.Instances.Length);
                    pia.Instances = newArr;
                    syll[ix] = pia;
                }
            }

            /// <summary>
            /// Persists filed index/unindex items in DB.
            /// </summary>
            private void applyHanziDB(StorageCommands sc)
            {
                // Unindex in DB: simple: just fire off deletes for collected entry IDs
                foreach (int entryId in entriesToUnindex)
                {
                    sc.CmdDelEntryHanziInstances.Parameters["@blob_id"].Value = entryId;
                    sc.CmdDelEntryHanziInstances.ExecuteNonQuery();
                }

                // Index in DB: simple: just insert
                foreach (var x in hanziToIndex)
                {
                    char hanzi = x.Key;
                    sc.CmdInsHanziInstance.Parameters["@hanzi"].Value = (int)x.Key;
                    foreach (var y in x.Value)
                    {
                        sc.CmdInsHanziInstance.Parameters["@flags"].Value = y.Flags;
                        sc.CmdInsHanziInstance.Parameters["@simp_count"].Value = y.SimpCount;
                        sc.CmdInsHanziInstance.Parameters["@trad_count"].Value = y.TradCount;
                        sc.CmdInsHanziInstance.Parameters["@blob_id"].Value = y.EntryId;
                        sc.CmdInsHanziInstance.ExecuteNonQuery();
                    }
                }
            }

            /// <summary>
            /// Update in-memory: unindex filed Hanzi.
            /// </summary>
            private void applyUnindexHanzi()
            {
                // Unindex Hanzi: recreate affected instance lists by omitting deleted IDs
                foreach (char c in hanziToUnindex)
                {
                    IdeoInstArr val = new IdeoInstArr { Hanzi = c };
                    int hpos = Array.BinarySearch(hanzi, val, hanziCmp);
                    val = hanzi[hpos];
                    List<IdeoEntryPtr> lst = new List<IdeoEntryPtr>(val.Instances.Length);
                    foreach (var iep in val.Instances) if (!entriesToUnindex.Contains(iep.EntryId)) lst.Add(iep);
                    val.Instances = lst.ToArray();
                    hanzi[hpos] = val;
                }
            }

            /// <summary>
            /// Update in-memory: index filed Hanzi.
            /// </summary>
            private void applyIndexHanzi()
            {
                // Index Hanzi
                // First make sure new Hanzi are there in sorted array.
                HashSet<char> newHanziChars = new HashSet<char>();
                foreach (var x in hanziToIndex)
                {
                    IdeoInstArr val = new IdeoInstArr { Hanzi = x.Key };
                    int hpos = Array.BinarySearch(hanzi, val, hanziCmp);
                    if (hpos < 0) newHanziChars.Add(x.Key);
                }
                List<IdeoInstArr> newHanzi = new List<IdeoInstArr>(hanzi.Length);
                newHanzi.AddRange(hanzi);
                foreach (char c in newHanziChars)
                {
                    IdeoInstArr newIIA = new IdeoInstArr { Hanzi = c, Instances = new IdeoEntryPtr[0] };
                    newHanzi.Add(newIIA);
                }
                newHanzi.Sort((a, b) => a.Hanzi.CompareTo(b.Hanzi));
                hanzi = newHanzi.ToArray();
                // Now, append to end of affected instance lists. No re-sorting, but verify monotonous ID growth.
                foreach (var x in hanziToIndex)
                {
                    char c = x.Key;
                    List<IdeoEntryPtr> lstNew = x.Value;
                    lstNew.Sort((a, b) => a.EntryId.CompareTo(b.EntryId)); // Only sorting the new IDs
                    IdeoInstArr val = new IdeoInstArr { Hanzi = c };
                    int hpos = Array.BinarySearch(hanzi, val, hanziCmp);
                    val = hanzi[hpos];
                    // Verify ID growth
                    if (val.Instances.Length > 0 && val.Instances[val.Instances.Length - 1].EntryId > lstNew[0].EntryId)
                        throw new Exception("IDs of newly indexed entries must be larger than all previously indexed entry ID.");
                    // New array: concat
                    IdeoEntryPtr[] arrNew = new IdeoEntryPtr[val.Instances.Length + lstNew.Count];
                    val.Instances.CopyTo(arrNew, 0);
                    lstNew.CopyTo(arrNew, val.Instances.Length);
                    val.Instances = arrNew;
                    hanzi[hpos] = val;
                }
            }

            /// <summary>
            /// Removes filed items from DB as well as in-memory index.
            /// </summary>
            private void applyUnindexTrg(StorageCommands sc)
            {
                // Unindex in DB: simple: just fire off deletes for collected entry IDs
                foreach (int entryId in entriesToUnindex)
                {
                    sc.CmdDelEntryTrgInstances.Parameters["@blob_id"].Value = entryId;
                    sc.CmdDelEntryTrgInstances.ExecuteNonQuery();
                }
                // In memory: from all affected normwords' instances, remove all items for unindexed entries
                foreach (string norm in trgToUnindex)
                {
                    int ix = getNormWordIx(norm);
                    TrgInstArr tia = trg[ix];
                    List<TrgEntryPtr> lst = new List<TrgEntryPtr>(tia.Instances.Length);
                    foreach (TrgEntryPtr tep in tia.Instances)
                        if (!entriesToUnindex.Contains(tep.EntryID))
                            lst.Add(tep);
                    tia.Instances = lst.ToArray();
                    trg[ix] = tia;
                }
            }

            /// <summary>
            /// Adds filed target items to DB as well as in-memory index.
            /// </summary>
            private void applyIndexTrg(StorageCommands sc)
            {
                // Find which normalized words are new. We must add them to DB.
                // Then create slots in-memory, with new DB ID; re-sort array.
                HashSet<string> newWords = new HashSet<string>();
                foreach (var x in trgToIndex)
                {
                    int tix = getNormWordIx(x.Key);
                    if (tix < 0) newWords.Add(x.Key);
                }
                TrgInstArr[] newTrg = new TrgInstArr[trg.Length + newWords.Count];
                trg.CopyTo(newTrg, 0);
                int i = trg.Length;
                trg = newTrg;
                foreach (string newWord in newWords)
                {
                    sc.CmdInsNormWord.Parameters["@word"].Value = newWord;
                    sc.CmdInsNormWord.ExecuteNonQuery();
                    int newId = (int)sc.CmdInsNormWord.LastInsertedId;
                    trg[i] = new TrgInstArr { Norm = newWord, WordId = newId, Instances = emptyTrgInstances };
                    ++i;
                }
                Array.Sort(trg, trgCmp);
                // Append to instance vectors. Just verify ID growth.
                // And while we're at it, store in DB too.
                foreach (var x in trgToIndex)
                {
                    int tix = getNormWordIx(x.Key);
                    TrgInstArr tia = trg[tix];
                    if (tia.Instances.Length > 0 && tia.Instances[tia.Instances.Length - 1].Ptr > x.Value[0].Ptr)
                        throw new Exception("IDs of newly indexed entries must be larger than all previously indexed entry ID.");
                    TrgEntryPtr[] newArr = new TrgEntryPtr[tia.Instances.Length + x.Value.Count];
                    tia.Instances.CopyTo(newArr, 0);
                    x.Value.CopyTo(newArr, tia.Instances.Length);
                    tia.Instances = newArr;
                    trg[tix] = tia;
                    foreach (TrgEntryPtr tep in x.Value)
                    {
                        sc.CmdInsTrgInstance.Parameters["@norm_word_id"].Value = tia.WordId;
                        sc.CmdInsTrgInstance.Parameters["@blob_id"].Value = tep.EntryID;
                        sc.CmdInsTrgInstance.Parameters["@sense_ix"].Value = tep.SenseIx;
                        sc.CmdInsTrgInstance.ExecuteNonQuery();
                    }
                }
            }

            /// <summary>
            /// Adds missing surface words, updates occurrence counts. DB only, this.
            /// </summary>
            private void applyPrefixDB(StorageCommands sc)
            {
                foreach (var x in prefixDiffs)
                {
                    sc.CmdInsUpdPrefixWord.Parameters["@prefix"].Value = Tokenizer.Get4Prefix(x.Key);
                    sc.CmdInsUpdPrefixWord.Parameters["@word"].Value = x.Key;
                    sc.CmdInsUpdPrefixWord.Parameters["@count"].Value = x.Value;
                    sc.CmdInsUpdPrefixWord.ExecuteNonQuery();
                }
            }

            /// <summary>
            /// Clears all filed index/unindex items.
            /// </summary>
            private void clearFiledWork()
            {
                // Clear all filed work
                entriesToUnindex.Clear();
                hanziToIndex.Clear();
                hanziToUnindex.Clear();
                trgToIndex.Clear();
                trgToUnindex.Clear();
                prefixDiffs.Clear();
                syllToIndex.Clear();
                syllToUnindex.Clear();
            }

            /// <summary>
            /// Applies all filed changes to both in-memory index and DB.
            /// </summary>
            public void ApplyChanges(StorageCommands sc)
            {
                applyHanziDB(sc);
                applyUnindexHanzi();
                applyIndexHanzi();
                applyUnindexTrg(sc);
                applyIndexTrg(sc);
                applyUnindexSyll(sc);
                applyIndexSyll(sc);
                applyPrefixDB(sc);
                clearFiledWork();
            }

            /// <summary>
            /// Intersects two ordered instance lists during Hanzi candidate retriaval.
            /// </summary>
            private static void intersect(List<int> a, IdeoEntryPtr[] b, List<int> isect, bool simp)
            {
                int ixa = 0, ixb = 0;
                while (ixa < a.Count && ixb < b.Length)
                {
                    if (simp && !b[ixb].IsInSimp || !simp && !b[ixb].IsInTrad) { ++ixb;  continue; }
                    var comp = a[ixa].CompareTo(b[ixb].EntryId);
                    if (comp < 0) ++ixa;
                    else if (comp > 0) ++ixb;
                    else
                    {
                        isect.Add(a[ixa]);
                        ++ixa;
                        ++ixb;
                    }
                }
            }

            /// <summary>
            /// Intersects two ordered instance lists during target candidate retriaval.
            /// </summary>
            private static void intersect(List<TrgEntryPtr> a, TrgEntryPtr[] b, List<TrgEntryPtr> isect)
            {
                int ixa = 0, ixb = 0;
                while (ixa < a.Count && ixb < b.Length)
                {
                    var comp = a[ixa].Ptr.CompareTo(b[ixb].Ptr);
                    if (comp < 0) ++ixa;
                    else if (comp > 0) ++ixb;
                    else
                    {
                        isect.Add(a[ixa]);
                        ++ixa;
                        ++ixb;
                    }
                }
            }

            /// <summary>
            /// One retrieved suggestion from a prefix.
            /// </summary>
            public struct PrefixWord
            {
                public string Word;
                public string NakedLo;
                public int Count;
            }

            /// <summary>
            /// Loads a raw list of words that (loosely) match the provided prefix.
            /// </summary>
            public List<PrefixWord> LoadWordsForPrefix(string prefix)
            {
                List<PrefixWord> res = new List<PrefixWord>();
                // Integer search range; stripped form for verification
                string nakedLo = Tokenizer.StripDiacritics(prefix).ToLowerInvariant();
                long min = Tokenizer.Get4Prefix(prefix);
                long max;
                if (prefix.Length >= 4) max = min;
                else if (prefix.Length == 3) max = min + 0xffff;
                else if (prefix.Length == 2) max = min + 0xffffffff;
                else if (prefix.Length == 1) max = min + 0xffffffffffff;
                else throw new ArgumentException("Empty prefix makes no sense.");
                // DB lookup
                // Filter if prefix is longer than 4 characters
                using (MySqlConnection conn = DB.GetConn())
                using (MySqlCommand cmd = DB.GetCmd(conn, "SelPrefixWords"))
                {
                    cmd.Parameters["@min"].Value = min;
                    cmd.Parameters["@max"].Value = max;
                    using (MySqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            PrefixWord pw = new PrefixWord { Word = rdr.GetString(0), Count = rdr.GetInt32(1) };
                            // Might no longer be in dictionary
                            if (pw.Count <= 0) continue;
                            pw.NakedLo = Tokenizer.StripDiacritics(pw.Word).ToLowerInvariant();
                            // Verify prefixes longer than 4 chars: numeric query doesn't filter for those
                            if (nakedLo.Length > 4 && !pw.NakedLo.StartsWith(nakedLo)) continue;
                            // It's a keeper.
                            res.Add(pw);
                        }
                    }
                }
                return res;
            }

            /// <summary>
            /// Returns candidates (EntryId+SenseIx) that contain all the queried target-language tokens.
            /// </summary>
            public HashSet<TrgCandidate> GetTrgCandidates(HashSet<Token> query)
            {
                List<string> norms = new List<string>(query.Count);
                foreach (Token tok in query) norms.Add(tok.Norm);
                HashSet<TrgCandidate> res = new HashSet<TrgCandidate>();
                if (norms.Count == 0) return res;

                // Only one token
                if (norms.Count == 1)
                {
                    int tix = getNormWordIx(norms[0]);
                    if (tix < 0) return res;
                    TrgInstArr tia = trg[tix];
                    foreach (var inst in tia.Instances)
                        res.Add(new TrgCandidate { EntryId = inst.EntryID, SenseIx = inst.SenseIx });
                    return res;
                }
                // Get intersection of instance vectors; algo relies on sortedness.
                // OPT: Start with shorter instance vectors
                // OPT: Reuse lists for intersection instead of constantly reallocating
                List<TrgEntryPtr> cands = null;
                foreach (string norm in norms)
                {
                    int tix = getNormWordIx(norm);
                    if (tix < 0) return res;
                    TrgInstArr tia = trg[tix];
                    // Very first character: initialize intersection lists.
                    if (cands == null)
                    {
                        cands = new List<TrgEntryPtr>(tia.Instances.Length);
                        foreach (var x in tia.Instances) cands.Add(x);
                        continue;
                    }
                    // Intersect with current character's lists
                    List<TrgEntryPtr> isect = new List<TrgEntryPtr>(Math.Min(cands.Count, tia.Instances.Length));
                    intersect(cands, tia.Instances, isect);
                    // List are now the intesections
                    cands = isect;
                    // We can cut it short if we're sure there won't be matches
                    if (cands.Count == 0) return res;
                }
                foreach (TrgEntryPtr tep in cands)
                    res.Add(new TrgCandidate { EntryId = tep.EntryID, SenseIx = tep.SenseIx });
                return res;
            }

            private static List<uint> union(List<uint> a, uint[] b)
            {
                List<uint> res = new List<uint>(a.Count + b.Length);
                int ixa = 0, ixb = 0;
                while (ixa < a.Count || ixb < b.Length)
                {
                    if (ixa == a.Count) { res.Add(b[ixb]); ++ixb; continue; }
                    if (ixb == b.Length) { res.Add(a[ixa]); ++ixa; continue; }
                    if (a[ixa] < b[ixb]) { res.Add(a[ixa]); ++ixa; }
                    else if (b[ixb] < a[ixa]) { res.Add(b[ixb]); ++ixb; }
                    else { res.Add(a[ixa]); ++ixa; ++ixb; }
                }
                return res;
            }

            private uint[] getPinyinUnion(int openId)
            {
                PinyinInstArr pia0 = new PinyinInstArr { TonedId = (openId << 8) };
                int ix0 = Array.BinarySearch(syll, pia0, syllCmp);
                uint[] arr0 = syll[ix0].Instances;
                PinyinInstArr pia1 = new PinyinInstArr { TonedId = (openId << 8) + 1 };
                int ix1 = Array.BinarySearch(syll, pia1, syllCmp);
                uint[] arr1 = syll[ix1].Instances;
                PinyinInstArr pia2 = new PinyinInstArr { TonedId = (openId << 8) + 2 };
                int ix2 = Array.BinarySearch(syll, pia2, syllCmp);
                uint[] arr2 = syll[ix2].Instances;
                PinyinInstArr pia3 = new PinyinInstArr { TonedId = (openId << 8) + 3 };
                int ix3 = Array.BinarySearch(syll, pia3, syllCmp);
                uint[] arr3 = syll[ix3].Instances;
                PinyinInstArr pia4 = new PinyinInstArr { TonedId = (openId << 8) + 4 };
                int ix4 = Array.BinarySearch(syll, pia4, syllCmp);
                uint[] arr4 = syll[ix4].Instances;
                List<uint> lstA = new List<uint>(arr0.Length);
                lstA.AddRange(arr0);
                List<uint> lstB = union(lstA, arr1);
                lstA = union(lstB, arr2);
                lstB = union(lstA, arr3);
                lstA = union(lstB, arr4);
                return lstA.ToArray();
            }

            private static List<uint> intersect(List<uint> a, uint[] b)
            {
                int ixa = 0, ixb = 0;
                List<uint> isect = new List<uint>(Math.Min(a.Count, b.Length));
                while (ixa < a.Count && ixb < b.Length)
                {
                    var comp = a[ixa].CompareTo(b[ixb]);
                    if (comp < 0) ++ixa;
                    else if (comp > 0) ++ixb;
                    else
                    {
                        isect.Add(a[ixa]);
                        ++ixa;
                        ++ixb;
                    }
                }
                return isect;
            }

            /// <summary>
            /// Returns IDs of entries that contain all provided distinct Pinyin syllables.
            /// </summary>
            public HashSet<int> GetPinyinCandidates(IEnumerable<PinyinSyllable> query)
            {
                HashSet<int> res = new HashSet<int>();
                // Separate query into distinct toned and open (underspecified) syllables
                // If both "shang" and "shang4" show up, then we only underspecified one.
                List<int> qToned = new List<int>();
                List<int> qOpen = new List<int>();
                // Gather distinct underspecified first
                foreach (var qs in query)
                {
                    int id = pinyin.GetId(qs);
                    if (id == 0) return res; // Any non-standard syllables: instant no match.
                    if (qs.Tone != -1) continue;
                    if (qOpen.Contains(id)) continue; // Real-life queries are short. This is assumed better than HashSet here.
                    qOpen.Add(id);
                }
                // Gather distinct specified.
                foreach (var qs in query)
                {
                    int id = pinyin.GetId(qs);
                    if (qs.Tone == -1) continue;
                    if (qOpen.Contains(id)) continue;
                    id <<= 8; id += qs.Tone;
                    if (qToned.Contains(id)) continue;
                    qToned.Add(id);
                }
                // OPT: start with shorter instances; do binary search when current set is small and instance list is large
                // For now, brute brute force
                // We first do unions of open queries, in-memory; then use these, plus normal toned instance vectors, to isect
                List<uint[]> toIsect = new List<uint[]>(qToned.Count + qOpen.Count);
                foreach (int tonedId in qToned)
                {
                    PinyinInstArr pia = new PinyinInstArr { TonedId = tonedId };
                    int ix = Array.BinarySearch(syll, pia, syllCmp);
                    toIsect.Add(syll[ix].Instances);
                }
                foreach (int openId in qOpen) toIsect.Add(getPinyinUnion(openId));
                // Only one list: easy
                if (toIsect.Count == 1)
                {
                    foreach (uint val in toIsect[0]) res.Add((int)(val >> 8));
                    return res;
                }
                // Intersect 'em all
                List<uint> isect = new List<uint>();
                isect.AddRange(toIsect[0]);
                for (int i = 1; i < toIsect.Count; ++i)
                {
                    List<uint> curr = intersect(isect, toIsect[i]);
                    isect = curr;
                }
                foreach (uint val in isect) res.Add((int)(val >> 8));
                return res;
            }

            /// <summary>
            /// Returns IDs of entries that contain all the queried Hanzi, either in their simplified or traditional HW.
            /// </summary>
            public HashSet<int> GetHanziCandidates(HashSet<char> query)
            {
                // Query: easier to work with as list
                List<char> qlist = new List<char>(query.Count);
                foreach (char c in query) qlist.Add(c);

                // Empty result
                HashSet<int> res = new HashSet<int>();
                if (qlist.Count == 0) return res;
                IdeoInstArr val = new IdeoInstArr();

                // Only one character in query: special, and easy
                if (qlist.Count == 1)
                {
                    val.Hanzi = qlist[0];
                    int hpos = Array.BinarySearch(hanzi, val, hanziCmp);
                    if (hpos < 0) return res;
                    val = hanzi[hpos];
                    foreach (var inst in val.Instances) res.Add(inst.EntryId);
                    return res;
                }
                // Get intersection of instance vectors; separately for simplified and traditional.
                // Merging sorted instance vectors.
                // OPT: Start with shorter instance vectors
                // OPT: Reuse lists for intersection instead of constantly reallocating
                List<int> lstSimp = null;
                List<int> lstTrad = null;
                foreach (char c in qlist)
                {
                    val.Hanzi = c;
                    int hpos = Array.BinarySearch(hanzi, val, hanziCmp);
                    if (hpos < 0) return res;
                    val = hanzi[hpos];
                    // Very first character: initialize intersection lists.
                    if (lstSimp == null)
                    {
                        lstSimp = new List<int>(val.Instances.Length);
                        lstTrad = new List<int>(val.Instances.Length);
                        foreach (var x in val.Instances)
                        {
                            if (x.IsInSimp) lstSimp.Add(x.EntryId);
                            if (x.IsInTrad) lstTrad.Add(x.EntryId);
                        }
                        continue;
                    }
                    // Intersect with current character's lists
                    List<int> isSimp = new List<int>(Math.Min(lstSimp.Count, val.Instances.Length));
                    List<int> isTrad = new List<int>(Math.Min(lstTrad.Count, val.Instances.Length));
                    intersect(lstSimp, val.Instances, isSimp, true);
                    intersect(lstTrad, val.Instances, isTrad, false);
                    // List are now the intesections
                    lstSimp = isSimp;
                    lstTrad = isTrad;
                    // We can cut it short if we're sure there won't be matches
                    if (lstSimp.Count == 0 && lstTrad.Count == 0) return res;
                }
                // Result is union of two lists
                foreach (int id in lstSimp) res.Add(id);
                foreach (int id in lstTrad) res.Add(id);
                return res;
            }

            public List<AnnCandidate> GetAnnotationCandidates(string query, bool simp, int start, int minEnd)
            {
                var res = new List<AnnCandidate>();
                IdeoInstArr val = new IdeoInstArr();
                int hpos;
                char cfirst = query[start];
                // First character not seen in any entry: we're done right here.
                val.Hanzi = cfirst;
                hpos = Array.BinarySearch(hanzi, val, hanziCmp);
                if (hpos < 0) { return res; }
                val = hanzi[hpos];
                // Special treatment: very last character of query string
                if (start == query.Length - 1 && minEnd == query.Length)
                {
                    foreach (var iep in val.Instances)
                    {
                        if (simp && iep.IsInSimp && iep.SimpCount == 1)
                            res.Add(new AnnCandidate { EntryId = iep.EntryId, Length = 1 });
                        if (!simp && iep.IsInTrad && iep.TradCount == 1)
                            res.Add(new AnnCandidate { EntryId = iep.EntryId, Length = 1 });
                    }
                    return res;
                }
                // Candidate IDs remaining at each position (increasingly narrow intersections)
                HashSet<int>[] cands = new HashSet<int>[query.Length - start];
                for (int i = 0; i != cands.Length; ++i) cands[i] = new HashSet<int>();
                // Fill in first candidate set
                foreach (var iep in val.Instances)
                {
                    if (simp && iep.IsInSimp) cands[0].Add(iep.EntryId);
                    if (!simp && iep.IsInTrad) cands[0].Add(iep.EntryId);
                }
                // Our candidates of length 1
                if (start + 1 > minEnd)
                {
                    foreach (int id in cands[0])
                    {
                        // TO-DO: only short enough entries
                        res.Add(new AnnCandidate { EntryId = id, Length = 1 });
                    }
                }
                // Narrow down intersections for each subsequent position
                for (int i = start + 1; i < query.Length; ++i)
                {
                    char c = query[i];
                    HashSet<int> prevSet = cands[i - start - 1];
                    // If we've run out of candidates, not point in continuing
                    if (prevSet.Count == 0) break;
                    // Calculate intersection with previous set
                    HashSet<int> thisSet = cands[i - start];
                    val.Hanzi = c;
                    hpos = Array.BinarySearch(hanzi, val, hanziCmp);
                    if (hpos < 0) continue;
                    val = hanzi[hpos];
                    foreach (var iep in val.Instances)
                    {
                        if (simp && iep.IsInSimp && prevSet.Contains(iep.EntryId)) thisSet.Add(iep.EntryId);
                        if (!simp && iep.IsInTrad && prevSet.Contains(iep.EntryId)) thisSet.Add(iep.EntryId);
                    }
                    // If we're far along enough, add to result
                    if (i > minEnd)
                    {
                        foreach (int id in thisSet)
                        {
                            // TO-DO: only short enough entries
                            res.Add(new AnnCandidate { EntryId = id, Length = i - start + 1 });
                        }
                    }
                }
                // Sort longest to shortest
                res.Sort((x, y) => y.Length.CompareTo(x.Length));
                // Done.
                return res;
            }
        }
    }
}
