using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using ZD.Common;
using ZD.LangUtils;

namespace ZDO.CHSite.Logic
{
    public class LangRepo
    {
        private HeadwordInfo hwInfo;
        public HeadwordInfo HWInfo { get { return hwInfo; } }

        public LangRepo()
        {
            hwInfo = new HeadwordInfo("files/data/unihanzi.bin");
        }
    }
}
