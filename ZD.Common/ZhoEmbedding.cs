using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZD.Common
{
    /// <summary>
    /// Part of a sense.
    /// </summary>
    public enum SensePart
    {
        Domain = 0,
        Equiv = 1,
        Note = 2,
    }

    /// <summary>
    /// Type of Chinese embedding.
    /// </summary>
    public enum ZhoEmbedType
    {
        /// <summary>
        /// Trad|Simp[Pinyin]
        /// </summary>
        TSP = 0,
        /// <summary>
        /// Hanzi[Pinyin]
        /// </summary>
        HP = 1,
        /// <summary>
        /// Trad|Simp
        /// </summary>
        TS = 2,
        /// <summary>
        /// Hanzi
        /// </summary>
        H = 3,
        /// <summary>
        /// [Pinyin]
        /// </summary>
        P = 4,
    }

    /// <summary>
    /// Describes one Chinese embedding within a sense's Latin text.
    /// </summary>
    public struct ZhoEmbedding
    {
        /// <summary>
        /// Value field 1; not for direct consumption.
        /// </summary>
        public ulong Val;
        /// <summary>
        /// Index of sense within the entry.
        /// </summary>
        public byte SenseIx { get { return (byte)(Val & 0xff); } }
        /// <summary>
        /// Which part of the sense the embedding is in (domain, equiv, note).
        /// </summary>
        public SensePart SensePart { get { return (SensePart)((Val >> 8) & 0xff); } }
        /// <summary>
        /// Type of the embedding.
        /// </summary>
        public ZhoEmbedType EmbedType { get { return (ZhoEmbedType)((Val >> 16) & 0xff); } }
        /// <summary>
        /// Start of embedding within sense part.
        /// </summary>
        public ushort Start { get { return (ushort)((Val >> 24) & 0xffff); } }
        /// <summary>
        /// Length of embedding within sense part.
        /// </summary>
        public ushort Length { get { return (ushort)((Val >> 40) & 0xffff); } }

        public ZhoEmbedding(byte senseIx, SensePart sensePart, ZhoEmbedType embedType, ushort start, ushort length)
        {
            ulong val = senseIx;
            val <<= 8;
            val += (byte)sensePart;
            val <<= 8;
            val += (byte)embedType;
            val <<= 16;
            val += start;
            val <<= 16;
            val += length;
            Val = val;
        }

        public ZhoEmbedding(BinReader br)
        {
            Val = br.ReadULong();
        }

        public void Serialize(BinWriter bw)
        {
            bw.WriteULong(Val);
        }
    }
}
