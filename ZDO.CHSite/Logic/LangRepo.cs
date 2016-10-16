using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using ZD.Common;
using ZD.CedictEngine;

namespace ZDO.CHSite.Logic
{
    public class LangRepo
    {
        private IHeadwordInfo hwInfo;
        public IHeadwordInfo HWInfo { get { return hwInfo; } }

        public LangRepo()
        {
            hwInfo = new HeadwordInfo("files/data/unihanzi.bin");
        }
    }
}
