using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

using ZD.Common;
using ZDO.CHSite.Entities;

namespace ZDO.CHSite.Logic
{
    public partial class SqlDict
    {
        /// <summary>
        /// Returns entry's changes (newest first), except the very last one.
        /// </summary>
        public static List<ChangeItem> GetPastChanges(int entryId)
        {
            List<ChangeItem> res = new List<ChangeItem>();
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand cmd = DB.GetCmd(conn, "GetPastChanges"))
            {
                cmd.Parameters["@entry_id"].Value = entryId;
                using (var rdr = cmd.ExecuteReader())
                {
                    object[] cols = new object[16];
                    while (rdr.Read())
                    {
                        rdr.GetValues(cols);
                        long whenTicks = ((DateTime)cols[1]).Ticks;
                        string bodyBefore = null;
                        if (!(cols[12] is DBNull)) bodyBefore = (string)cols[12];
                        if (bodyBefore == "") bodyBefore = null;
                        sbyte statusBefore = 99;
                        if (!(cols[13] is DBNull)) statusBefore = (sbyte)(sbyte)cols[13];
                        ChangeItem ci = new ChangeItem
                        {
                            When = new DateTime(whenTicks, DateTimeKind.Utc).ToLocalTime(),
                            User = cols[8] as string,
                            EntryId = (int)cols[0],
                            EntryHead = cols[2] as string,
                            EntryBody = cols[3] as string,
                            Note = cols[4] as string,
                            ChangeType = (ChangeType)(sbyte)cols[5],
                            BulkRef = (int)cols[6],
                            CountA = (int)cols[9],
                            CountB = (cols[10] is DBNull) ? int.MinValue : (int)cols[10],
                            BodyBefore = bodyBefore,
                            StatusBefore = (byte)statusBefore,
                            EntryStatus = (EntryStatus)(sbyte)cols[14],
                        };
                        res.Add(ci);
                    }
                }
            }
            res.Sort((x, y) => y.When.CompareTo(x.When));
            return res;
        }

        /// <summary>
        /// Returns all of an entry's changes (newest first).
        /// </summary>
        public static List<ChangeItem> GetEntryChanges(int entryId)
        {
            List<ChangeItem> res = new List<ChangeItem>();
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand cmd = DB.GetCmd(conn, "GetEntryChanges"))
            {
                cmd.Parameters["@entry_id"].Value = entryId;
                using (var rdr = cmd.ExecuteReader())
                {
                    object[] cols = new object[16];
                    while (rdr.Read())
                    {
                        rdr.GetValues(cols);
                        long whenTicks = ((DateTime)cols[0]).Ticks;
                        string bodyBefore = null;
                        if (!(cols[9] is DBNull)) bodyBefore = (string)cols[9];
                        if (bodyBefore == "") bodyBefore = null;
                        sbyte statusBefore = 99;
                        if (!(cols[10] is DBNull)) statusBefore = (sbyte)cols[10];
                        ChangeItem ci = new ChangeItem
                        {
                            When = new DateTime(whenTicks, DateTimeKind.Utc).ToLocalTime(),
                            User = cols[5] as string,
                            EntryId = entryId,
                            Note = cols[1] as string,
                            ChangeType = (ChangeType)(sbyte)cols[2],
                            BulkRef = (int)cols[3],
                            CountA = (int)cols[6],
                            CountB = (cols[7] is DBNull) ? int.MinValue : (int)cols[7],
                            BodyBefore = bodyBefore,
                            StatusBefore = (byte)statusBefore,
                            EntryStatus = (EntryStatus)(sbyte)cols[11],
                        };
                        res.Add(ci);
                    }
                }
            }
            res.Sort((x, y) => y.When.CompareTo(x.When));
            return res;
        }

        public class History : IDisposable
        {
            private MySqlConnection conn;

            // Reused commands
            private MySqlCommand cmdSelChangePage;
            private MySqlCommand cmdGetChangeCount;
            // ---------------

            public History()
            {
                conn = DB.GetConn();
                cmdSelChangePage = DB.GetCmd(conn, "SelModifPage");
                cmdGetChangeCount = DB.GetCmd(conn, "GetChangeCount");
            }

            /// <summary>
            /// Gets the total number of changes (history items).
            /// </summary>
            public int GetChangeCount()
            {
                Int64 count = (Int64)cmdGetChangeCount.ExecuteScalar();
                return (int)count;
            }


            public List<ChangeItem> GetChangePage(int pageStart, int pageLen)
            {
                List<ChangeItem> res = new List<ChangeItem>();
				cmdSelChangePage.Parameters["@page_start"].Value = pageStart;
                cmdSelChangePage.Parameters["@page_len"].Value = pageLen;
				using (MySqlDataReader rdr = cmdSelChangePage.ExecuteReader())
                {
                    object[] cols = new object[16];
					while (rdr.Read())
                    {
                        rdr.GetValues(cols);
                        long whenTicks = ((DateTime)cols[1]).Ticks;
                        string bodyBefore = null;
                        if (!(cols[13] is DBNull)) bodyBefore = (string)cols[13];
                        if (bodyBefore == "") bodyBefore = null;
                        sbyte statusBefore = 99;
                        if (!(cols[14] is DBNull)) statusBefore = (sbyte)cols[14];
                        ChangeItem ci = new ChangeItem
                        {
                            When = new DateTime(whenTicks, DateTimeKind.Utc).ToLocalTime(),
                            User = cols[9] as string,
                            EntryId = (cols[2] is DBNull) ? -1 : (int)cols[2],
                            EntryHead = (cols[3] is DBNull) ? null : cols[3] as string,
                            EntryBody = (cols[4] is DBNull) ? null : cols[4] as string,
                            Note = cols[5] as string,
                            ChangeType = (ChangeType)(sbyte)cols[6],
                            BulkRef = (int)cols[7],
                            CountA = (int)cols[10],
                            CountB = (cols[11] is DBNull) ? int.MinValue : (int)cols[11],
                            BodyBefore = bodyBefore,
                            StatusBefore = (byte)statusBefore,
                            EntryStatus = (cols[15] is DBNull) ? EntryStatus.Neutral : (EntryStatus)(sbyte)cols[15],
                        };
                        res.Add(ci);
                    }
                }
                return res;
            }

            public void Dispose()
            {
                if (cmdGetChangeCount != null) cmdGetChangeCount.Dispose();
                if (cmdSelChangePage != null) cmdSelChangePage.Dispose();
                if (conn != null) conn.Dispose();
            }
        }
    }
}