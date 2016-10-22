using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZD.Common
{
    /// <summary>
    /// One sense in an entry.
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{GetPlainText()}")]
    public class CedictSense : IBinSerializable
    {
        /// <summary>
        /// Domain: text in parentheses at start.
        /// </summary>
        public readonly string Domain;
        /// <summary>
        /// Target-language equivalents ("translations").
        /// </summary>
        public readonly string Equiv;
        /// <summary>
        /// Note: text in parentheses at end.
        /// </summary>
        public readonly string Note;

        /// <summary>
        /// Ctor: init immutable instance.
        /// </summary>
        public CedictSense(string domain, string equiv, string note)
        {
            Domain = domain;
            Equiv = equiv;
            Note = note;

        }

        /// <summary>
        /// Ctor: read from binary stream.
        /// </summary>
        public CedictSense(BinReader br)
        {
            Domain = br.ReadString();
            Equiv = br.ReadString();
            Note = br.ReadString();
        }

        /// <summary>
        /// Serialize into binary stream.
        /// </summary>
        public void Serialize(BinWriter bw)
        {
            bw.WriteString(Domain);
            bw.WriteString(Equiv);
            bw.WriteString(Note);
        }

        /// <summary>
        /// Gets sense in plain text.
        /// </summary>
        public string GetPlainText()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Domain);
            sb.Append(Equiv);
            sb.Append(Note);
            return sb.ToString();
        }
    }
}
