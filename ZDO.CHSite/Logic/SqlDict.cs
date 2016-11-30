using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

using ZD.Common;

namespace ZDO.CHSite.Logic
{
    public partial class SqlDict
    {
        /// <summary>
        /// My own logger.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Pinyin normalizer/helper.
        /// </summary>
        private readonly Pinyin pinyin;

        /// <summary>
        /// In-memort index.
        /// </summary>
        private readonly Index index;

        /// <summary>
        /// Ctor: init app-wide singleton.
        /// </summary>
        public SqlDict(ILoggerFactory lf)
        {
            if (lf != null) logger = lf.CreateLogger(GetType().FullName);
            else logger = new DummyLogger();
            logger.LogInformation("SQL dictionary initializing...");
            pinyin = new Pinyin();
            index = new Index(logger, pinyin);
            logger.LogInformation("SQL dictionary initialized.");
        }

        /// <summary>
        /// Reloads index from DB. (For dev only: so system can continue running after recreating DB.)
        /// </summary>
        public void ReloadIndex()
        {
            index.Reload();
        }

        public SimpleBuilder GetSimpleBuilder(int userId)
        {
            return new SimpleBuilder(index, userId);
        }

        /// <summary>
        /// Gets a dictionary builder for all-out import from full versioned dictionary file.
        /// </summary>
        public ImportBuilder GetBulkBuilder(string workingFolder, HashSet<string> users, Dictionary<int, ImportBuilder.BulkChangeInfo> bulks)
        {
            return new ImportBuilder(index, workingFolder, users, bulks);
        }

        public class HeadAndTrg
        {
            public readonly string Head;
            public readonly string Trg;
            public HeadAndTrg(string head, string trg)
            {
                Head = head;
                Trg = trg;
            }
        }

        public static bool DoesHeadExist(string head)
        {
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand cmd = DB.GetCmd(conn, "SelCountHead"))
            {
                cmd.Parameters["@hw"].Value = head;
                Int64 count = (Int64)cmd.ExecuteScalar();
                return count > 0;
            }
        }

        public static void GetEntryById(int id, out string hw, out string trg, out EntryStatus status)
        {
            hw = trg = null;
            status = EntryStatus.Neutral;
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand cmd = DB.GetCmd(conn, "SelEntryById"))
            {
                cmd.Parameters["@id"].Value = id;
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        hw = rdr.GetString("hw");
                        trg = rdr.GetString("trg");
                        sbyte sx = rdr.GetSByte("status");
                        if (sx == 0) status = EntryStatus.Neutral;
                        else if (sx == 2) status = EntryStatus.Flagged;
                        else if (sx == 1) status = EntryStatus.Approved;
                        else throw new Exception("Invalid status in DB: " + sx);
                    }
                }
            }
        }

        public static List<HeadAndTrg> GetEntriesBySimp(string simp)
        {
            return null;
        }
    }
}
