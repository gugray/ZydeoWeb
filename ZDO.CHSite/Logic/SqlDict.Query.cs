using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text;
using System.Data;
using MySql.Data.MySqlClient;

using ZD.Common;

namespace ZDO.CHSite.Logic
{
    partial class SqlDict
    {
        private CedictEntry loadFromBlob(int blobId, MySqlCommand cmdSelBinaryEntry, byte[] buf)
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

        private static bool hasHanzi(string query)
        {
            foreach (char c in query)
                if (Utils.IsHanzi(c))
                    return true;
            return false;
        }

        private void verifyHanzi(CedictEntry entry, string query, List<CedictResult> res)
        {
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
                CedictResult cr = new CedictResult(CedictResult.SimpTradWarning.None,
                    entry, entry.HanziPinyinMap,
                    hiliteStart, hiliteLength);
                res.Add(cr);
            }
        }

        private void retrieveBatch(List<int> batch, byte[] buf, List<CedictEntry> entries, MySqlCommand cmdSelBinary10)
        {
            entries.Clear();
            cmdSelBinary10.Parameters["@id0"].Value = batch.Count > 0 ? batch[0] : -1;
            cmdSelBinary10.Parameters["@id1"].Value = batch.Count > 1 ? batch[1] : -1;
            cmdSelBinary10.Parameters["@id2"].Value = batch.Count > 2 ? batch[2] : -1;
            cmdSelBinary10.Parameters["@id3"].Value = batch.Count > 3 ? batch[3] : -1;
            cmdSelBinary10.Parameters["@id4"].Value = batch.Count > 4 ? batch[4] : -1;
            cmdSelBinary10.Parameters["@id5"].Value = batch.Count > 5 ? batch[5] : -1;
            cmdSelBinary10.Parameters["@id6"].Value = batch.Count > 6 ? batch[6] : -1;
            cmdSelBinary10.Parameters["@id7"].Value = batch.Count > 7 ? batch[7] : -1;
            cmdSelBinary10.Parameters["@id8"].Value = batch.Count > 8 ? batch[8] : -1;
            cmdSelBinary10.Parameters["@id9"].Value = batch.Count > 9 ? batch[9] : -1;
            using (MySqlDataReader rdr = cmdSelBinary10.ExecuteReader())
            {
                while (rdr.Read())
                {
                    int len = (int)rdr.GetBytes(0, 0, buf, 0, buf.Length);
                    using (BinReader br = new BinReader(buf, len))
                    {
                        entries.Add(new CedictEntry(br));
                    }
                }
            }
        }

        private void retrieveVerifyHanzi(HashSet<int> cands, string query, List<CedictResult> res)
        {
            byte[] buf = new byte[32768];
            List<CedictEntry> entries = new List<CedictEntry>(10);
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand cmdSelBinary10 = DB.GetCmd(conn, "SelBinaryEntry10"))
            {
                List<int> batch = new List<int>(10);
                foreach (int blobId in cands)
                {
                    if (batch.Count < 10) { batch.Add(blobId); continue; }
                    retrieveBatch(batch, buf, entries, cmdSelBinary10);
                    foreach (var entry in entries) verifyHanzi(entry, query, res);
                    batch.Clear();
                }
                if (batch.Count != 0)
                {
                    retrieveBatch(batch, buf, entries, cmdSelBinary10);
                    foreach (var entry in entries) verifyHanzi(entry, query, res);
                }
            }
        }

        private static int hrComp(CedictResult a, CedictResult b)
        {
            // First come those where match starts sooner
            int startCmp = a.HanziHiliteStart.CompareTo(b.HanziHiliteStart);
            if (startCmp != 0) return startCmp;
            // Then, pinyin lexical compare up to shorter's length
            int pyComp = a.Entry.PinyinCompare(b.Entry);
            if (pyComp != 0) return pyComp;
            // Pinyin is identical: shorter comes first
            int lengthCmp = a.Entry.ChSimpl.Length.CompareTo(b.Entry.ChSimpl.Length);
            return lengthCmp;
        }

        private void lookupHanzi(string query, List<CedictResult> res, List<CedictAnnotation> anns)
        {
            // Normalize query
            query = query.ToUpperInvariant();
            query = query.Replace(" ", "");
            // Distinct Hanzi
            HashSet<char> qhanzi = new HashSet<char>();
            foreach (char c in query) qhanzi.Add(c);
            // Get candidate IDs
            Stopwatch watch = new Stopwatch();
            watch.Restart();
            HashSet<int> cands = index.GetHanziCandidates(qhanzi);
            Console.WriteLine("Candidates: " + cands.Count + " (" + watch.ElapsedMilliseconds + " msec)");

            // Retrieve all candidates; verify on the fly
            watch.Restart();
            retrieveVerifyHanzi(cands, query, res);
            Console.WriteLine("Retrieval: " + res.Count + " (" + watch.ElapsedMilliseconds + " msec)");
            // Sort Hanzi results
            res.Sort((a, b) => hrComp(a, b));
            // Done.
        }

        public CedictLookupResult Lookup(string query)
        {
            // Prepare
            List<CedictResult> res = new List<CedictResult>();
            List<CedictAnnotation> anns = new List<CedictAnnotation>();
            SearchLang sl = SearchLang.Chinese;

            if (hasHanzi(query)) lookupHanzi(query, res, anns);
            else
            {
                //lookupPinyin(query, res);
            }

            // Done
            return new CedictLookupResult(query, res, anns, sl);
        }
    }
}
