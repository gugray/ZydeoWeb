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
            /// DB ID of user perpetrating this change.
            /// </summary>
            protected readonly int userId;

            /// <summary>
            /// DB connection I'll be using throughout build. Owned.
            /// </summary>
            protected MySqlConnection conn;

            /// <summary>
            /// Current DB transaction.
            /// </summary>
            protected MySqlTransaction tr = null;

            // Reused commands
            private MySqlCommand cmdInsBinaryEntry;
            private MySqlCommand cmdInsHanziInstance;
            private MySqlCommand cmdInsEntry;
            protected MySqlCommand cmdUpdLastModif;
            protected MySqlCommand cmdInsModifNew;
            // Reused commands owned for the index module
            protected Index.StorageCommands indexCommands = new Index.StorageCommands();
            // ---------------

            /// <summary>
            /// Ctor: init DB connection; shared builder resources.
            /// </summary>
            protected Builder(Index index, int userId)
            {
                if (!index.Lock.TryEnterWriteLock(15000))
                    throw new Exception("Write lock timeout.");
                try
                {
                    this.index = index;
                    this.userId = userId;
                    conn = DB.GetConn();
                    // Shared builder commands, owned here in base class
                    cmdInsBinaryEntry = DB.GetCmd(conn, "InsBinaryEntry");
                    cmdInsHanziInstance = DB.GetCmd(conn, "InsHanziInstance");
                    cmdInsEntry = DB.GetCmd(conn, "InsEntry");
                    cmdUpdLastModif = DB.GetCmd(conn, "UpdLastModif");
                    cmdInsModifNew = DB.GetCmd(conn, "InsModifNew");
                    // Commands owned for the index module
                    indexCommands.CmdDelEntryHanziInstances = DB.GetCmd(conn, "DelEntryHanziInstances");
                    indexCommands.CmdInsHanziInstance = DB.GetCmd(conn, "InsHanziInstance");
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

                    if (indexCommands.CmdInsHanziInstance != null) indexCommands.CmdInsHanziInstance.Dispose();
                    if (indexCommands.CmdDelEntryHanziInstances != null) indexCommands.CmdDelEntryHanziInstances.Dispose();

                    if (cmdInsModifNew != null) cmdInsModifNew.Dispose();
                    if (cmdUpdLastModif != null) cmdUpdLastModif.Dispose();
                    if (cmdInsEntry != null) cmdInsEntry.Dispose();
                    if (cmdInsHanziInstance != null) cmdInsHanziInstance.Dispose();
                    if (cmdInsBinaryEntry != null) cmdInsBinaryEntry.Dispose();
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
                if (simp.Length > 16) throw new Exception("Headword must not exceed 16 syllables.");
                if (trg.Length > 1024) throw new Exception("Translation, in CEDICT format, must not exceed 1024 characters.");
            }

            protected int storeEntry(string simp, string head, string trg, int binId)
            {
                cmdInsEntry.Parameters["@hw"].Value = head;
                cmdInsEntry.Parameters["@trg"].Value = trg;
                cmdInsEntry.Parameters["@simp_hash"].Value = CedictEntry.Hash(simp);
                cmdInsEntry.Parameters["@status"].Value = 0;
                cmdInsEntry.Parameters["@deleted"].Value = 0;
                cmdInsEntry.Parameters["@bin_id"].Value = binId;
                cmdInsEntry.ExecuteNonQuery();
                return (int)cmdInsEntry.LastInsertedId;
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
                return binId;
            }

            /// <summary>
            /// Add Hanzi to DB's index/instance tables.
            /// </summary>
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
            // Reused commands
            // ---------------

            public SimpleBuilder(Index index, int userId)
                : base(index, userId)
            {
            }

            protected override void DoDispose()
            {
                // This must come at the end. Will close connection, which we need for disposing of our own stuff.
                base.DoDispose();
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
                cmdInsModifNew.Parameters["@timestamp"].Value = DateTime.UtcNow;
                cmdInsModifNew.Parameters["@user_id"].Value = userId;
                cmdInsModifNew.Parameters["@note"].Value = note;
                cmdInsModifNew.Parameters["@entry_id"].Value = entryId;
                cmdInsModifNew.ExecuteNonQuery();
                int modifId = (int)cmdInsModifNew.LastInsertedId;
                // Also link from entry
                cmdUpdLastModif.Parameters["@entry_id"].Value = entryId;
                cmdUpdLastModif.Parameters["@last_modif_id"].Value = modifId;
                cmdUpdLastModif.ExecuteNonQuery();
                // Commit. Otherwise, dispose will roll all this back if it finds non-null transaction.
                tr.Commit(); tr.Dispose(); tr = null;
            }
        }

        #endregion

        #region Bulk builder

        public class BulkBuilder : Builder
        {
			// Reused commands
            private MySqlCommand cmdInsDummyForBulk;
            private MySqlCommand cmdInsModifForBulk;
            private MySqlCommand cmdInsBulkModif;
			// ---------------

            private readonly string note;
            private readonly bool foldHistory;

			/// <summary>
			/// See <see cref="LogFileName"/>.
			/// </summary>
            private string logFileName;
			/// <summary>
            /// See <see cref="DroppedFileName"/>.
            /// </summary>
            private string droppedFileName;
            /// <summary>
            /// Log stream.
            /// </summary>
            private FileStream fsLog;
			/// <summary>
			/// Log stream writer.
			/// </summary>
            private StreamWriter swLog;
            /// <summary>
            /// Stream for collecting dropped lines.
            /// </summary>
            private FileStream fsDropped;
			/// <summary>
			/// Dropped lines writer.
			/// </summary>
            private StreamWriter swDropped;
			/// <summary>
			/// See <see cref="LineNum"/>.
			/// </summary>
            private int lineNum = 0;
            /// <summary>
            /// ID of modification record for this entire operation.
            /// </summary>
            private readonly int modifId;

			/// <summary>
			/// Full path to file with import log.
			/// </summary>
			public string LogFileName { get { return logFileName; } }
			/// <summary>
			/// Full path to file with dropped entries (either failed, or duplicates).
			/// </summary>
            public string DroppedFileName { get { return droppedFileName; } }
			/// <summary>
			/// Number of lines processed.
			/// </summary>
            public int LineNum { get { return lineNum; } }

            /// <summary>
            /// Ctor: initialize bulk builder resources.
            /// </summary>
            public BulkBuilder(Index index, string workingFolder, int userId, string note, bool foldHistory)
                : base(index, userId)
            {
                this.note = note;
                this.foldHistory = foldHistory;

                tr = conn.BeginTransaction();

                cmdInsDummyForBulk = DB.GetCmd(conn, "InsDummyForBulk");
                cmdInsModifForBulk = DB.GetCmd(conn, "InsModifForBulk");
                cmdInsBulkModif = DB.GetCmd(conn, "InsBulkModif");

                DateTime dt = DateTime.UtcNow;
                string dtStr = "{0:D4}-{1:D2}-{2:D2}!{3:D2}-{4:D2}-{5:D2}.{6:D3}";
                dtStr = string.Format(dtStr, dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond);
                logFileName = Path.Combine(workingFolder, "Import-" + dtStr + "-log.txt");
                fsLog = new FileStream(logFileName, FileMode.Create, FileAccess.ReadWrite);
                swLog = new StreamWriter(fsLog);
                droppedFileName = Path.Combine(workingFolder, "Import-" + dtStr + "-dropped.txt");
                fsDropped = new FileStream(droppedFileName, FileMode.Create, FileAccess.ReadWrite);
                swDropped = new StreamWriter(fsDropped);

                // If we're folding history, insert dummy item; we'll refer to this from every entry.
                if (foldHistory)
                {
                    cmdInsDummyForBulk.ExecuteNonQuery();
                    int dummyId = (int)cmdInsDummyForBulk.LastInsertedId;
                    cmdInsModifForBulk.Parameters["@timestamp"].Value = dt;
                    cmdInsModifForBulk.Parameters["@user_id"].Value = userId;
                    cmdInsModifForBulk.Parameters["@note"].Value = note;
                    cmdInsModifForBulk.Parameters["@dummy_entry_id"].Value = dummyId;
                    cmdInsModifForBulk.ExecuteNonQuery();
                    modifId = (int)cmdInsModifForBulk.LastInsertedId;
                    cmdUpdLastModif.Parameters["@entry_id"].Value = dummyId;
                    cmdUpdLastModif.Parameters["@last_modif_id"].Value = modifId;
                    cmdUpdLastModif.ExecuteNonQuery();
                }
            }

            /// <summary>
            /// Dispose of bulk builder resources.
            /// </summary>
            protected override void DoDispose()
            {
                if (swDropped != null) swDropped.Dispose();
                if (swLog != null) swLog.Dispose();
                if (fsDropped != null) fsDropped.Dispose();
                if (fsLog != null) fsLog.Dispose();

                if (cmdInsBulkModif != null) cmdInsBulkModif.Dispose();
                if (cmdInsModifForBulk != null) cmdInsModifForBulk.Dispose();
                if (cmdInsDummyForBulk != null) cmdInsDummyForBulk.Dispose();

                // This must come at the end. Will close connection, which we need for disposing of our own stuff.
                base.DoDispose();
            }

            /// <summary>
            /// Process one line in the CEDICT format: parse, and store/index in dictionary.
            /// !! Does not check against dupes; cannot be used to update.
            /// </summary>
            public void AddEntry(string line)
            {
                ++lineNum;
                // Cycle through transactions
                if (lineNum % 3000 == 0)
                {
                    // Make index apply changes
                    index.ApplyChanges(indexCommands);
                    // Commit to DB; start new transaction
                    tr.Commit(); tr.Dispose(); tr = null;
                    tr = conn.BeginTransaction();
                }
                // Parse line from CEDICT format
                // TO-DO: Make parser a member to avoid constantly recreating (costly)
                // TO-DO: Get rid of swDropped
                CedictParser parser = new CedictParser();
                CedictEntry entry = parser.ParseEntry(line, lineNum, swLog);
                if (entry == null) return;
                string head, trg;
                CedictWriter.Write(entry, out head, out trg);
                // Check restrictions - can end up dropped entry
                try { checkRestrictions(entry.ChSimpl, trg); }
                catch { return; }

                // Serialize, store in DB, index
                int binId = indexEntry(entry);
                // Populate entries table
                int entryId = storeEntry(entry.ChSimpl, head, trg, binId);

                // Folding history: mark new entry as affected by this bulk operation
                if (foldHistory)
                {
                    cmdInsBulkModif.Parameters["@modif_id"].Value = modifId;
                    cmdInsBulkModif.Parameters["@entry_id"].Value = entryId;
                    cmdInsBulkModif.ExecuteNonQuery();
                }
                // Verbose (per-entry) history
                else
                {
                    // Record change
                    cmdInsModifNew.Parameters["@timestamp"].Value = DateTime.UtcNow;
                    cmdInsModifNew.Parameters["@user_id"].Value = userId;
                    cmdInsModifNew.Parameters["@note"].Value = note;
                    cmdInsModifNew.Parameters["@entry_id"].Value = entryId;
                    cmdInsModifNew.ExecuteNonQuery();
                    int modifId = (int)cmdInsModifNew.LastInsertedId;
                    // Also link from entry
                    cmdUpdLastModif.Parameters["@entry_id"].Value = entryId;
                    cmdUpdLastModif.Parameters["@last_modif_id"].Value = modifId;
                    cmdUpdLastModif.ExecuteNonQuery();
                }
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