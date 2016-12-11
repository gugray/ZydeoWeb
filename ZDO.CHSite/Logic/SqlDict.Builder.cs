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
	public partial class SqlDict
    {
        #region Builder base

        /// <summary>
        /// Builder base class: owns common builder resources, contains shared logic.
        /// </summary>
        public class Builder : IDisposable
        {
            /// <summary>
            /// User-initiated status transitions that show up in the history.
            /// </summary>
            public enum StatusChange
            {
                None,
                Approve,
                Flag,
                Unflag,
            }

            /// <summary>
            /// Reference to the singleton's in-memory index.
            /// </summary>
            protected readonly Index index;

            /// <summary>
            /// Tokenizer for target language.
            /// </summary>
            protected readonly Tokenizer tokenizer;

            /// <summary>
            /// DB connection I'll be using throughout build. Owned.
            /// </summary>
            protected MySqlConnection conn;

            /// <summary>
            /// Chinese word frequency provider.
            /// </summary>
            protected Freq freq;

            /// <summary>
            /// Random generator for new entry IDs.
            /// </summary>
            protected readonly Random rnd = new Random();

            /// <summary>
            /// Current DB transaction.
            /// </summary>
            protected MySqlTransaction tr = null;

            // Reused commands
            private MySqlCommand cmdInsBinaryEntry;
            private MySqlCommand cmdInsHanziInstance;
            private MySqlCommand cmdInsSkeletonEntry;
            private MySqlCommand cmdSelHwByEntryId;
            private MySqlCommand cmdUpdSkeletonEntry;
            protected MySqlCommand cmdInsModif;
            protected MySqlCommand cmdInsModifPreCounts1;
            protected MySqlCommand cmdInsModifPreCounts2;
            protected MySqlCommand cmdSelBinByEntryId;
            protected MySqlCommand cmdUpdEntryTrg;
            protected MySqlCommand cmdSelEntryStatus;
            protected MySqlCommand cmdUpdEntryStatus;
            protected MySqlCommand cmdUpdBinaryEntry;
            protected MySqlCommand cmdAddContribScore;
            // Reused commands owned for the index module
            protected Index.StorageCommands indexCommands = new Index.StorageCommands();
            // ---------------

            /// <summary>
            /// Ctor: init DB connection; shared builder resources.
            /// </summary>
            protected Builder(Index index)
            {
                if (!index.Lock.TryEnterWriteLock(15000))
                    throw new Exception("Write lock timeout.");
                try
                {
                    this.index = index;
                    tokenizer = new Tokenizer();
                    conn = DB.GetConn();
                    freq = new Freq(conn);
                    // Shared builder commands, owned here in base class
                    cmdInsBinaryEntry = DB.GetCmd(conn, "InsBinaryEntry");
                    cmdInsHanziInstance = DB.GetCmd(conn, "InsHanziInstance");
                    cmdInsSkeletonEntry = DB.GetCmd(conn, "InsSkeletonEntry");
                    cmdSelHwByEntryId = DB.GetCmd(conn, "SelHwByEntryId");
                    cmdUpdSkeletonEntry = DB.GetCmd(conn, "UpdSkeletonEntry");
                    cmdSelBinByEntryId = DB.GetCmd(conn, "SelBinByEntryId");
                    cmdInsModif = DB.GetCmd(conn, "InsModif"); 
                    cmdInsModifPreCounts1 = DB.GetCmd(conn, "InsModifPreCounts1");
                    cmdInsModifPreCounts2 = DB.GetCmd(conn, "InsModifPreCounts2");
                    cmdUpdEntryTrg = DB.GetCmd(conn, "UpdEntryTrg");
                    cmdSelEntryStatus = DB.GetCmd(conn, "SelEntryStatus");
                    cmdUpdEntryStatus = DB.GetCmd(conn, "UpdEntryStatus");
                    cmdUpdBinaryEntry = DB.GetCmd(conn, "UpdBinaryEntry");
                    cmdAddContribScore = DB.GetCmd(conn, "AddContribScore");
                    // Commands owned for the index module
                    indexCommands.CmdDelEntryHanziInstances = DB.GetCmd(conn, "DelEntryHanziInstances");
                    indexCommands.CmdInsHanziInstance = DB.GetCmd(conn, "InsHanziInstance");
                    indexCommands.CmdDelEntryTrgInstances = DB.GetCmd(conn, "DelEntryTrgInstances");
                    indexCommands.CmdInsNormWord = DB.GetCmd(conn, "InsNormWord");
                    indexCommands.CmdInsTrgInstance = DB.GetCmd(conn, "InsTrgInstance");
                    indexCommands.CmdInsUpdPrefixWord = DB.GetCmd(conn, "InsUpdPrefixWord");
                    indexCommands.CmdDelEntrySyllInstances = DB.GetCmd(conn, "DelEntrySyllInstances");
                    indexCommands.CmdInsSyllInstance = DB.GetCmd(conn, "InsSyllInstance");
                }
                catch
                {
                    index.Lock.ExitWriteLock();
                    throw;
                }
            }

            /// <summary>
            /// Close files, clean up DB resources.
            /// </summary>
            protected virtual void DoDispose()
            {
                try
                {
                    if (tr != null) { tr.Rollback(); tr.Dispose(); tr = null; }

                    if (indexCommands.CmdInsSyllInstance != null) indexCommands.CmdInsSyllInstance.Dispose();
                    if (indexCommands.CmdDelEntrySyllInstances != null) indexCommands.CmdDelEntrySyllInstances.Dispose();
                    if (indexCommands.CmdInsUpdPrefixWord != null) indexCommands.CmdInsUpdPrefixWord.Dispose();
                    if (indexCommands.CmdInsTrgInstance != null) indexCommands.CmdInsTrgInstance.Dispose();
                    if (indexCommands.CmdInsNormWord != null) indexCommands.CmdInsNormWord.Dispose();
                    if (indexCommands.CmdDelEntryTrgInstances != null) indexCommands.CmdDelEntryTrgInstances.Dispose();
                    if (indexCommands.CmdInsHanziInstance != null) indexCommands.CmdInsHanziInstance.Dispose();
                    if (indexCommands.CmdDelEntryHanziInstances != null) indexCommands.CmdDelEntryHanziInstances.Dispose();

                    if (cmdInsModifPreCounts1 != null) cmdInsModifPreCounts1.Dispose();
                    if (cmdInsModifPreCounts2 != null) cmdInsModifPreCounts2.Dispose();
                    if (cmdInsModif != null) cmdInsModif.Dispose();
                    if (cmdSelHwByEntryId != null) cmdSelHwByEntryId.Dispose();
                    if (cmdUpdSkeletonEntry != null) cmdUpdSkeletonEntry.Dispose();
                    if (cmdSelBinByEntryId != null) cmdSelBinByEntryId.Dispose();
                    if (cmdInsSkeletonEntry != null) cmdInsSkeletonEntry.Dispose();
                    if (cmdInsHanziInstance != null) cmdInsHanziInstance.Dispose();
                    if (cmdInsBinaryEntry != null) cmdInsBinaryEntry.Dispose();
                    if (cmdUpdEntryTrg != null) cmdUpdEntryTrg.Dispose();
                    if (cmdSelEntryStatus != null) cmdSelEntryStatus.Dispose();
                    if (cmdUpdEntryStatus != null) cmdUpdEntryStatus.Dispose();
                    if (cmdUpdBinaryEntry != null) cmdUpdBinaryEntry.Dispose();
                    if (cmdAddContribScore != null) cmdAddContribScore.Dispose();

                    if (freq != null) freq.Dispose();
                    if (conn != null) conn.Dispose();
                }
                finally { index.Lock.ExitWriteLock(); }
            }

            /// <summary>
            /// Dispose of non-managed resources, particularly MySQL.
            /// </summary>
            public void Dispose()
            {
                DoDispose();
            }

            protected static void checkRestrictions(string simp, string trg)
            {
                // TO-DO: anything else
                // Data type size restrictions: number and length of senses, ...
                if (simp.Length > 16) throw new Exception("Headword must not exceed 16 syllables.");
                if (trg.Length > 1024) throw new Exception("Translation, in CEDICT format, must not exceed 1024 characters.");
            }

            protected int reserveEntryId()
            {
                int id;
                while (true)
                {
                    id = rnd.Next();
                    cmdInsSkeletonEntry.Parameters["@id"].Value = id;
                    cmdInsSkeletonEntry.ExecuteNonQuery();
                    cmdSelHwByEntryId.Parameters["@id"].Value = id;
                    object o = cmdSelHwByEntryId.ExecuteScalar();
                    if (o is DBNull) break;
                }
                return id;
            }

            protected void storeEntry(string simp, string head, string trg, EntryStatus status, int binId, int id)
            {
                // Use provided ID. Throw if it's not unique.
                cmdInsSkeletonEntry.Parameters["@id"].Value = id;
                cmdInsSkeletonEntry.ExecuteNonQuery();
                cmdSelHwByEntryId.Parameters["@id"].Value = id;
                object o = cmdSelHwByEntryId.ExecuteScalar();
                if (!(o is DBNull)) throw new Exception("Entry ID already exists.");
                // Now store values in skeleton
                cmdUpdSkeletonEntry.Parameters["@id"].Value = id;
                cmdUpdSkeletonEntry.Parameters["@hw"].Value = head;
                cmdUpdSkeletonEntry.Parameters["@trg"].Value = trg;
                cmdUpdSkeletonEntry.Parameters["@simp_hash"].Value = CedictEntry.Hash(simp);
                cmdUpdSkeletonEntry.Parameters["@status"].Value = (sbyte)status;
                cmdUpdSkeletonEntry.Parameters["@deleted"].Value = 0;
                cmdUpdSkeletonEntry.Parameters["@bin_id"].Value = binId;
                cmdUpdSkeletonEntry.ExecuteNonQuery();
            }

            protected CedictEntry unindexEntry(int entryId)
            {
                // Get our entry from binary
                byte[] buf = new byte[32768];
                CedictEntry entry = null;
                int binId = -1;
                cmdSelBinByEntryId.Parameters["@id"].Value = entryId;
                using (var rdr = cmdSelBinByEntryId.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        binId = (int)rdr.GetInt32(1);
                        int len = (int)rdr.GetBytes(0, 0, buf, 0, buf.Length);
                        using (BinReader br = new BinReader(buf, len))
                        {
                            entry = new CedictEntry(br);
                        }
                    }
                }
                // Get hanzi
                HashSet<char> hAll = new HashSet<char>();
                foreach (char c in entry.ChSimpl) hAll.Add(c);
                foreach (char c in entry.ChTrad) hAll.Add(c);
                // Get target tokens
                List<Token> allToks = new List<Token>();
                for (int i = 0; i != entry.SenseCount; ++i)
                {
                    CedictSense sense = entry.GetSenseAt(i);
                    List<Token> toks = tokenizer.Tokenize(sense.Equiv);
                    allToks.AddRange(toks);
                }
                // File to unindex
                index.FileToUnindex(binId, hAll, allToks, entry.Pinyin);
                // Return old entry we retrieved
                return entry;
            }

            protected int indexEntry(CedictEntry entry)
            {
                MemoryStream ms = new MemoryStream();
                BinWriter bw = new BinWriter(ms);
                entry.Serialize(bw);
                cmdInsBinaryEntry.Parameters["@data"].Value = ms.ToArray();
                cmdInsBinaryEntry.ExecuteNonQuery();
                int binId = (int)cmdInsBinaryEntry.LastInsertedId;
                // Index different parts of the entry
                indexHanzi(entry, binId);
                indexPinyin(entry, binId);
                indexSenses(entry, binId);
                return binId;
            }

            private void indexPinyin(CedictEntry entry, int entryId)
            {
                index.FileToIndex(entryId, entry.Pinyin);
            }

            private void indexSenses(CedictEntry entry, int entryId)
            {
                for (int i = 0;  i != entry.SenseCount; ++i)
                {
                    CedictSense sense = entry.GetSenseAt(i);
                    List<Token> toks = tokenizer.Tokenize(sense.Equiv);
                    index.FileToIndex(entryId, (byte)i, toks);
                }
            }

            private void indexHanzi(CedictEntry entry, int entryId)
            {
                // Distinct Hanzi in simplified and traditional HW
                HashSet<char> simpSet = new HashSet<char>();
                foreach (char c in entry.ChSimpl) simpSet.Add(c);
                int simpCount = simpSet.Count;
                HashSet<char> tradSet = new HashSet<char>();
                foreach (char c in entry.ChTrad) tradSet.Add(c);
                int tradCount = tradSet.Count;
                // Extract intersection
                HashSet<char> cmnSet = new HashSet<char>();
                foreach (char c in simpSet)
                    if (tradSet.Contains(c)) cmnSet.Add(c);
                foreach (char c in cmnSet) { simpSet.Remove(c); tradSet.Remove(c); }
                // File to index
                index.FileToIndex(entryId, simpSet, tradSet, cmnSet);
            }

            /// <summary>
            /// Gets score of contribution (as per modifs.chg value).
            /// </summary>
            protected int getContribScore(byte chg)
            {
                if (chg == 0) return SqlDict.ScoreNew;
                else if (chg == 2) return SqlDict.ScoreEdit;
                else return SqlDict.ScoreOther;
            }
        }

        #endregion

        #region Simple builder

        public class SimpleBuilder : Builder
        {
            private readonly int userId;

            // Reused commands
            // ---------------

            public SimpleBuilder(Index index, int userId)
                : base(index)
            {
                this.userId = userId;
            }

            protected override void DoDispose()
            {
                // This must come at the end. Will close connection, which we need for disposing of our own stuff.
                base.DoDispose();
            }

            public void ChangeTarget(int userId, int entryId, string trg, string note)
            {
                tr = conn.BeginTransaction();

                // Retrieve current entry
                string oldHw, oldTrg;
                EntryStatus status;
                GetEntryById(entryId, out oldHw, out oldTrg, out status);

                // Create new modif
                cmdInsModif.Parameters["@parent_id"].Value = -1;
                cmdInsModif.Parameters["@bulk_ref"].Value = -1;
                cmdInsModif.Parameters["@hw_before"].Value = "";
                cmdInsModif.Parameters["@trg_before"].Value = oldTrg;
                cmdInsModif.Parameters["@status_before"].Value = 99;
                cmdInsModif.Parameters["@timestamp"].Value = DateTime.UtcNow;
                cmdInsModif.Parameters["@user_id"].Value = userId;
                cmdInsModif.Parameters["@note"].Value = note;
                cmdInsModif.Parameters["@chg"].Value = (byte)Entities.ChangeType.Edit;
                cmdInsModif.Parameters["@entry_id"].Value = entryId;
                cmdInsModif.ExecuteNonQuery();
                // Count contrib score
                cmdAddContribScore.Parameters["@id"].Value = userId;
                cmdAddContribScore.Parameters["@val"].Value = getContribScore((byte)Entities.ChangeType.Edit);
                cmdAddContribScore.ExecuteNonQuery();
                // Update previous version counts
                cmdInsModifPreCounts1.Parameters["@top_id"].Value = cmdInsModif.LastInsertedId;
                cmdInsModifPreCounts1.Parameters["@entry_id"].Value = entryId;
                cmdInsModifPreCounts1.ExecuteNonQuery();
                cmdInsModifPreCounts2.Parameters["@top_id"].Value = cmdInsModif.LastInsertedId;
                cmdInsModifPreCounts2.Parameters["@entry_id"].Value = entryId;
                cmdInsModifPreCounts2.ExecuteNonQuery();

                // Unindex current entry: removes binary record, returns entry deserialized from it
                CedictEntry oldEntry = unindexEntry(entryId);
                // Create new entry, index it
                CedictParser parser = new CedictParser();
                CedictEntry newEntry = parser.ParseEntry(oldHw + " " + trg, -1, null);
                // Infuse corpus frequency: same as before; HW didn't change
                newEntry.Freq = oldEntry.Freq;
                // Infuse stable ID: same as before (our entry ID)
                newEntry.StableId = oldEntry.StableId;
                // Infuse status: same as before
                newEntry.Status = oldEntry.Status;
                // Index new entry
                int newBinId = indexEntry(newEntry);
                // Update entry target itself
                cmdUpdEntryTrg.Parameters["@id"].Value = entryId;
                cmdUpdEntryTrg.Parameters["@trg"].Value = trg;
                cmdUpdEntryTrg.Parameters["@bin_id"].Value = newBinId;
                cmdUpdEntryTrg.ExecuteNonQuery();
                // Have index commit filed changes: unindex and index
                index.ApplyChanges(indexCommands);

                // Commit. Otherwise, dispose will roll all this back if it finds non-null transaction.
                tr.Commit(); tr.Dispose(); tr = null;
            }

            public void CommentEntry(int entryId, string note, StatusChange change)
            {
                tr = conn.BeginTransaction();

                // Is there a status change?
                byte changeType = (byte)Entities.ChangeType.Note;
                sbyte oldStatus = 99;
                sbyte newStatus = 99;
                if (change != StatusChange.None)
                {
                    // Verify that status change is legit
                    cmdSelEntryStatus.Parameters["@id"].Value = entryId;
                    oldStatus = (sbyte)cmdSelEntryStatus.ExecuteScalar();
                    if (change == StatusChange.Approve)
                    {
                        if (oldStatus == 1) throw new Exception("Entry is already approved.");
                        newStatus = 1;
                    }
                    if (change == StatusChange.Flag)
                    {
                        if (oldStatus == 2) throw new Exception("Entry is already flagged.");
                        newStatus = 2;
                    }
                    if (change == StatusChange.Unflag)
                    {
                        if (oldStatus != 2) throw new Exception("Entry is not flagged.");
                        newStatus = 0;
                    }
                    // Change type is now "status"
                    changeType = (byte)Entities.ChangeType.StatusChange;
                    // Update entry status
                    cmdUpdEntryStatus.Parameters["@id"].Value = entryId;
                    cmdUpdEntryStatus.Parameters["@status"].Value = newStatus;
                    cmdUpdEntryStatus.ExecuteNonQuery();
                    // Recreate binary entry too: contains status inside
                    byte[] buf = new byte[32768];
                    int binId = -1;
                    CedictEntry entry = null;
                    cmdSelBinByEntryId.Parameters["@id"].Value = entryId;
                    using (var rdr = cmdSelBinByEntryId.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            binId = (int)rdr.GetInt32(1);
                            int len = (int)rdr.GetBytes(0, 0, buf, 0, buf.Length);
                            using (BinReader br = new BinReader(buf, len))
                            {
                                entry = new CedictEntry(br);
                            }
                        }
                    }
                    entry.Status = (EntryStatus)newStatus;
                    MemoryStream ms = new MemoryStream();
                    BinWriter bw = new BinWriter(ms);
                    entry.Serialize(bw);
                    cmdUpdBinaryEntry.Parameters["@data"].Value = ms.ToArray();
                    cmdUpdBinaryEntry.Parameters["@id"].Value = binId;
                    cmdUpdBinaryEntry.ExecuteNonQuery();
                }

                // No index update, just a modif
                cmdInsModif.Parameters["@parent_id"].Value = -1;
                cmdInsModif.Parameters["@bulk_ref"].Value = -1;
                cmdInsModif.Parameters["@hw_before"].Value = "";
                cmdInsModif.Parameters["@trg_before"].Value = "";
                cmdInsModif.Parameters["@timestamp"].Value = DateTime.UtcNow;
                cmdInsModif.Parameters["@user_id"].Value = userId;
                cmdInsModif.Parameters["@note"].Value = note;
                cmdInsModif.Parameters["@chg"].Value = changeType;
                cmdInsModif.Parameters["@entry_id"].Value = entryId;
                if (change == StatusChange.None) cmdInsModif.Parameters["@status_before"].Value = 99;
                else cmdInsModif.Parameters["@status_before"].Value = oldStatus;
                cmdInsModif.ExecuteNonQuery();
                // Count contrib score
                cmdAddContribScore.Parameters["@id"].Value = userId;
                cmdAddContribScore.Parameters["@val"].Value = getContribScore(changeType);
                cmdAddContribScore.ExecuteNonQuery();
                // Update previous version counts
                cmdInsModifPreCounts1.Parameters["@top_id"].Value = cmdInsModif.LastInsertedId;
                cmdInsModifPreCounts1.Parameters["@entry_id"].Value = entryId;
                cmdInsModifPreCounts1.ExecuteNonQuery();
                cmdInsModifPreCounts2.Parameters["@top_id"].Value = cmdInsModif.LastInsertedId;
                cmdInsModifPreCounts2.Parameters["@entry_id"].Value = entryId;
                cmdInsModifPreCounts2.ExecuteNonQuery();

                // Commit. Otherwise, dispose will roll all this back if it finds non-null transaction.
                tr.Commit(); tr.Dispose(); tr = null;
            }

            /// <summary>
            /// Adds a single new entry to the dictionary.
            /// </summary>
            public void NewEntry(CedictEntry entry, string note)
            {
                tr = conn.BeginTransaction();
                string head, trg;
                CedictWriter.Write(entry, out head, out trg);
                // Check restrictions - can end up dropped entry
                checkRestrictions(entry.ChSimpl, trg);
                // Check for duplicate
                if (SqlDict.DoesHeadExist(head)) throw new Exception("Headword already exists: " + head);

                // Reserve entry ID
                int entryId = reserveEntryId();

                // Infuse corpus frequency
                int iFreq = freq.GetFreq(entry.ChSimpl);
                ushort uFreq = iFreq > ushort.MaxValue ? ushort.MaxValue : (ushort)iFreq;
                entry.Freq = uFreq;
                // New entry's status is neutral aka new
                entry.Status = EntryStatus.Neutral;

                // Serialize, store in DB, index
                entry.StableId = entryId;
                int binId = indexEntry(entry);
                // Have index commit filed change
                index.ApplyChanges(indexCommands);

                // Populate entries table
                storeEntry(entry.ChSimpl, head, trg, EntryStatus.Neutral, binId, entryId);
                // Record change
                cmdInsModif.Parameters["@parent_id"].Value = -1;
                cmdInsModif.Parameters["@bulk_ref"].Value = -1;
                cmdInsModif.Parameters["@hw_before"].Value = "";
                cmdInsModif.Parameters["@trg_before"].Value = "";
                cmdInsModif.Parameters["@status_before"].Value = 99;
                cmdInsModif.Parameters["@timestamp"].Value = DateTime.UtcNow;
                cmdInsModif.Parameters["@user_id"].Value = userId;
                cmdInsModif.Parameters["@note"].Value = note;
                cmdInsModif.Parameters["@chg"].Value = (byte)Entities.ChangeType.New;
                cmdInsModif.Parameters["@entry_id"].Value = entryId;
                cmdInsModif.ExecuteNonQuery();
                // Count contrib score
                cmdAddContribScore.Parameters["@id"].Value = userId;
                cmdAddContribScore.Parameters["@val"].Value = getContribScore((byte)Entities.ChangeType.New);
                cmdAddContribScore.ExecuteNonQuery();
                // Update previous version counts
                cmdInsModifPreCounts1.Parameters["@top_id"].Value = cmdInsModif.LastInsertedId;
                cmdInsModifPreCounts1.Parameters["@entry_id"].Value = entryId;
                cmdInsModifPreCounts1.ExecuteNonQuery();
                cmdInsModifPreCounts2.Parameters["@top_id"].Value = cmdInsModif.LastInsertedId;
                cmdInsModifPreCounts2.Parameters["@entry_id"].Value = entryId;
                cmdInsModifPreCounts2.ExecuteNonQuery();

                // Commit. Otherwise, dispose will roll all this back if it finds non-null transaction.
                tr.Commit(); tr.Dispose(); tr = null;
            }
        }

        #endregion

        #region Bulk builder

        public class ImportBuilder : Builder
        {
            public class BulkChangeInfo
            {
                public DateTime Timestamp;
                public string UserName;
                public string Comment;
                public int NewEntries;
                public int ChangedEntries;
            }

            /// <summary>
            /// Counter for batch transactions.
            /// </summary>
            private int count = 0;

            // Reused commands
            private MySqlCommand cmdInsBulkModif;
            private MySqlCommand cmdUpdBulkModifCounts;
            private MySqlCommand cmdSelUserByName;
            private MySqlCommand cmdInsImplicitUser;
            // ---------------

            /// <summary>
            /// Maps user names to their IDs.
            /// </summary>
            private readonly Dictionary<string, int> userToId = new Dictionary<string, int>();

            /// <summary>
            /// Maps input's "bulk change references" to ID of bulk modif record created.
            /// </summary>
            private readonly Dictionary<int, int> bulkRefToModifId = new Dictionary<int, int>();

            /// <summary>
            /// Maps user IDs to the contribution score accumulated during the import.
            /// </summary>
            private readonly Dictionary<int, int> userIdToScore = new Dictionary<int, int>();

            /// <summary>
            /// Ctor: initialize bulk builder resources.
            /// </summary>
            public ImportBuilder(Index index, string workingFolder, HashSet<string> users, Dictionary<int, BulkChangeInfo> bulks)
                : base(index)
            {
                tr = conn.BeginTransaction();

                cmdInsBulkModif = DB.GetCmd(conn, "InsBulkModif");
                cmdUpdBulkModifCounts = DB.GetCmd(conn, "UpdBulkModifCounts");
                cmdInsImplicitUser = DB.GetCmd(conn, "InsImplicitUser");
                cmdSelUserByName = DB.GetCmd(conn, "SelUserByName");
                // Gather user IDs up front
                // Insert missing users as implicit users
                foreach (var x in users)
                {
                    string[] userParts = x.Split('\t');
                    string userName = userParts[0];
                    string userAbout = userParts[1];
                    int userId = -1;
                    cmdSelUserByName.Parameters["@user_name"].Value = userName;
                    using (var rdr = cmdSelUserByName.ExecuteReader())
                    {
                        while (rdr.Read()) userId = rdr.GetInt32("id");
                    }
                    if (userId == -1)
                    {
                        cmdInsImplicitUser.Parameters["@user_name"].Value = userName;
                        cmdInsImplicitUser.Parameters["@registered"].Value = DateTime.UtcNow;
                        cmdInsImplicitUser.Parameters["@about"].Value = userAbout;
                        cmdInsImplicitUser.ExecuteNonQuery();
                        userId = (int)cmdInsImplicitUser.LastInsertedId;
                    }
                    userToId[userName] = userId;
                    userIdToScore[userId] = 0;
                }
                // Insert modif records for every bulk change
                foreach (var x in bulks)
                {
                    cmdInsBulkModif.Parameters["@timestamp"].Value = x.Value.Timestamp;
                    cmdInsBulkModif.Parameters["@user_id"].Value = userToId[x.Value.UserName];
                    cmdInsBulkModif.Parameters["@note"].Value = x.Value.Comment;
                    cmdInsBulkModif.Parameters["@bulk_ref"].Value = x.Key;
                    cmdInsBulkModif.ExecuteNonQuery();
                    int modifId = (int)cmdInsBulkModif.LastInsertedId;
                    bulkRefToModifId[x.Key] = modifId;
                    cmdUpdBulkModifCounts.Parameters["@id"].Value = cmdInsBulkModif.LastInsertedId;
                    cmdUpdBulkModifCounts.Parameters["@count_a"].Value = x.Value.NewEntries;
                    cmdUpdBulkModifCounts.Parameters["@count_b"].Value = x.Value.ChangedEntries;
                    cmdUpdBulkModifCounts.ExecuteNonQuery();
                }
            }

            /// <summary>
            /// Dispose of bulk builder resources.
            /// </summary>
            protected override void DoDispose()
            {
                if (cmdSelUserByName != null) cmdSelUserByName.Dispose();
                if (cmdInsImplicitUser != null) cmdInsImplicitUser.Dispose();
                if (cmdInsBulkModif != null) cmdInsBulkModif.Dispose();
                if (cmdUpdBulkModifCounts != null) cmdUpdBulkModifCounts.Dispose();

                // This must come at the end. Will close connection, which we need for disposing of our own stuff.
                base.DoDispose();
            }

            /// <summary>
            /// Adds entry with full history (oldest first).
            /// </summary>
            public bool AddEntry(int entryId, List<EntryVersion> vers)
            {
                ++count;
                // Cycle through transactions
                if (count % 3000 == 0)
                {
                    // Make index apply changes
                    index.ApplyChanges(indexCommands);
                    // Commit to DB; start new transaction
                    tr.Commit(); tr.Dispose(); tr = null;
                    tr = conn.BeginTransaction();
                }
                
                // The final entry: one in the last version
                CedictEntry entry = vers[vers.Count - 1].Entry;
                entry.Status = vers[vers.Count - 1].Status;
                // Infuse corpus frequency
                int iFreq = freq.GetFreq(entry.ChSimpl);
                ushort uFreq = iFreq > ushort.MaxValue ? ushort.MaxValue : (ushort)iFreq;
                entry.Freq = uFreq;
                string hw, trg;
                CedictWriter.Write(entry, out hw, out trg);
                // Check restrictions. Will skip failing entries.
                try { checkRestrictions(entry.ChSimpl, trg); }
                catch { return false; }
                // Serialize, store in DB, index. Infuse stable ID!
                entry.StableId = entryId;
                int binId = indexEntry(entry);
                // Populate entries table
                storeEntry(entry.ChSimpl, hw, trg, entry.Status, binId, entryId);
                // Working backwards, create MODIF records for each version
                int topModifId = -1;
                for (int i = vers.Count - 1; i >= 0; --i)
                {
                    EntryVersion ver = vers[i];
                    hw = null; trg = null;
                    if (ver.Entry != null) CedictWriter.Write(ver.Entry, out hw, out trg);
                    // Find previous HW and TRG. Then null if no change from previous version to this.
                    string hwPrev = null;
                    string trgPrev = null;
                    if (i > 0)
                    {
                        string hwTmp, trgTmp;
                        CedictWriter.Write(vers[i - 1].Entry, out hwTmp, out trgTmp);
                        if (hwTmp != hw) hwPrev = hwTmp;
                        if (trgTmp != trg) trgPrev = trgTmp;
                    }
                    // Find previous status
                    EntryStatus prevStatus = ver.Status;
                    if (i > 0) prevStatus = vers[i - 1].Status; // Very first modif is never status change LoL
                    // ---
                    int parentId = ver.BulkRef == -1 ? -1 : bulkRefToModifId[ver.BulkRef];
                    int bulkRef = ver.BulkRef <= 0 ? -1 : ver.BulkRef;
                    byte change;
                    if (i == 0) change = 0; // New entry
                    else if (hwPrev != null || trgPrev != null) change = 2; // Edit
                    else if (ver.Status != prevStatus) change = 4; // Status changed
                    else change = 3; // Simply commented
                    // Store in DB
                    cmdInsModif.Parameters["@parent_id"].Value = parentId;
                    cmdInsModif.Parameters["@bulk_ref"].Value = bulkRef;
                    if (hwPrev != null) cmdInsModif.Parameters["@hw_before"].Value = hwPrev;
                    else cmdInsModif.Parameters["@hw_before"].Value = "";
                    if (trgPrev != null) cmdInsModif.Parameters["@trg_before"].Value = trgPrev;
                    else cmdInsModif.Parameters["@trg_before"].Value = "";
                    cmdInsModif.Parameters["@timestamp"].Value = ver.Timestamp;
                    cmdInsModif.Parameters["@user_id"].Value = userToId[ver.User];
                    cmdInsModif.Parameters["@note"].Value = ver.Comment;
                    cmdInsModif.Parameters["@chg"].Value = change;
                    cmdInsModif.Parameters["@entry_id"].Value = entryId;
                    if (change == 4) cmdInsModif.Parameters["@status_before"].Value = prevStatus;
                    else cmdInsModif.Parameters["@status_before"].Value = 99;
                    cmdInsModif.ExecuteNonQuery();
                    // Remember ID of top (latest) modif
                    if (i == vers.Count - 1) topModifId = (int)cmdInsModif.LastInsertedId;
                    // Count contrib score
                    userIdToScore[userToId[ver.User]] += getContribScore(change);
                }
                // Update previous version counts
                cmdInsModifPreCounts1.Parameters["@top_id"].Value = topModifId;
                cmdInsModifPreCounts1.Parameters["@entry_id"].Value = entryId;
                cmdInsModifPreCounts1.ExecuteNonQuery();
                cmdInsModifPreCounts2.Parameters["@top_id"].Value = topModifId;
                cmdInsModifPreCounts2.Parameters["@entry_id"].Value = entryId;
                cmdInsModifPreCounts2.ExecuteNonQuery();
                return true;
            }

            /// <summary>
            /// Finalize pending transaction at the end.
            /// </summary>
            public void CommitRest()
            {
                // Make index apply changes
                index.ApplyChanges(indexCommands);
                // Apply contrib scores -- only now, once, at the very end.
                foreach (var x in userIdToScore)
                {
                    cmdAddContribScore.Parameters["@id"].Value = x.Key;
                    cmdAddContribScore.Parameters["@val"].Value = x.Value;
                    cmdAddContribScore.ExecuteNonQuery();
                }

                // Finish transaction
                tr.Commit(); tr.Dispose(); tr = null;
            }
        }

        #endregion

    }
}
