using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ZD.Common;

namespace ZDO.CHSite.Entities
{
    public class EditEntryData
    {
        public string EntryHtml { get; set; }
        public string HeadSimp { get; set; }
        public string HeadTrad { get; set; }
        public string HeadPinyin { get; set; }
        public string TrgTxt { get; set; }
        public bool CanApprove { get; set; }
        public string HistoryHtml { get; set; }
        public string Status { get; set; }
    }
}
