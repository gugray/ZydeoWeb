using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZD.Common
{
    public class EntryVersion
    {
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
        /// <para>The entry at this version.</para>
        /// <para>For export, null when entry didn't change from previous version, except first and last version, which are always present.</para>
        /// </summary>
        public CedictEntry Entry = null;
    }
}
