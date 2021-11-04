using System;

using ZD.Common;

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
        public DateTime When { get; set; }
        /// <summary>
        /// User that made the change.
        /// </summary>
        public string User { get; set; }
        /// <summary>
        /// For bulk changes, a non-negative reference allows showing link to details page.
        /// </summary>
        public int BulkRef { get; set; }
        /// <summary>
        /// Type of change.
        /// </summary>
        public ChangeType ChangeType { get; set; }
        /// <summary>
        /// Note added by user.
        /// </summary>
        public string Note { get; set; }
        /// <summary>
        /// ID of affected entry.
        /// </summary>
        public int EntryId { get; set; }
        /// <summary>
        /// Entry headword, from CEDICT format.
        /// </summary>
        public string EntryHead { get; set; }
        /// <summary>
        /// Entry body (senses), from CEDICT fornmat.
        /// </summary>
        public string EntryBody { get; set; }
        /// <summary>
        /// Current entry status.
        /// </summary>
        public EntryStatus EntryStatus { get; set; }
        /// <summary>
        /// Entry head before change, if headword was edited in normal change. Otherwise null.
        /// </summary>
        public string HeadBefore { get; set; }
        /// <summary>
        /// Entry body before change, if target was edited in normal change. Otherwise null.
        /// </summary>
        public string BodyBefore { get; set; }
        /// <summary>
        /// Status before change. 99 if n/a, or a value of <see cref="ZD.Common.EntryStatus"/>. 
        /// </summary>
        public byte StatusBefore { get; set; }
        /// <summary>
        /// Bulk: new entry count. Normal: number of additional history items in entry's past.
        /// </summary>
        public int CountA { get; set; }
        /// <summary>
        /// Bulk: number of changed entries,.
        /// </summary>
        public int CountB { get; set; }
    }
}
