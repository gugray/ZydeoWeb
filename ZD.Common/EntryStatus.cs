using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZD.Common
{
    // !! Keep values in sync with those in DB's entries.status
    public enum EntryStatus
    {
        Flagged = 1,
        Neutral = 0,
        Approved = 2,
    }
}
