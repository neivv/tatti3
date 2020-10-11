using System;
using System.Buffers.Binary;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

using Tatti3.GameData.BinaryWriterExt;

namespace Tatti3.GameData
{
    class GameData
    {
        GameData(string root)
        {
            var firegraft = new FiregraftData(root);
            Units = LoadDatTable(Path.Join(root, "arr/units.dat"), LegacyDatDecl.Units, firegraft);
            // Wireframe mode, wireframe ID
            if (!Units.HasField(0x41))
            {
                var data = new List<byte>(Enumerable.Repeat((byte)0, (int)Units.Entries));
                var alternateWireframes = (new int[] {
                        0x3b, 0x3e, 0x3f, 0x44, 0x4c, 0x67, 0xad, 0xbb,
                        0xbc, 0xc2, 0xc5, 0xc9, 0xd8, 0xdc, 0xe2, 0xe3,
                    })
                    .Concat(Enumerable.Range(0x23, 0x3a - 0x23))
                    .Concat(Enumerable.Range(0x59, 0x62 - 0x59))
                    .Concat(Enumerable.Range(0x82, 0x99 - 0x82))
                    .Concat(Enumerable.Range(0xb0, 0xb3 - 0xb0));
                foreach (int i in alternateWireframes)
                {
                    data[i] = 1;
                }
                Units.AddField(0x41, DatFieldFormat.Uint8, data);
                var data2 = new List<byte>(
                    Enumerable.Range(0, (int)Units.Entries)
                        .SelectMany(x => {
                            byte[] vars = { 0, 0 };
                            BinaryPrimitives.WriteUInt16LittleEndian(vars, (UInt16)x);
                            return vars;
                        })
                );
                Units.AddField(0x42, DatFieldFormat.Uint16, data2);
            }
            // Icon ID
            if (!Units.HasField(0x43))
            {
                var data2 = new List<byte>(
                    Enumerable.Range(0, (int)Units.Entries)
                        .SelectMany(x => {
                            byte[] vars = { 0, 0 };
                            BinaryPrimitives.WriteUInt16LittleEndian(vars, (UInt16)x);
                            return vars;
                        })
                );
                Units.AddField(0x43, DatFieldFormat.Uint16, data2);
            }
            Weapons = LoadDatTable(Path.Join(root, "arr/weapons.dat"), LegacyDatDecl.Weapons, firegraft);
            Upgrades =
                LoadDatTable(Path.Join(root, "arr/upgrades.dat"), LegacyDatDecl.Upgrades, firegraft);
            TechData = LoadDatTable(Path.Join(root, "arr/techdata.dat"), LegacyDatDecl.TechData, firegraft);
            Flingy = LoadDatTable(Path.Join(root, "arr/flingy.dat"), LegacyDatDecl.Flingy, firegraft);
            Sprites = LoadDatTable(Path.Join(root, "arr/sprites.dat"), LegacyDatDecl.Sprites, firegraft);
            Images = LoadDatTable(Path.Join(root, "arr/images.dat"), LegacyDatDecl.Images, firegraft);
            Orders = LoadDatTable(Path.Join(root, "arr/orders.dat"), LegacyDatDecl.Orders, firegraft);
            Buttons = LoadButtons(Path.Join(root, "arr/buttons.dat"), LegacyDatDecl.Buttons, Units, firegraft);
            StatTxt = LoadStringTable(Path.Join(root, "rez/stat_txt"), Properties.Resources.rez_stat_txt_json);
            CmdIcons = LoadDdsGrp(
                Path.Join(root, "HD2/unit/cmdicons/cmdicons.dds.grp"),
                Properties.Resources.cmdicons_dds_grp
            );
            // Widen some arrays to 32bit
            WidenDatField(Units, 0x00);
            WidenDatField(Units, 0x11);
            WidenDatField(Units, 0x13);
            WidenDatField(Units, 0x19);
            WidenDatField(Weapons, 0x06);
            WidenDatField(Orders, 0x0d);
            WidenDatField(Orders, 0x0e);
        }

