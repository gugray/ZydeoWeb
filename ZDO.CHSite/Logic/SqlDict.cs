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

        public BulkBuilder GetBulkBuilder(string workingFolder, int userId, string note, bool foldHistory)
        {
            return new BulkBuilder(index, workingFolder, userId, note, foldHistory);
        }

        public Query GetQuery()
        {
            return new Query(index);
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

        public static List<HeadAndTrg> GetEntriesBySimp(string simp)
        {
            return null;
        }
    }
}