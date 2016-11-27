using System;

namespace ZDO.CHSite.Entities
{
    public enum ChangeType
    {
        New = 0,
        Delete = 1,
        Edit = 2,
        Note = 3,
        StatusChange = 4,
        BulkImport = 99,
    }

    public class ChangeItem
    {
        /// <summary>
        /// Change's timestamp.
        /// </summary>
        public DateTime When;
        /// <summary>
        /// User that made the change.
        /// </summary>
        public string User;
        /// <summary>
        /// For bulk changes, a non-negative reference allows showing link to details page.
        /// </summary>
        public int BulkRef;
        /// <summary>
        /// Type of change.
        /// </summary>
        public ChangeType ChangeType;
        /// <summary>
        /// Note added by user.
        /// </summary>
        public string Note;
        /// <summary>
        /// ID of affected entry.
        /// </summary>
        public int EntryId;
        /// <summary>
        /// Entry headword, from CEDICT format.
        /// </summary>
        public string EntryHead;
        /// <summary>
        /// Entry body (senses), from CEDICT fornmat.
        /// </summary>
        public string EntryBody;
        /// <summary>
        /// Entry body before change, if target was edited in normal change. Otherwise null.
        /// </summary>
        public string BodyBefore;
        /// <summary>
        /// Bulk: new entry count. Normal: number of additional history items in entry's past.
        /// </summary>
        public int CountA;
        /// <summary>
        /// Bulk: number of changed entries,.
        /// </summary>
        public int CountB;
    }
}
