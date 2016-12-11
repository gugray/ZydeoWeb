using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Data;
using MySql.Data.MySqlClient;

using ZD.Common;
using ZD.LangUtils;

namespace ZDO.CHSite.Logic
{
    partial class SqlDict
    {
        public class Exporter : IDisposable
        {
            private readonly Dictionary<int, string> idToUserName = new Dictionary<int, string>();
            private readonly MySqlConnection conn;
            private readonly MySqlCommand cmdSelAllHistory;
            private readonly CedictParser parser = new CedictParser();
            private MySqlDataReader rdrAllHistory = null;

            public Exporter()
            {
                conn = DB.GetConn();
                cmdSelAllHistory = DB.GetCmd(conn, "SelAllHistory");
                // Read all users - we won't keep looking up user names
                using (var cmd = DB.GetCmd(conn, "SelAllUsers"))
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        int id = rdr.GetInt32("id");
                        string name = rdr.GetString("user_name");
                        idToUserName[id] = name;
                    }
                }
            }

            private class HistRec
            {
                public int EntryId;
                public string EntryHw;
                public string EntryTrg;
                public EntryStatus EntryStatus;
                public bool EntryDeleted;
                public int ModifUserId;
                public DateTime ModifTimeUtc;
                public string ModifNote;
                public int ModifBulkRef;
                public byte ModifChangeType;
                public string ModifHwBefore;
                public string ModifTrgBefore;
                public byte ModifStatusBefore;
            }

            public void Dispose()
            {
                if (rdrAllHistory != null) rdrAllHistory.Dispose();
                if (cmdSelAllHistory != null) cmdSelAllHistory.Dispose();
                if (conn != null) conn.Dispose();
            }

            private HistRec lastHistRec = null;

            /// <summary>
            /// <para>Gets full history of entry with next smallest ID, or null if query is over.</para>
            /// <para>Returned history is oldest to newest.</para>
            /// </summary>
            public void GetNext(out List<EntryVersion> history, out int entryId)
            {
                history = null; entryId = -1;
                if (rdrAllHistory == null) rdrAllHistory = cmdSelAllHistory.ExecuteReader();
                List<HistRec> recs = new List<HistRec>();
                if (lastHistRec != null)
                {
                    recs.Add(lastHistRec);
                    lastHistRec = null;
                }
                while (true)
                {
                    if (!rdrAllHistory.Read())
                    {
                        if (recs.Count != 0) toVersions(recs, out history, out entryId);
                        return;
                    }
                    // Retrieve data from reader
                    HistRec hrec = new HistRec
                    {
                        EntryId = rdrAllHistory.GetInt32("id"),
                        EntryHw = rdrAllHistory.GetString("hw"),
                        EntryTrg = rdrAllHistory.GetString("trg"),
                        EntryStatus = (EntryStatus)(sbyte)rdrAllHistory.GetSByte("status"),
                        EntryDeleted = ((sbyte)rdrAllHistory.GetSByte("deleted")) == 1,
                        ModifUserId = rdrAllHistory.GetInt32("user_id"),
                        ModifTimeUtc = new DateTime(rdrAllHistory.GetDateTime("timestamp").Ticks, DateTimeKind.Utc),
                        ModifNote = rdrAllHistory.GetString("note"),
                        ModifBulkRef = rdrAllHistory.GetInt32("bulk_ref"),
                        ModifChangeType = rdrAllHistory.GetByte("chg"),
                        ModifHwBefore = rdrAllHistory.GetFieldType("hw_before") == DBNull.Value.GetType() ? null : rdrAllHistory.GetString("hw_before"),
                        ModifTrgBefore = rdrAllHistory.GetFieldType("trg_before") == DBNull.Value.GetType() ? null : rdrAllHistory.GetString("trg_before"),
                        ModifStatusBefore = rdrAllHistory.GetFieldType("status_before") == DBNull.Value.GetType() ? (byte)99 : rdrAllHistory.GetByte("status_before"),
                    };
                    // No-DBNull workaround: fix empties
                    if (hrec.ModifHwBefore == "") hrec.ModifHwBefore = null;
                    if (hrec.ModifTrgBefore == "") hrec.ModifTrgBefore = null;
                    if (hrec.ModifChangeType != 4) hrec.ModifStatusBefore = 99;
                    // Same entry?
                    if (recs.Count == 0 || recs[recs.Count - 1].EntryId == hrec.EntryId)
                    {
                        recs.Add(hrec);
                        continue;
                    }
                    lastHistRec = hrec;
                    break;
                }
                toVersions(recs, out history, out entryId);
            }

            private void toVersions(List<HistRec> recs, out List<EntryVersion> history, out int entryId)
            {
                entryId = recs[0].EntryId;
                history = new List<EntryVersion>();
                // Sort records newest to oldest
                recs.Sort((x, y) => y.ModifTimeUtc.CompareTo(x.ModifTimeUtc));
                // Resolve HW, TRG and STATUS for each version
                string[] verHw = new string[recs.Count];
                string[] verTrg = new string[recs.Count];
                EntryStatus[] verStatus = new EntryStatus[recs.Count];
                for (int i = 0; i != recs.Count; ++i)
                {
                    HistRec rec = recs[i];
                    // Newest: take entry, "propagate" all the way to oldest
                    if (i == 0)
                    {
                        for (int j = 0; j != recs.Count; ++j)
                        {
                            verHw[j] = rec.EntryHw;
                            verTrg[j] = rec.EntryTrg;
                            verStatus[j] = rec.EntryStatus;
                        }
                    }
                    // Change has "before" value: propagate to earlier versions
                    if (rec.ModifHwBefore != null)
                        for (int j = i + 1; j < verHw.Length; ++j) verHw[j] = rec.ModifHwBefore;
                    if (rec.ModifTrgBefore != null)
                        for (int j = i + 1; j < verTrg.Length; ++j) verTrg[j] = rec.ModifTrgBefore;
                    if (rec.ModifStatusBefore != 99)
                        for (int j = i + 1; j < verStatus.Length; ++j) verStatus[j] = (EntryStatus)rec.ModifStatusBefore;
                }
                // Moving now from oldest to newest, build history
                // First and last item are special: they always contain entry
                // First coz we must "anchor" entry; last because it's always present in non-commented form
                for (int i = recs.Count - 1; i >= 0; --i)
                {
                    HistRec rec = recs[i];
                    CedictEntry entry = parser.ParseEntry(verHw[i] + " " + verTrg[i], -1, null);
                    EntryVersion ev = new EntryVersion
                    {
                        Timestamp = rec.ModifTimeUtc,
                        User = idToUserName[rec.ModifUserId],
                        Status = verStatus[i],
                        BulkRef = rec.ModifBulkRef,
                        Comment = rec.ModifNote,
                        Entry = null,
                    };
                    // First and last version always get entry
                    if (i == 0 || i == recs.Count - 1) ev.Entry = entry;
                    // Also if HW or TRG changed from earlier (i + 1) version
                    else if (verHw[i] != verHw[i + 1] || verTrg[i] != verTrg[i + 1]) ev.Entry = entry;
                    // Append to history
                    history.Add(ev);
                }
            }
        }
    }
}
