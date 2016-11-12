using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Reflection;
using System.IO;
using Newtonsoft.Json;

namespace ZDO.CHSite
{
    internal class TextProvider
    {
        private static TextProvider instance = null;

        public static TextProvider Instance
        {
            get { return instance; }
        }

        /// <summary>
        /// The site's mutation.
        /// </summary>
        private readonly Mutation mut;

        /// <summary>
        /// Gets the site's mutation (CHD or HDD).
        /// </summary>
        public Mutation Mut { get { return mut; } }

        /// <summary>
        /// Initializes the global singleton.
        /// </summary>
        public static void Init(Mutation mut)
        {
            instance = new TextProvider(mut);
        }

        private readonly Dictionary<string, Dictionary<string, string>> dict = new Dictionary<string, Dictionary<string, string>>();

        private void initForLang(string langCode)
        {
            // Key-value pairs parsed now.
            Dictionary<string, string> newStrings = new Dictionary<string, string>();

            // Load language file, parse
            string fileName = "files/strings/strings." + langCode + ".json";
            using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            using (StreamReader sr = new StreamReader(fs))
            {
                string fileStr = sr.ReadToEnd();
                var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(fileStr);
                foreach (var x in parsed)
                {
                    foreach (var y in (x.Value as Newtonsoft.Json.Linq.JObject))
                    {
                        string key = x.Key + "." + y.Key;
                        string value = y.Value.ToString();
                        newStrings[key] = value;
                    }
                }
            }

            // Store for language
            dict[langCode] = newStrings;
        }

        private TextProvider(Mutation mut)
        {
            this.mut = mut;
            initForLang("en");
            initForLang("de");
            initForLang("hu");
        }

        public string GetString(string langCode, string id)
        {
            Dictionary<string, string> defDict = dict["en"];
            Dictionary<string, string> myDict = defDict;
            if (dict.ContainsKey(langCode)) myDict = dict[langCode];
            if (myDict.ContainsKey(id)) return myDict[id];
            else if (defDict.ContainsKey(id)) return defDict[id];
            else return id;
        }
    }
}