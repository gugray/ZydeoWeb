using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZD.Common
{
    public enum SensePart
    {
        Domain = 0,
        Equiv = 1,
        Note = 2,
    }

    /// <summary>
    /// Describes one Chinese embedding within a sense's Latin text.
    /// </summary>
    public struct ZhoEmbedding
    {
        /// <summary>
        /// Index of sense within the entry.
        /// </summary>
        public short SenseIx;
        /// <summary>
        /// Which part of the sense the embedding is in (domain, equiv, note).
        /// </summary>
        public SensePart SensePart;
        /// <summary>
        /// Start of embedding within sense part.
        /// </summary>
        public short Start;
        /// <summary>
        /// Length of embedding withi sense part.
        /// </summary>
        public short Length;
        /// <summary>
        /// Start of simplified Hanzi within sense part, or -1.
        /// </summary>
        public short SimpStart;
        /// <summary>
        /// Length of simplified Hanzi within sense part, or -1.
        /// </summary>
        public short SimpLength;
        /// <summary>
        /// Start of traditional Hanzi within sense part, or -1.
        /// </summary>
        public short TradStart;
        /// <summary>
        /// Length of traditional Hanzi within sense part, or 0.
        /// </summary>
        public short TradLength;
        /// <summary>
        /// Start of Pinyin within sense part, or -1. Excludes opening square bracket.
        /// </summary>
        public short PinyinStart;
        /// <summary>
        /// Length of Pinyin within sense part, or 0. Excludes closing square bracket.
        /// </summary>
        public short PinyinLength;

        public ZhoEmbedding(BinReader br)
        {
            SenseIx = br.ReadShort();
            SensePart = (SensePart)br.ReadByte();
            Start = br.ReadShort();
            Length = br.ReadShort();
            SimpStart = br.ReadShort();
            SimpLength = br.ReadShort();
            TradStart = br.ReadShort();
            TradLength = br.ReadShort();
            PinyinStart = br.ReadShort();
            PinyinLength = br.ReadShort();
        }

        public void Serialize(BinWriter bw)
        {
            bw.WriteShort(SenseIx);
            bw.WriteByte((byte)SensePart);
            bw.WriteShort(Start);
            bw.WriteShort(Length);
            bw.WriteShort(SimpStart);
            bw.WriteShort(SimpLength);
            bw.WriteShort(TradStart);
            bw.WriteShort(TradLength);
            bw.WriteShort(PinyinStart);
            bw.WriteShort(PinyinLength);
        }
    }
}
