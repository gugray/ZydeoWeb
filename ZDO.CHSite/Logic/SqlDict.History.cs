using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using MySql.Data.MySqlClient;

using ZD.Common;
using ZDO.CHSite.Entities;

namespace ZDO.CHSite.Logic
{
    public partial class SqlDict
    {
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