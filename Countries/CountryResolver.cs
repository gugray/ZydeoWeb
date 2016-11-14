using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Net;
using System.Net.Sockets;

namespace Countries
{
    public class CountryResolver
    {
        /// <summary>
        /// Country codes; position in array is country ID.
        /// </summary>
        private readonly string[] countries;

        /// <summary>
        /// IPv4 ranges, sorted by start of range.
        /// </summary>
        private readonly IPv4Range[] ip4Ranges;

        /// <summary>
        /// Initializes resolver by parsing IPv4 range file.
        /// </summary>
        public CountryResolver(string fnIp4)
        {
            countries = loadCountries();
            using (FileStream fsIp4 = new FileStream(fnIp4, FileMode.Open, FileAccess.Read))
            using (StreamReader srIp4 = new StreamReader(fsIp4))
            {
                ip4Ranges = loadIp4(srIp4);
            }
        }

        /// <summary>
        /// Returns code for unknown coutry (ZZZ).
        /// </summary>
        public string NoCountry { get { return countries[countries.Length - 1]; } }

        /// <summary>
        /// Placeholder "country" for IPv6 requests.
        /// </summary>
        public string IP6 { get { return "IP6"; } }

        /// <summary>
        /// Placeholder "country" for resolution errors.
        /// </summary>
        public string Error { get { return "ZYX"; } }

        /// <summary>
        /// Gets country code from an IP address.
        /// </summary>
        public string GetContryCode(IPAddress addr)
        {
            // IPv4
            if (addr.AddressFamily == AddressFamily.InterNetwork) return getCountryCodeIPv4(addr);
            // IPv6
            else if (addr.AddressFamily == AddressFamily.InterNetworkV6) return IP6;
            // Bollocks
            return Error;
        }

        /// <summary>
        /// Gets country code from an IPv4 address.
        /// </summary>
        private string getCountryCodeIPv4(IPAddress addr)
        {
            // Get IP address as unsigned 32-bit
            byte[] bytes = addr.GetAddressBytes();
            UInt32 val = 0;
            for (int i = 0; i != bytes.Length; ++i)
            {
                val <<= 8;
                val += bytes[i];
            }
            // Find largest RangeFirst that's not greater than value
            // Binary search on ranges sorted by their first values
            int bottom = 0;
            int top = ip4Ranges.Length - 1;
            int middle = top >> 1;
            while (top >= bottom)
            {
                if (ip4Ranges[middle].RangeFirst == val) break;
                if (ip4Ranges[middle].RangeFirst > val) top = middle - 1;
                else bottom = middle + 1;
                middle = (bottom + top) >> 1;
            }
            // We're looking for equal or nearest smaller
            while (middle > 0 && ip4Ranges[middle].RangeFirst > val) --middle;
            IPv4Range range = ip4Ranges[middle];
            // We just have a larger one: no country
            if (range.RangeFirst > val) return NoCountry;
            // We're actually within range: return that country
            if (range.RangeFirst <= val && range.RangeLast >= val) return countries[range.CountryId];
            // No country
            return NoCountry;
        }

        /// <summary>
        /// Reads IPv4 range file; returns sorted array, with contiguous same-country ranges merged.
        /// </summary>
        private IPv4Range[] loadIp4(StreamReader sr)
        {
            // Reserve
            List<IPv4Range> res = new List<IPv4Range>(180000);
            // Parse file
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                if (line == string.Empty || line.StartsWith("#")) continue;
                string[] parts = getParts(line);
                UInt32 first = UInt32.Parse(parts[0]);
                UInt32 last = UInt32.Parse(parts[1]);
                string country = parts[5];
                byte countryId = getCountryId(country);
                res.Add(new IPv4Range { RangeFirst = first, RangeLast = last, CountryId = countryId });
            }
            // Sort by range starts
            res.Sort((a, b) => a.RangeFirst.CompareTo(b.RangeFirst));
            // Eliminate duplicates
            List<IPv4Range> cpy = new List<IPv4Range>(res.Count);
            cpy.Add(res[0]);
            for (int i = 1; i < res.Count; ++i)
            {
                IPv4Range curr = res[i];
                IPv4Range prev = cpy[cpy.Count - 1];
                // Current range is contiguous to previous one; same country too
                if (curr.CountryId == prev.CountryId && curr.RangeFirst == prev.RangeLast + 1)
                {
                    prev.RangeLast = curr.RangeLast;
                    cpy[cpy.Count - 1] = prev;
                }
                // Nop, add new item
                else cpy.Add(curr);
            }
            // Return redcued array (with contiguous country ranges merged)
            return cpy.ToArray();
        }

        /// <summary>
        /// Gets ID of country code;
        /// </summary>
        private byte getCountryId(string country)
        {
            for (byte b = 0; b <= 255; ++b)
            {
                if (countries[b] == country) return b;
            }
            return (byte)(countries.Length - 1);
        }

        /// <summary>
        /// Gets parts of a line from IP ranges file.
        /// </summary>
        private static string[] getParts(string line)
        {
            line = line.Replace("\",\"", "|");
            line = line.Replace("\"", "");
            return line.Split(new char[] { '|' });
        }

        /// <summary>
        /// Loads countries from embedded text file.
        /// </summary>
        private static string[] loadCountries()
        {
            List<string> res = new List<string>(256);
            Assembly a = typeof(CountryResolver).GetTypeInfo().Assembly;
            using (Stream s = a.GetManifestResourceStream("Countries.countries.txt"))
            using (StreamReader sr = new StreamReader(s))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line == string.Empty) continue;
                    res.Add(line);
                }
            }
            return res.ToArray();
        }
    }
}
