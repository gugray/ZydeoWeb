using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
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
        /// Target stopwords (not indexed as prefixes/postfixes, only found in full).
        /// </summary>
        private readonly HashSet<string> trgStopWords = new HashSet<string>();

        private static readonly int ScoreNew = 4;
        private static readonly int ScoreEdit = 2;
        private static readonly int ScoreOther = 1;

        /// <summary>
        /// Ctor: init app-wide singleton.
        /// </summary>
        public SqlDict(ILoggerFactory lf, Mutation mut)
        {
            if (lf != null) logger = lf.CreateLogger(GetType().FullName);
            else logger = new DummyLogger();
            logger.LogInformation("SQL dictionary initializing...");

            if (mut == Mutation.CHD)
            {
                Assembly a = typeof(SqlDict).GetTypeInfo().Assembly;
                string fileName = "ZDO.CHSite.files.other.chd-trg-stops.txt";
                using (Stream s = a.GetManifestResourceStream(fileName))
                using (StreamReader sr = new StreamReader(s))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line != "") trgStopWords.Add(line);
                    }
                }
            }

            pinyin = new Pinyin();
            index = new Index(logger, pinyin, trgStopWords);

            logger.LogInformation("SQL dictionary initialized.");
        }

        public int EntryCount
        {
            get { return index.EntryCount; }
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
        public ImportBuilder GetBulkBuilder(HashSet<string> users, Dictionary<int, ImportBuilder.BulkChangeInfo> bulks)
        {
            return new ImportBuilder(index, users, bulks);
        }

        /// <summary>
        /// Gets a dictionary builder for importing a changeset as a single bulk
        /// </summary>
        public ImportBuilder GetBulkBuilder(string userName, string comment)
        {
            return new ImportBuilder(index, userName, comment);
        }

        public bool IsTrgStop(string word)
        {
            return trgStopWords.Contains(word);
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

        public static bool DoesHeadExist(string hw)
        {
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand cmd = DB.GetCmd(conn, "SelCountHead"))
            {
                cmd.Parameters["@hw"].Value = hw;
                Int64 count = (Int64)cmd.ExecuteScalar();
                return count > 0;
            }
        }

        public static CedictEntry GetEntryByHead(string hw)
        {
            int entryId = -1;
            string trg = null;
            EntryStatus status = EntryStatus.Neutral;
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand cmd = DB.GetCmd(conn, "SelEntryByHead"))
            {
                cmd.Parameters["@hw"].Value = hw;
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        entryId = rdr.GetInt32("id");
                        trg = rdr.GetString("trg");
                        sbyte sx = rdr.GetSByte("status");
                        if (sx == 0) status = EntryStatus.Neutral;
                        else if (sx == 2) status = EntryStatus.Flagged;
                        else if (sx == 1) status = EntryStatus.Approved;
                        else throw new Exception("Invalid status in DB: " + sx);
                    }
                }
            }
            if (entryId == -1) return null;
            CedictEntry entry = Utils.BuildEntry(hw, trg);
            entry.Status = status;
            entry.StableId = entryId;
            return entry;
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

        public static DateTime GetLatestChangeUtc()
        {
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand cmd = DB.GetCmd(conn, "GetLatestChange"))
            {
                object o = cmd.ExecuteScalar();
                if (o == null || o is DBNull) return DateTime.MinValue;
                long ticks = ((DateTime)o).Ticks;
                return new DateTime(ticks, DateTimeKind.Utc);
            }
        }
    }
}
