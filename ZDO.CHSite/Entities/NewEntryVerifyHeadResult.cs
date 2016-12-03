using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZDO.CHSite.Entities
{
    public class NewEntryVerifyHeadResult
    {
        public bool Duplicate;
        public string RefEntries = null;
        public string ExistingEntry = null;
        public string ExistingEntryId = null;
    }
}
