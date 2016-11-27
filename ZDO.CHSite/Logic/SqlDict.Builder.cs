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
                    cmdInsModif = DB.GetCmd(conn, "InsModif"); 
                    cmdInsModifPreCounts1 = DB.GetCmd(conn, "InsModifPreCounts1");
                    cmdInsModifPreCounts2 = DB.GetCmd(conn, "InsModifPreCounts2");
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
                    if (cmdInsSkeletonEntry != null) cmdInsSkeletonEntry.Dispose();
                    if (cmdInsHanziInstance != null) cmdInsHanziInstance.Dispose();
                    if (cmdInsBinaryEntry != null) cmdInsBinaryEntry.Dispose();

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

            protected int storeEntry(string simp, string head, string trg, int binId, int id = -1)
            {
                // Come up with new random entry ID?
                if (id == -1)
                {
                    while (true)
                    {
                        id = rnd.Next();
                        cmdInsSkeletonEntry.Parameters["@id"].Value = id;
                        cmdInsSkeletonEntry.ExecuteNonQuery();
                        cmdSelHwByEntryId.Parameters["@id"].Value = id;
                        object o = cmdSelHwByEntryId.ExecuteScalar();
                        if (o is DBNull) break;
                    }
                }
                // Use provided ID. Throw if it's not unique.
                else
                {
                    cmdInsSkeletonEntry.Parameters["@id"].Value = id;
                    cmdInsSkeletonEntry.ExecuteNonQuery();
                    cmdSelHwByEntryId.Parameters["@id"].Value = id;
                    object o = cmdSelHwByEntryId.ExecuteScalar();
                    if (!(o is DBNull)) throw new Exception("Entry ID already exists.");
                }
                // Now store values in skeleton
                cmdUpdSkeletonEntry.Parameters["@id"].Value = id;
                cmdUpdSkeletonEntry.Parameters["@hw"].Value = head;
                cmdUpdSkeletonEntry.Parameters["@trg"].Value = trg;
                cmdUpdSkeletonEntry.Parameters["@simp_hash"].Value = CedictEntry.Hash(simp);
                cmdUpdSkeletonEntry.Parameters["@status"].Value = 0;
                cmdUpdSkeletonEntry.Parameters["@deleted"].Value = 0;
                cmdUpdSkeletonEntry.Parameters["@bin_id"].Value = binId;
                cmdUpdSkeletonEntry.ExecuteNonQuery();
                return id;
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

            public void CommentEntry(int entryId, string note)
            {
                // This only involves storing a modif!
                cmdInsModif.Parameters["@parent_id"].Value = -1;
                cmdInsModif.Parameters["@hw_before"].Value = "";
                cmdInsModif.Parameters["@trg_before"].Value = "";
                cmdInsModif.Parameters["@timestamp"].Value = DateTime.UtcNow;
                cmdInsModif.Parameters["@user_id"].Value = userId;
                cmdInsModif.Parameters["@note"].Value = note;
                cmdInsModif.Parameters["@chg"].Value = (byte)Entities.ChangeType.Note;
                cmdInsModif.Parameters["@entry_id"].Value = entryId;
                cmdInsModif.ExecuteNonQuery();
                // Update previous version counts
                cmdInsModifPreCounts1.Parameters["@top_id"].Value = cmdInsModif.LastInsertedId;
                cmdInsModifPreCounts1.Parameters["@entry_id"].Value = entryId;
                cmdInsModifPreCounts1.ExecuteNonQuery();
                cmdInsModifPreCounts2.Parameters["@top_id"].Value = cmdInsModif.LastInsertedId;
                cmdInsModifPreCounts2.Parameters["@entry_id"].Value = entryId;
                cmdInsModifPreCounts2.ExecuteNonQuery();
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

                // Serialize, store in DB, index
                int binId = indexEntry(entry);
                // Have index commit filed change
                index.ApplyChanges(indexCommands);

                // Populate entries table
                int entryId = storeEntry(entry.ChSimpl, head, trg, binId);
                // Record change
                cmdInsModif.Parameters["@parent_id"].Value = -1;
                cmdInsModif.Parameters["@hw_before"].Value = "";
                cmdInsModif.Parameters["@trg_before"].Value = "";
                cmdInsModif.Parameters["@timestamp"].Value = DateTime.UtcNow;
                cmdInsModif.Parameters["@user_id"].Value = userId;
                cmdInsModif.Parameters["@note"].Value = note;
                cmdInsModif.Parameters["@chg"].Value = (byte)Entities.ChangeType.New;
                cmdInsModif.Parameters["@entry_id"].Value = entryId;
                cmdInsModif.ExecuteNonQuery();
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
                    int userId = -1;
                    cmdSelUserByName.Parameters["@user_name"].Value = x;
                    using (var rdr = cmdSelUserByName.ExecuteReader())
                    {
                        while (rdr.Read()) userId = rdr.GetInt32("id");
                    }
                    if (userId == -1)
                    {
                        cmdInsImplicitUser.Parameters["@user_name"].Value = x;
                        cmdInsImplicitUser.Parameters["@registered"].Value = DateTime.UtcNow;
                        cmdInsImplicitUser.ExecuteNonQuery();
                        userId = (int)cmdInsImplicitUser.LastInsertedId;
                    }
                    userToId[x] = userId;
                }
                // Insert modif records for every bulk change
                foreach (var x in bulks)
                {
                    cmdInsBulkModif.Parameters["@timestamp"].Value = x.Value.Timestamp;
                    cmdInsBulkModif.Parameters["@user_id"].Value = userToId[x.Value.UserName];
                    cmdInsBulkModif.Parameters["@note"].Value = x.Value.Comment;
                    cmdInsBulkModif.Parameters["@bulk_ref"].Value = x.Key;
                    cmdInsBulkModif.ExecuteNonQuery();
                    bulkRefToModifId[x.Key] = (int)cmdInsBulkModif.LastInsertedId;
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
                // Infuse corpus frequency
                int iFreq = freq.GetFreq(entry.ChSimpl);
                ushort uFreq = iFreq > ushort.MaxValue ? ushort.MaxValue : (ushort)iFreq;
                entry.Freq = uFreq;
                string hw, trg;
                CedictWriter.Write(entry, out hw, out trg);
                // Check restrictions. Will skip failing entries.
                try { checkRestrictions(entry.ChSimpl, trg); }
                catch { return false; }
                // Serialize, store in DB, index
                int binId = indexEntry(entry);
                // Populate entries table
                storeEntry(entry.ChSimpl, hw, trg, binId, entryId);
                // Working backwards, create MODIF records for each version
                int topModifId = -1;
                for (int i = vers.Count - 1; i >= 0; --i)
                {
                    EntryVersion ver = vers[i];
                    hw = null; trg = null;
                    if (ver.Entry != null) CedictWriter.Write(ver.Entry, out hw, out trg);
                    // Find previous different HW and TRG, if any
                    string hwLast = null;
                    string trgLast = null;
                    if (ver.Entry != null)
                    {
                        for (int j = i - 1; j >= 0 && hwLast == null && trgLast == null; --j)
                        {
                            if (vers[j].Entry == null) continue;
                            CedictWriter.Write(vers[j].Entry, out hwLast, out trgLast);
                            if (hwLast == hw) hwLast = null;
                            if (trgLast == trg) trgLast = null;
                        }
                    }
                    // --
                    int parentId = ver.BulkRef == -1 ? -1 : bulkRefToModifId[ver.BulkRef];
                    int change;
                    if (i == 0) change = 0; // New entry
                    else if (hwLast != null || trgLast != null) change = 2; // Edit
                    else if (ver.Status != vers[i - 1].Status) change = 4; // Status changed
                    else change = 3; // Simply commented
                    // Store in DB
                    cmdInsModif.Parameters["@parent_id"].Value = parentId;
                    if (hwLast != null) cmdInsModif.Parameters["@hw_before"].Value = hwLast;
                    else cmdInsModif.Parameters["@hw_before"].Value = "";
                    if (trgLast != null) cmdInsModif.Parameters["@trg_before"].Value = trgLast;
                    else cmdInsModif.Parameters["@trg_before"].Value = "";
                    cmdInsModif.Parameters["@timestamp"].Value = ver.Timestamp;
                    cmdInsModif.Parameters["@user_id"].Value = userToId[ver.User];
                    cmdInsModif.Parameters["@note"].Value = ver.Comment;
                    cmdInsModif.Parameters["@chg"].Value = change;
                    cmdInsModif.Parameters["@entry_id"].Value = entryId;
                    cmdInsModif.ExecuteNonQuery();
                    // Remember ID of top (latest) modif
                    if (i == vers.Count - 1) topModifId = (int)cmdInsModif.LastInsertedId;
                }
                // Update previous version counts
                cmdInsModifPreCounts1.Parameters["@top_id"].Value = cmdInsModif.LastInsertedId;
                cmdInsModifPreCounts1.Parameters["@entry_id"].Value = entryId;
                cmdInsModifPreCounts1.ExecuteNonQuery();
                cmdInsModifPreCounts2.Parameters["@top_id"].Value = cmdInsModif.LastInsertedId;
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
                // Finish transaction
                tr.Commit(); tr.Dispose(); tr = null;
            }
        }

        #endregion

    }
}