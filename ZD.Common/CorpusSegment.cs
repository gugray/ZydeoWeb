using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZD.Common
{
    public class CorpusSegment : IBinSerializable
    {
        public struct IndexPair
        {
            public short A;
            public short B;
        }

        public struct AlignPair
        {
            public short Ix1;
            public short Ix2;
            public float Score;
        }

        public readonly string ZhSurf;
        public readonly string TrgSurf;
        public readonly IndexPair[] ZhTokMap;
        public readonly IndexPair[] TrgTokMap;
        public readonly AlignPair[] ZhToTrgAlign;
        public readonly AlignPair[] TrgToZhAlign;

        public CorpusSegment(string zhSurf, string trgSurf, 
            List<short[]> zhTokMap, List<short[]> trgTokMap,
            List<AlignPair> zhToTrg, List<AlignPair> trgToZh)
        {
            ZhSurf = zhSurf;
            TrgSurf = trgSurf;
            ZhTokMap = new IndexPair[zhTokMap.Count];
            for (int i = 0; i != zhTokMap.Count; ++i)
                ZhTokMap[i] = new IndexPair { A = zhTokMap[i][0], B = zhTokMap[i][1] };
            TrgTokMap = new IndexPair[trgTokMap.Count];
            for (int i = 0; i != trgTokMap.Count; ++i)
                TrgTokMap[i] = new IndexPair { A = trgTokMap[i][0], B = trgTokMap[i][1] };
            ZhToTrgAlign = new AlignPair[zhToTrg.Count];
            for (int i = 0; i != zhToTrg.Count; ++i) ZhToTrgAlign[i] = zhToTrg[i];
            TrgToZhAlign = new AlignPair[trgToZh.Count];
            for (int i = 0; i != trgToZh.Count; ++i) TrgToZhAlign[i] = trgToZh[i];
        }

        public CorpusSegment(BinReader br)
        {
            ZhSurf = br.ReadString();
            TrgSurf = br.ReadString();
            short count;
            count = br.ReadShort();
            ZhTokMap = new IndexPair[count];
            for (short i = 0; i != count; ++i)
            {
                short a = br.ReadShort();
                short b = br.ReadShort();
                ZhTokMap[i] = new IndexPair { A = a, B = b };
            }
            count = br.ReadShort();
            TrgTokMap = new IndexPair[count];
            for (short i = 0; i != count; ++i)
            {
                short a = br.ReadShort();
                short b = br.ReadShort();
                TrgTokMap[i] = new IndexPair { A = a, B = b };
            }
            count = br.ReadShort();
            ZhToTrgAlign = new AlignPair[count];
            for (short i = 0; i != count; ++i)
            {
                short ix1 = br.ReadShort();
                short ix2 = br.ReadShort();
                float score = (float)br.ReadDouble();
                ZhToTrgAlign[i] = new AlignPair { Ix1 = ix1, Ix2 = ix2, Score = score };
            }
            count = br.ReadShort();
            TrgToZhAlign = new AlignPair[count];
            for (short i = 0; i != count; ++i)
            {
                short ix1 = br.ReadShort();
                short ix2 = br.ReadShort();
                float score = (float)br.ReadDouble();
                TrgToZhAlign[i] = new AlignPair { Ix1 = ix1, Ix2 = ix2, Score = score };
            }
        }

        public void Serialize(BinWriter bw)
        {
            bw.WriteString(ZhSurf);
            bw.WriteString(TrgSurf);
            bw.WriteShort((short)ZhTokMap.Length);
            foreach (var ip in ZhTokMap)
            {
                bw.WriteShort(ip.A);
                bw.WriteShort(ip.B);
            }
            bw.WriteShort((short)TrgTokMap.Length);
            foreach (var ip in TrgTokMap)
            {
                bw.WriteShort(ip.A);
                bw.WriteShort(ip.B);
            }
            bw.WriteShort((short)ZhToTrgAlign.Length);
            foreach (var alm in ZhToTrgAlign)
            {
                bw.WriteShort(alm.Ix1);
                bw.WriteShort(alm.Ix2);
                bw.WriteDouble(alm.Score);
            }
            bw.WriteShort((short)TrgToZhAlign.Length);
            foreach (var alm in TrgToZhAlign)
            {
                bw.WriteShort(alm.Ix1);
                bw.WriteShort(alm.Ix2);
                bw.WriteDouble(alm.Score);
            }
        }
    }
}
