using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace Tatti3.GameData
{
    class StringTable
    {
        StringTable()
        {
            keyToIndex = new Dictionary<string, uint>();
            byIndex = new List<string>();
        }

        public static StringTable FromJson(Stream input)
        {
            var self = new StringTable();
            var json = JsonDocument.Parse(input);
            self.byIndex.Add("(None)");
            foreach (var str in json.RootElement.EnumerateArray())
            {
                var key = str.GetProperty("Key").GetString();
                var value = str.GetProperty("Value").GetString();
                self.keyToIndex[key] = (uint)self.byIndex.Count;
                self.byIndex.Add(value);
            }
            return self;
        }

        public static StringTable FromXml(Stream input)
        {
            var convertedInput = PreprocessXml(input);
            var self = new StringTable();
            var xml = new XmlDocument();
            xml.LoadXml(convertedInput);
            self.byIndex.Add("(None)");
            foreach (var val in xml["strings"])
            {
                if (val is XmlElement xmlString)
                {
                    var key = xmlString["id"].InnerText;
                    var value = xmlString["value"].InnerText;
                    self.keyToIndex[key] = (uint)self.byIndex.Count;
                    self.byIndex.Add(value);
                }
            }
            return self;
        }

        // .NET doesn't seem to support raw control codes in xml, so convert them to &#N;
        static string PreprocessXml(Stream input)
        {
            string result;
            using (var reader = new StreamReader(input))
            {
                result = reader.ReadToEnd();
            }
            var builder = new StringBuilder(result.Length);
            var pos = 0;
            while (true)
            {
                var substringStart = pos;
                while (pos != result.Length)
                {
                    var ch = result[pos];
                    if (ch < 0x20 && ch != '\r' && ch != '\n')
                    {
                        break;
                    }
                    pos += 1;
                }
                builder.Append(result, substringStart, pos - substringStart);
                if (pos == result.Length)
                {
                    break;
                }
                builder.Append($"&#{(int)result[pos]};");
                pos += 1;
            }
            return builder.ToString();
        }

        public string? GetByIndex(uint index)
        {
            return byIndex.Count <= index ? null : byIndex[(int)index];
        }

        public string? GetByKey(string key)
        {
            if (keyToIndex.TryGetValue(key, out uint index))
            {
                return GetByIndex(index);
            }
            else
            {
                return null;
            }
        }

        public List<string> ListByIndex()
        {
            return new List<string>(this.byIndex);
        }

        Dictionary<string, uint> keyToIndex;
        List<string> byIndex;
    }
}
