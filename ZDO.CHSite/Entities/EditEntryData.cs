using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ZD.Common;

namespace ZDO.CHSite.Entities
{
    public class EditEntryData
    {
        public string EntryHtml;
        public string HeadTxt;
        public string TrgTxt;
        public bool CanApprove;
        public string HistoryHtml;
        public EntryStatus Status;
    }
}
