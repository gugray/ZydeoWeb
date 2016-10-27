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
        /// Stores and retrieves Chinese (simplified) word frequencies.
        /// </summary>
		public class Freq : IDisposable
        {
            private readonly MySqlConnection conn = null;
            private readonly MySqlCommand cmdInsFreq = null;
            private readonly MySqlCommand cmdSelFreq = null;
            private MySqlTransaction tr = null;
            private int batchCount = 0;

			public Freq()
            {
				try
                {
                    conn = DB.GetConn();
                    cmdInsFreq = DB.GetCmd(conn, "InsFreq");
                    cmdSelFreq = DB.GetCmd(conn, "SelFreq");
                }
				catch { cleanup(); throw; }
            }

			/// <summary>
            /// Use for lookup only, when owner already has a DB connection.
            /// </summary>
            public Freq(MySqlConnection conn)
            {
                try
                {
                    cmdInsFreq = DB.GetCmd(conn, "InsFreq");
                    cmdSelFreq = DB.GetCmd(conn, "SelFreq");
                }
                catch { cleanup(); throw; }
            }

            private void cleanup()
            {
				if (tr != null) { tr.Rollback(); tr.Dispose(); }
                if (cmdSelFreq != null) cmdSelFreq.Dispose();
                if (cmdInsFreq != null) cmdInsFreq.Dispose();
                if (conn != null) conn.Dispose();
            }

			public void Dispose()
            {
                cleanup();
            }

			public int GetFreq(string word)
            {
                int freq = 0;
                cmdSelFreq.Parameters["@word"].Value = word;
                using (MySqlDataReader rdr = cmdSelFreq.ExecuteReader())
                {
                    while (rdr.Read()) freq = rdr.GetInt32(0);
                }
                return freq;
            }

			public void StoreFreq(string word, int freq)
            {
                if (tr == null) tr = conn.BeginTransaction();
                cmdInsFreq.Parameters["@word"].Value = word;
                cmdInsFreq.Parameters["@freq"].Value = freq;
                cmdInsFreq.ExecuteNonQuery();
                ++batchCount;
				if (batchCount == 10000)
                {
                    tr.Commit(); tr.Dispose(); tr = null;
                    batchCount = 0;
                }
            }

			public void CommitRest()
            {
                if (tr == null) return;
                tr.Commit(); tr.Dispose(); tr = null;
            }
        }
    }
}