        public GameData(GameData other)
        {
            Units = new DatTable(other.Units);
            Weapons = new DatTable(other.Weapons);
            Upgrades = new DatTable(other.Upgrades);
            TechData = new DatTable(other.TechData);
            Flingy = new DatTable(other.Flingy);
            Sprites = new DatTable(other.Sprites);
            Images = new DatTable(other.Images);
            Orders = new DatTable(other.Orders);
            Buttons = new DatTable(other.Buttons);
            // Ok as long as this program doesn't support TBL editing
            StatTxt = other.StatTxt;
            CmdIcons = other.CmdIcons;
        }

        public static GameData Open(string root)
        {
            return new GameData(root);
        }

        public void Save(string root)
        {
            using (var tempFiles = new WriteTempFiles())
            {
                SaveDatTable(tempFiles, Units, Path.Join(root, "arr/units.dat"));
                SaveDatTable(tempFiles, Weapons, Path.Join(root, "arr/weapons.dat"));
                SaveDatTable(tempFiles, Flingy, Path.Join(root, "arr/flingy.dat"));
                SaveDatTable(tempFiles, Upgrades, Path.Join(root, "arr/upgrades.dat"));
                SaveDatTable(tempFiles, TechData, Path.Join(root, "arr/techdata.dat"));
                SaveDatTable(tempFiles, Orders, Path.Join(root, "arr/orders.dat"));
                SaveDatTable(tempFiles, Buttons, Path.Join(root, "arr/buttons.dat"));
                tempFiles.Commit();
            }
        }

