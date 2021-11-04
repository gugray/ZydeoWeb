using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZDO.CHSite.Entities
{
    public class NewEntryVerifyHeadResult
    {
        public bool Duplicate { get; set; }
        public string RefEntries { get; set; } = null;
        public string ExistingEntry { get; set; } = null;
        public string ExistingEntryId { get; set; } = null;
    }
}
