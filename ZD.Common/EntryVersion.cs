using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZD.Common
{
    public class EntryVersion
    {
        /// <summary>
        /// Entry's version number. Oldest is 1.
        /// </summary>
        public int Ver;
        
        /// <summary>
        /// Timestamp of this change.
        /// </summary>
        public DateTime Timestamp;

        /// <summary>
        /// User that created this change.
        /// </summary>
        public string User;

        /// <summary>
        /// Status of entry.
        /// </summary>
        public EntryStatus Status;

        /// <summary>
        /// Bulk change reference. -1 for non-bulk changes.
        /// </summary>
        public int BulkRef = -1;

        /// <summary>
        /// Comment of change that led to this version.
        /// </summary>
        public string Comment;

        /// <summary>
        /// The entry, or null if it's identical to *next* (later) version.
        /// </summary>
        public CedictEntry Entry = null;
    }
}