        void SaveDatTable(WriteTempFiles tempFiles, DatTable dat, string path)
        {
            try
            {
                using (var file = tempFiles.NewFile(path))
                {
                    dat.Write(file);
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to save {path}", e);
            }
        }

        static DatTable LoadDatTable(string path, LegacyDatDecl legacyDecl, FiregraftData firegraft)
        {
            DatTable table;
            try
            {
                using (var file = File.OpenRead(path))
                {
                    if (file.Length == legacyDecl.FileSize)
                    {
                        table = DatTable.LoadLegacy(file, legacyDecl);
                    }
                    else
                    {
                        table = DatTable.LoadNew(file, legacyDecl);
                    }
                }
            }
            catch (FileNotFoundException)
            {
                var stream = new MemoryStream(legacyDecl.defaultFile);
                table = DatTable.LoadLegacy(stream, legacyDecl);
            }
            foreach ((var firegraftId, var fieldId) in table.MissingRequirements())
            {
                var data = firegraft.GetRequirements(firegraftId, table.Entries);
                table.ResetListField(fieldId, data.Offsets, new byte[][] { data.Data });
            }
            return table;
        }

        static DatTable LoadButtons(
            string path,
            LegacyDatDecl legacyDecl,
            DatTable units,
            FiregraftData firegraft
        ) {
            // Buttons are loaded from extended dat, or as a fallback, converted from firegraft
            // arrays Unit / Buts. Unit array will be updated to have a button entry if loaded
            // from firegraft.
            DatTable table;
            try
            {
                using (var file = File.OpenRead(path))
                {
                    table = DatTable.LoadNew(file, legacyDecl);
                }
            }
            catch (FileNotFoundException)
            {
                // Firegraft data only contains buttons that have been changed; load base
                // buttons first and then patch firegraft data on that.
                var startOffsets = new BinaryWriter(new MemoryStream());
                var buttonCounts = new List<byte>();
                var buttonData = Enumerable.Range(0, 8)
                    .Select(x => new BinaryWriter(new MemoryStream()))
                    .ToArray();
                var unitButtons = Enumerable.Range(0, (int)units.Entries)
                    .SelectMany(x => new byte[] { 0, 0 })
                    .ToArray();
                var unitLinked = Enumerable.Range(0, (int)units.Entries)
                    .SelectMany(x => new byte[] { 0xff, 0xff })
                    .ToArray();
                var buttonsStream = new BinaryReader(new MemoryStream(Properties.Resources.buttons_bin));
                var baseSets = (int)buttonsStream.ReadUInt16();
                var baseButtons = (int)buttonsStream.ReadUInt16();
                // Used to map firegraft buttonset ids to buttons.dat ids.
                // First it is initialized to contain buttonsets that weren't in
                // firegraft, then later when firegraft-added buttonsets are processed,
                // the mapping is updated to point to any new ids.
                var buttonsetIdToButtonIndex = new Dictionary<uint, int>();
                for (int i = 0; i < 0xfa; i++)
                {
                    var span = new Span<byte>(unitButtons, i * 2, 2);
                    BinaryPrimitives.WriteUInt16LittleEndian(span, (UInt16)buttonsStream.ReadByte());
                }
                for (int i = 0; i < 0xfa; i++)
                {
                    var linked = buttonsStream.ReadUInt16();
                    var span = new Span<byte>(unitLinked, i * 2, 2);
                    BinaryPrimitives.WriteUInt16LittleEndian(span, linked);
                }
                for (int i = 0; i < baseSets; i++)
                {
                    var off = buttonsStream.ReadUInt16();
                    startOffsets.WriteU32((UInt32)off);
                }
                for (int i = 0; i < baseSets; i++)
                {
                    var count = buttonsStream.ReadByte();
                    buttonCounts.Add(count);
                }
                for (int i = 0; i < baseSets; i++)
                {
                    var firegraftId = buttonsStream.ReadByte();
                    buttonsetIdToButtonIndex[(uint)firegraftId] = i;
                }
                for (int field = 0; field < 8; field++)
                {
                    for (int i = 0; i < baseButtons; i++)
                    {
                        if (field == 0)
                        {
                            var val = buttonsStream.ReadByte();
                            buttonData[field].WriteU8(val);
                        }
                        else
                        {
                            var val = buttonsStream.ReadUInt16();
                            buttonData[field].WriteU16(val);
                        }
                    }
                }
                // Create arrays
                table = DatTable.Empty((uint)baseSets, legacyDecl);
                table.AddField(
                    0x00,
                    DatFieldFormat.Uint32,
                    new List<byte>(((MemoryStream)startOffsets.BaseStream).ToArray())
                );
                table.AddField(0x01, DatFieldFormat.Uint8, buttonCounts);
                for (int j = 0; j < buttonData.Length; j++)
                {
                    var format = j == 0 ? DatFieldFormat.Uint8 : DatFieldFormat.Uint16;
                    var bytes = new List<byte>(((MemoryStream)buttonData[j].BaseStream).ToArray());
                    table.AddField((uint)j + 2, format, bytes);
                }
                units.AddField(0x44, DatFieldFormat.Uint16, new List<byte>(unitButtons));
                units.AddField(0x45, DatFieldFormat.Uint16, new List<byte>(unitLinked));

                // Add firegraft data; Override buttonset if all its users had been set in the
                // firegraft data, otherwise add a new one.
                {
                    var overriddenButtons = new HashSet<uint>();
                    int i = baseSets;
                    var fgUnits = firegraft.Units();
                    foreach (var set in firegraft.Buttons())
                    {
                        uint? firstUnit = fgUnits
                            .Where(x => x.ButtonSetId == set.ButtonSetId + 1)
                            .Select(x => x.UnitId)
                            .FirstOrDefault();
                        if (firstUnit == null)
                        {
                            // ?? Odd that FG has an unused buttonset
                            continue;
                        }

                        uint[][] newButtons = Enumerable.Range(0, 8)
                            .Select(j => {
                                return set.Buttons.Select(x => {
                                    return j switch
                                    {
                                        0 => (uint)x.Position,
                                        1 => (uint)x.Icon,
                                        2 => (uint)x.DisabledString,
                                        3 => (uint)x.EnabledString,
                                        4 => (uint)x.Condition,
                                        5 => (uint)x.ConditionParam,
                                        6 => (uint)x.Action,
                                        7 => (uint)x.ActionParam,
                                        _ => throw new Exception("Unreachable"),
                                    };
                                }).ToArray();
                            }).ToArray();

                        var oldSetId = units.GetFieldUint((uint)firstUnit, 0x44);
                        if (!overriddenButtons.Contains(oldSetId))
                        {
                            overriddenButtons.Add(oldSetId);
                            var oldSetUsers = Enumerable.Range(0, (int)units.Entries)
                                .Where(x => units.GetFieldUint((uint)x, 0x44) == oldSetId)
                                .Select(x => (uint)x)
                                .ToArray();
                            if (oldSetUsers.All(x => fgUnits.Any(y => y.UnitId == x)))
                            {
                                // Can override old
                                buttonsetIdToButtonIndex[set.ButtonSetId + 1] = (int)oldSetId;
                                table.SetListRaw(oldSetId, 0x00, newButtons);
                                continue;
                            }
                        }
                        // Create new entry
                        buttonsetIdToButtonIndex[set.ButtonSetId + 1] = i;
                        table.DuplicateEntry(0);
                        table.SetListRaw((uint)i, 0x00, newButtons);
                        i += 1;
                    }
                    foreach (var unit in firegraft.Units())
                    {
                        int index = 0;
                        buttonsetIdToButtonIndex.TryGetValue(unit.ButtonSetId, out index);
                        units.SetFieldUint(unit.UnitId, 0x44, (uint)index);
                        units.SetFieldUint(unit.UnitId, 0x45, unit.Linked);
                    }
                }
            }
            return table;
        }

        /// Path must be without extension. Tries both json/xml
        static StringTable LoadStringTable(string path, byte[] defaultFile)
        {
            try
            {
                using (var file = File.OpenRead($"{path}.json"))
                {
                    return StringTable.FromJson(file);
                }
            }
            catch (FileNotFoundException) { }
            try
            {
                using (var file = File.OpenRead($"{path}.xml"))
                {
                    return StringTable.FromXml(file);
                }
            }
            catch (FileNotFoundException) { }
            var stream = new MemoryStream(defaultFile);
            return StringTable.FromJson(stream);
        }

        /// File will be kept open as it is lazily read.
        static DdsGrp LoadDdsGrp(string path, byte[] defaultFile)
        {
            try
            {
                var file = File.OpenRead(path);
                return new DdsGrp(file);
            }
            catch (FileNotFoundException) { }
            var stream = new MemoryStream(defaultFile);
            return new DdsGrp(stream);
        }

        void WidenDatField(DatTable table, uint field)
        {
            if (table.FieldFormat(field) != DatFieldFormat.Uint32)
            {
                var stream = new MemoryStream();
                var writer = new BinaryWriter(stream);
                for (uint i = 0; i < table.Entries; i++)
                {
                    writer.WriteU32(table.GetFieldUint(i, field));
                }
                table.AddField(field, DatFieldFormat.Uint32, new List<byte>(stream.ToArray()));
            }
        }

        public override bool Equals(object? obj)
        {
            return obj is GameData data &&
                   EqualityComparer<DatTable>.Default.Equals(Units, data.Units) &&
                   EqualityComparer<DatTable>.Default.Equals(Weapons, data.Weapons) &&
                   EqualityComparer<DatTable>.Default.Equals(Upgrades, data.Upgrades) &&
                   EqualityComparer<DatTable>.Default.Equals(TechData, data.TechData) &&
                   EqualityComparer<DatTable>.Default.Equals(Flingy, data.Flingy) &&
                   EqualityComparer<DatTable>.Default.Equals(Sprites, data.Sprites) &&
                   EqualityComparer<DatTable>.Default.Equals(Images, data.Images) &&
                   EqualityComparer<DatTable>.Default.Equals(Orders, data.Orders) &&
                   EqualityComparer<DatTable>.Default.Equals(Buttons, data.Buttons);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                HashCode.Combine(Units, Weapons, Upgrades, TechData, Flingy, Sprites, Images),
                HashCode.Combine(Orders, Buttons)
            );
        }

        public DatTable Units { get; }
        public DatTable Weapons { get; }
        public DatTable Upgrades { get; }
        public DatTable TechData { get; }
        public DatTable Flingy { get; }
        public DatTable Sprites { get; }
        public DatTable Images { get; }
        public DatTable Orders { get; }
        public DatTable Buttons { get; }
        public StringTable StatTxt { get; }
        public DdsGrp CmdIcons { get; }

        public static bool operator ==(GameData? left, GameData? right)
        {
            return EqualityComparer<GameData>.Default.Equals(left, right);
        }

        public static bool operator !=(GameData? left, GameData? right)
        {
            return !(left == right);
        }
    }
}
