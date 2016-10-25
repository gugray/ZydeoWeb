using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;
using MySql.Data.MySqlClient;

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
            /// One item in list of headwords that contain a Hanzi.
            /// </summary>
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct IdeoEntryPtr
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
                public char Hanzi;
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
            /// RW lock protecting index. Caller must acquire before invoking any function on index.
            /// </summary>
            public readonly ReaderWriterLockSlim Lock = new ReaderWriterLockSlim();

            /// <summary>
            /// Ctor: load (construct) index from DB.
            /// </summary>
            public Index()
            {
                Reload();
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
                }

                // Ugly, but this is one place where it's justified: collect our garbage from temporary index construction
                // Only done once, at startup; better burn this time right now
                GC.Collect();
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

            private readonly Dictionary<char, List<IdeoEntryPtr>> hanziToIndex = new Dictionary<char, List<IdeoEntryPtr>>();
            private readonly HashSet<int> entriesToUnindex = new HashSet<int>();
            private readonly HashSet<char> hanziToUnindex = new HashSet<char>();

            public class StorageCommands
            {
                public MySqlCommand CmdDelEntryHanziInstances;
                public MySqlCommand CmdInsHanziInstance;
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
            /// Files an entry for indexing, pending <see cref="ApplyChanges"/>.
            /// </summary>
            public void FileToIndex(int entryId,
                HashSet<char> hSimp, HashSet<char> hTrad, HashSet<char> hBoth)
            {
                byte simpCount = (byte)hSimp.Count;
                byte tradCount = (byte)hTrad.Count;
                foreach (char c in hSimp) append(c, hanziToIndex, entryId, 1, simpCount, 0);
                foreach (char c in hTrad) append(c, hanziToIndex, entryId, 2, 0, tradCount);
                foreach (char c in hBoth) append(c, hanziToIndex, entryId, 3, simpCount, tradCount);
            }

            /// <summary>
            /// Files an entry for unindexing, pending <see cref="ApplyChanges"/>.
            /// </summary>
            public void FileToUnindex(int entryId, HashSet<char> hAll)
            {
                entriesToUnindex.Add(entryId);
                foreach (char c in hAll) hanziToUnindex.Add(c);
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
            /// Clears all filed index/unindex items.
            /// </summary>
            private void clearFiledWork()
            {
                // Clear all filed work
                hanziToIndex.Clear();
                entriesToUnindex.Clear();
                hanziToUnindex.Clear();
            }

            /// <summary>
            /// Applies all filed changes to both in-memory index and DB.
            /// </summary>
            public void ApplyChanges(StorageCommands sc)
            {
                applyHanziDB(sc);
                applyUnindexHanzi();
                applyIndexHanzi();
                clearFiledWork();
            }

            /// <summary>
            /// Intersects two ordered instance lists during Hanzi candidate retriaval.
            /// </summary>
            private static void intersect(List<int> a, IdeoEntryPtr[] b, List<int> trg, bool simp)
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
                        trg.Add(a[ixa]);
                        ++ixa;
                        ++ixb;
                    }
                }
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
        }
    }
}
