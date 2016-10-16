using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZDO.CHSite.Entities
{
    public enum ChangeType
    {
        New = 0,
        Delete = 1,
        Edit = 2,
        Note = 3,
        Flag = 4,
        Approve = 5,
        BulkImport = 6,
    }

    public class ChangeItem
    {
        public DateTime When;
        public string User;
        public ChangeType ChangeType;
        public string Note;
        public string EntryHead;
        public string EntryBody;
    }
}
