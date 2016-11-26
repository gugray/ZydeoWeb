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
        public DateTime When;
        public string User;
        public int BulkRef;
        public ChangeType ChangeType;
        public string Note;
        public int EntryId;
        public string EntryHead;
        public string EntryBody;
    }
}
