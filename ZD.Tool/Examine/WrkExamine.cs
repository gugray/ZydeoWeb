using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

using ZD.Common;
using ZD.LangUtils;

namespace ZD.Tool.Examine
{
    public class WrkExamine : IWorker
    {
        private readonly OptExamine opt;
        public WrkExamine(OptExamine opt)
        {
            this.opt = opt;
        }

        private FileStream fsDict = null;
        private StreamReader srDict = null;
        private FileStream fsDiag = null;
        private StreamWriter swDiag = null;
        private FileStream fsTrip = null;
        private StreamWriter swTrip = null;
        private int lineNum = 0;
        private CedictParser parser = new CedictParser();

        public void Init()
        {
            fsDict = new FileStream(opt.DictFileName, FileMode.Open, FileAccess.Read);
            srDict = new StreamReader(fsDict);
            fsDiag = new FileStream(opt.DiagFileName, FileMode.Create, FileAccess.ReadWrite);
            swDiag = new StreamWriter(fsDiag);
            fsTrip = new FileStream(opt.RoundtripFileName, FileMode.Create, FileAccess.ReadWrite);
            swTrip = new StreamWriter(fsTrip);
        }

        public void Dispose()
        {
            if (swTrip != null) swTrip.Dispose();
            if (fsTrip != null) fsTrip.Dispose();
            if (swDiag != null) swDiag.Dispose();
            if (fsDiag != null) fsDiag.Dispose();
            if (srDict != null) srDict.Dispose();
            if (fsDict != null) fsDict.Dispose();
        }

        public void Work()
        {
            string line;
            while ((line = srDict.ReadLine()) != null)
            {
                ++lineNum;
                if (line.StartsWith("#")) continue;
                //// DBG
                //if (line.StartsWith("阿巴多 阿巴多 [a1 ba1 duo1] / Abadol"))
                //{
                //    int iii = 0;
                //}
                CedictEntry entry = parser.ParseEntry(line, lineNum, swDiag);
                if (entry != null)
                {
                    string trippedLine = CedictWriter.Write(entry);
                    if (trippedLine != line)
                    {
                        swTrip.WriteLine(line);
                        swTrip.WriteLine(trippedLine);
                    }
                }
            }
        }

        public void Finish()
        {
        }

    }
}
