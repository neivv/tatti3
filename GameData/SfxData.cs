using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Tatti3.GameData
{
    class SfxData
    {
        public static SfxData FromJson(Stream input)
        {
            var self = new SfxData();
            var json = JsonDocument.Parse(input);
            // Sfx.json is "sfx" { set { subset { "entries" [ { sfx entry } ] } } }
            // set is "original" or "scr", subset is more grouping
            var sfx = json.RootElement.GetProperty("sfx");
            var subsets = sfx.EnumerateObject()
                .Where(x => x.Value.ValueKind == JsonValueKind.Object)
                .SelectMany(x => x.Value.EnumerateObject());
            foreach (var subset in subsets)
            {
                if (subset.Value.TryGetProperty("entries", out var entries))
                {
                    foreach (var entry in entries.EnumerateArray())
                    {
                        var id = entry.GetProperty("ID").GetString();
                        if (id != null)
                        {
                            self.names.Add(id);
                        }
                        else
                        {
                            throw new Exception("Invalid sfx.json format");
                        }
                    }
                }
            }
            return self;
        }

        public List<string> Names
        {
            get => names;
        }

        List<string> names = new();
    }
}
