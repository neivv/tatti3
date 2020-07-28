using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

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
            throw new NotImplementedException();
        }

        public string? GetByIndex(uint index)
        {
            return byIndex.Count <= index ? null : byIndex[(int)index];
        }

        public List<string> ListByIndex()
        {
            return new List<string>(this.byIndex);
        }

        Dictionary<string, uint> keyToIndex;
        List<string> byIndex;
    }
}
