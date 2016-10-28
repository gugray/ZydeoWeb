using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

namespace ZD.Tool
{
    public class WrkToBytes : IWorker
    {
        public void Dispose()
        { }

        public void Finish()
        { }

        public void Init()
        { }

        public void Work()
        {
            using (FileStream sin = new FileStream("medians.bin", FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(sin))
            using (FileStream sout = new FileStream("chardata.js", FileMode.Create, FileAccess.ReadWrite))
            using (StreamWriter sw = new StreamWriter(sout))
            {
                sw.WriteLine("var charDataArr =");
                sw.WriteLine("[");
                int count = 0;
                while (sin.Position < sin.Length)
                {
                    if (count != 0) sw.Write(", ");
                    if (count % 4096 == 4095) sw.WriteLine();
                    byte b = br.ReadByte();
                    sw.Write(b.ToString());
                    ++count;
                }
                sw.WriteLine();
                sw.WriteLine("];");
            }
        }
    }
}
