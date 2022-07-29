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
        GameData(string root) : this(new OsFilesystem(root))
        {
        }

        GameData(IFilesystem fsys)
        {
            var firegraft = new FiregraftData(fsys);
            Units = LoadDatTable(fsys, "arr/units.dat", LegacyDatDecl.Units, firegraft);
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
            // Movement speed multiplier, ext flags
            if (!Units.HasField(0x46))
            {
                Units.AddZeroField(0x46, DatFieldFormat.Uint16);
                Units.AddZeroField(0x47, DatFieldFormat.Uint32);
                var defaultSpeeds = new (uint, uint)[] {
                    (0x30, 1536), (0x35, 1536), (0x36, 1536), (0x39, 4102), (0x4d, 1536),
                    (0x50, 1366), (0x58, 1366), (0x67, 1536),
                };
                for (uint i = 0; i < Units.Entries; i++)
                {
                    Units.SetFieldUint(i, 0x46, 1024);
                }
                foreach (var (unit, multiplier) in defaultSpeeds)
                {
                    Units.SetFieldUint(unit, 0x46, multiplier);
                }
                Units.SetFieldUint(0x36, 0x47, 0x1);
            }
            // Turret max angle
            if (!Units.HasField(0x48))
            {
                Units.AddZeroField(0x48, DatFieldFormat.Uint8);
                for (uint i = 0; i < Units.Entries; i++)
                {
                    Units.SetFieldUint(i, 0x48, 128);
                }
                Units.SetFieldUint(0x4, 0x48, 32);
                Units.SetFieldUint(0x12, 0x48, 32);
            }
            // Bunker range bonus
            if (!Units.HasField(0x49))
            {
                Units.AddZeroField(0x49, DatFieldFormat.Uint16);
                for (uint i = 0; i < Units.Entries; i++)
                {
                    Units.SetFieldUint(i, 0x49, 0x40);
                }
            }
            Weapons = LoadDatTable(fsys, "arr/weapons.dat", LegacyDatDecl.Weapons, firegraft);
            Upgrades = LoadDatTable(fsys, "arr/upgrades.dat", LegacyDatDecl.Upgrades, firegraft);
            // Attached units
            if (!Upgrades.HasField(0x11))
            {
                Upgrades.AddZeroField(0x11, DatFieldFormat.Uint16);
                Upgrades.AddZeroField(0x12, DatFieldFormat.Uint16);
                Upgrades.AddField(0x13, DatFieldFormat.Uint16, new List<byte>());
                var defaultValues = new (uint, uint)[] {
                    (0x10, 0x00), (0x14, 0x01), (0x15, 0x01), (0x11, 0x02), (0x16, 0x08),
                    (0x13, 0x09), (0x17, 0x0c), (0x18, 0x2a), (0x19, 0x2a), (0x1a, 0x2a),
                    (0x1b, 0x25), (0x1c, 0x25), (0x1d, 0x26), (0x1e, 0x26), (0x1f, 0x2d),
                    (0x20, 0x2e), (0x21, 0x42), (0x22, 0x41), (0x23, 0x53), (0x24, 0x53),
                    (0x25, 0x45), (0x26, 0x54), (0x27, 0x54), (0x28, 0x43), (0x29, 0x46),
                    (0x2a, 0x46), (0x2b, 0x48), (0x2c, 0x47), (0x36, 0x03), (0x35, 0x27),
                    (0x34, 0x27), (0x33, 0x22), (0x31, 0x3f), (0x2f, 0x3c),
                };
                foreach (var (upgrade, unit) in defaultValues)
                {
                    Upgrades.SetListRaw(upgrade, 0x11, new uint[][] { new uint[]{ unit }});
                }
            }
            bool hasEffect1 = Upgrades.HasField(0x14);
            // This has to be read first since effect1 adds 0x1b too.
            bool hasEffect2 = Upgrades.HasField(0x1b);
            // Upgrade Effects
            if (!hasEffect1)
            {
                Upgrades.AddZeroField(0x14, DatFieldFormat.Uint16);
                Upgrades.AddZeroField(0x15, DatFieldFormat.Uint16);
                Upgrades.AddField(0x16, DatFieldFormat.Uint8, new List<byte>());
                Upgrades.AddField(0x17, DatFieldFormat.Uint8, new List<byte>());
                Upgrades.AddField(0x18, DatFieldFormat.Uint8, new List<byte>());
                Upgrades.AddField(0x19, DatFieldFormat.Uint16, new List<byte>());
                Upgrades.AddField(0x1a, DatFieldFormat.Uint32, new List<byte>());
                // Effect 2, will also be added with AddListField
                // below if hasEffect1 was true but hasEffect2 false
                Upgrades.AddField(0x1b, DatFieldFormat.Uint32, new List<byte>());
                var defaultValues = new (uint, uint, uint, uint)[] {
                    (0x11, 0x02, 0x00, 512),
                    (0x11, 0x13, 0x00, 512),
                    (0x1a, 0x2a, 0x00, 3078),
                    (0x1b, 0x25, 0x00, 512),
                    (0x1c, 0x25, 0x01, 0),
                    (0x1d, 0x26, 0x00, 512),
                    (0x22, 0x41, 0x00, 512),
                    (0x25, 0x45, 0x00, 512),
                    (0x27, 0x54, 0x00, 512),
                    (0x2a, 0x46, 0x00, 342),
                    (0x35, 0x27, 0x00, 512),
                };
                foreach (var (upgrade, unit, type, val) in defaultValues)
                {
                    var old = Upgrades.GetListRaw(upgrade, 0x14);
                    Upgrades.SetListRaw(upgrade, 0x14, new uint[][] {
                        ArrayPush(old[0], type),
                        ArrayPush(old[1], 1),
                        ArrayPush(old[2], 255),
                        ArrayPush(old[3], unit),
                        ArrayPush(old[4], val),
                        ArrayPush(old[5], 0),
                    });
                }
            }
            // Upgrade effect stat 2
            if (!hasEffect2)
            {
                Upgrades.AddListField(0x14, 0x1b, DatFieldFormat.Uint32, 0);
                // Attack/sight range upgrades
                var defaultValues = new (uint, uint, uint, uint, uint, uint)[] {
                    (0x10, 0x00, 2, 0x20, 0x82, 1),
                    (0x1e, 0x26, 2, 0x20, 0x82, 1),
                    (0x21, 0x42, 2, 0x40, 0x82, 1),
                    (0x21, 0x4e, 2, 0x40, 0x82, 0),
                    (0x36, 0x3, 2, 0x60, 0x8, 1),
                    (0x36, 0x4, 2, 0x60, 0x8, 1),
                    (0x36, 0x11, 2, 0x60, 0xa, 0),
                    (0x36, 0x12, 2, 0x60, 0xa, 0),
                    (0x14, 0x1, 3, 0, 0, 1),
                    (0x19, 0x2a, 3, 0, 0, 1),
                    (0x26, 0x54, 3, 0, 0, 1),
                    (0x29, 0x46, 3, 0, 0, 1),
                };
                foreach (var (upgrade, unit, type, val_, weapon, level) in defaultValues)
                {
                    var old = Upgrades.GetListRaw(upgrade, 0x14);
                    uint val = val_;
                    if (type == 3)
                    {
                        // Sight range upgrade was hardcoded to set sight to 0xb,
                        // have it offset by whatever makes it work
                        val = 0xb - Units.GetFieldUint(unit, 0x18);
                    }
                    Upgrades.SetListRaw(upgrade, 0x14, new uint[][] {
                        ArrayPush(old[0], type),
                        ArrayPush(old[1], level),
                        ArrayPush(old[2], 255),
                        ArrayPush(old[3], unit),
                        ArrayPush(old[4], val),
                        ArrayPush(old[5], weapon),
                    });
                }
            }
            TechData = LoadDatTable(fsys, "arr/techdata.dat", LegacyDatDecl.TechData, firegraft);
            if (!TechData.HasField(0x12))
            {
                TechData.AddZeroField(0x12, DatFieldFormat.Uint16);
                TechData.AddZeroField(0x13, DatFieldFormat.Uint16);
                TechData.AddField(0x14, DatFieldFormat.Uint16, new List<byte>());
                var defaultValues = new (uint, uint)[] {
                    (0x06, 0x09), (0x07, 0x09), (0x02, 0x09), (0x01, 0x01), (0x0a, 0x01),
                    (0x03, 0x02), (0x09, 0x08), (0x08, 0x0c), (0x0c, 0x2d), (0x12, 0x2d),
                    (0x0d, 0x2d), (0x11, 0x2d), (0x0e, 0x2e), (0x0f, 0x2e), (0x10, 0x2e),
                    (0x17, 0x43), (0x13, 0x43), (0x14, 0x43), (0x15, 0x47), (0x16, 0x47),
                    (0x22, 0x22), (0x18, 0x22), (0x1e, 0x22), (0x19, 0x3c), (0x1c, 0x3d),
                    (0x1b, 0x3f), (0x1d, 0x3f), (0x1f, 0x3f),
                };
                foreach (var (upgrade, unit) in defaultValues)
                {
                    TechData.SetListRaw(upgrade, 0x12, new uint[][] { new uint[]{ unit }});
                }
                TechData.SetListRaw(0x00, 0x12, new uint[][] { new uint[]{ 0x00, 0x20 }});
                TechData.SetListRaw(0x05, 0x12, new uint[][] { new uint[]{ 0x05, 0x1e }});
                TechData.SetListRaw(0x0b, 0x12, new uint[][] { new uint[]{ 0x25, 0x26, 0x29, 0x2e, 0x32 }});
                TechData.SetListRaw(0x20, 0x12, new uint[][] { new uint[]{ 0x26, 0x67 }});
            }
            Flingy = LoadDatTable(fsys, "arr/flingy.dat", LegacyDatDecl.Flingy, firegraft);
            Sprites = LoadDatTable(fsys, "arr/sprites.dat", LegacyDatDecl.Sprites, firegraft);
            Images = LoadDatTable(fsys, "arr/images.dat", LegacyDatDecl.Images, firegraft);
            PortData = LoadDatTable(fsys, "arr/portdata.dat", LegacyDatDecl.PortData, firegraft);
            Orders = LoadDatTable(fsys, "arr/orders.dat", LegacyDatDecl.Orders, firegraft);
            if (Orders.Version < 2)
            {
                // Dummy reqs
                var orders = new uint[] { 0x24, 0x66 };
                foreach (var order in orders)
                {
                    var reqs = Orders.GetRequirements(order, 0x11);
                    reqs.Add(new Requirement(0xff13));
                    Orders.SetRequirements(order, 0x11, reqs.ToArray());
                }
            }
            if (Orders.Version < 3)
            {
                var reqs = Orders.GetRequirements(0x24, 0x11);
                reqs.Remove(new Requirement(0xff13));
                reqs.Add(new Requirement(0xff0c));
                Orders.SetRequirements(0x24, 0x11, reqs.ToArray());
            }
            if (Units.Version < 4)
            {
                for (uint i = 228; i < 261; i++)
                {
                    Units.SetFieldUint(i, 0x01, 0xe4);
                    Units.SetFieldUint(i, 0x02, 0xe4);
                    Units.SetFieldUint(i, 0x03, 0xe4);
                    Units.SetFieldUint(i, 0x0c, 23);
                    Units.SetFieldUint(i, 0x0d, 23);
                    Units.SetFieldUint(i, 0x0e, 23);
                    Units.SetFieldUint(i, 0x0f, 23);
                    Units.SetFieldUint(i, 0x10, 23);
                    Units.SetFieldUint(i, 0x11, 130);
                    Units.SetFieldUint(i, 0x13, 130);
                    Units.SetFieldUint(i, 0x18, 1);
                }
            }
            if (Units.Version < 5)
            {
                // No cloak aggression
                var ghosts = new uint[] { 0x1, 0x10, 0x64, 0x63, 0x68 };
                foreach (var unit in ghosts)
                {
                    var old = Units.GetFieldUint(unit, 0x47);
                    Units.SetFieldUint(unit, 0x47, old | 0x2);
                }
            }
            Buttons = LoadButtons(fsys, "arr/buttons.dat", LegacyDatDecl.Buttons, Units, firegraft);
            StatTxt = LoadStringTable(fsys, "rez/stat_txt", Properties.Resources.rez_stat_txt_json);
            ImagesTbl = LoadTbl(fsys, "arr/images.tbl", Properties.Resources.arr_images_tbl);
            PortDataTbl = LoadTbl(fsys, "arr/portdata.tbl", Properties.Resources.arr_portdata_tbl);
            Sfx = LoadSfx(fsys, "rez/sfx.json", Properties.Resources.rez_sfx_json);
            CmdIcons = LoadDdsGrp(
                fsys,
                "HD2/unit/cmdicons/cmdicons.dds.grp",
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
            PortData = new DatTable(other.PortData);
            Orders = new DatTable(other.Orders);
            Buttons = new DatTable(other.Buttons);
            // Ok as long as this program doesn't support TBL editing
            StatTxt = other.StatTxt;
            CmdIcons = other.CmdIcons;
            ImagesTbl = other.ImagesTbl;
            PortDataTbl = other.PortDataTbl;
            Sfx = other.Sfx;
        }

        public static GameData Open(string root)
        {
            return new GameData(root);
        }

        public static GameData Open(IFilesystem root)
        {
            return new GameData(root);
        }

        public void Save(string root)
        {
            var arrDir = Path.Join(root, "arr");
            if (!Directory.Exists(arrDir)) {
                Directory.CreateDirectory(arrDir);
            }
            using var tempFiles = new WriteTempFiles();
            SaveDatTable(tempFiles, Units, Path.Join(root, "arr/units.dat"));
            SaveDatTable(tempFiles, Weapons, Path.Join(root, "arr/weapons.dat"));
            SaveDatTable(tempFiles, Flingy, Path.Join(root, "arr/flingy.dat"));
            SaveDatTable(tempFiles, Sprites, Path.Join(root, "arr/sprites.dat"));
            SaveDatTable(tempFiles, Upgrades, Path.Join(root, "arr/upgrades.dat"));
            SaveDatTable(tempFiles, TechData, Path.Join(root, "arr/techdata.dat"));
            SaveDatTable(tempFiles, PortData, Path.Join(root, "arr/portdata.dat"));
            SaveDatTable(tempFiles, Orders, Path.Join(root, "arr/orders.dat"));
            SaveDatTable(tempFiles, Buttons, Path.Join(root, "arr/buttons.dat"));
            tempFiles.Commit();
        }

        void SaveDatTable(WriteTempFiles tempFiles, DatTable dat, string path)
        {
            try
            {
                using var file = tempFiles.NewFile(path);
                dat.Write(file);
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to save {path}", e);
            }
        }

        static DatTable LoadDatTable(
            IFilesystem fsys,
            string path,
            LegacyDatDecl legacyDecl,
            FiregraftData firegraft
        ) {
            DatTable table;
            try
            {
                using var file = fsys.OpenFile(path);
                if (file.Length == legacyDecl.FileSize)
                {
                    table = DatTable.LoadLegacy(file, legacyDecl);
                }
                else
                {
                    table = DatTable.LoadNew(file, legacyDecl);
                }
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
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
            IFilesystem fsys,
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
                using var file = fsys.OpenFile(path);
                table = DatTable.LoadNew(file, legacyDecl);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
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
        static StringTable LoadStringTable(IFilesystem fsys, string path, byte[] defaultFile)
        {
            try
            {
                using var file = fsys.OpenFile($"{path}.json");
                return StringTable.FromJson(file);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException) { }
            try
            {
                using var file = fsys.OpenFile($"{path}.xml");
                return StringTable.FromXml(file);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException) { }
            var stream = new MemoryStream(defaultFile);
            return StringTable.FromJson(stream);
        }

        static StringTable LoadTbl(IFilesystem fsys, string path, byte[] defaultFile)
        {
            try
            {
                using var file = fsys.OpenFile(path);
                return StringTable.FromTbl(file);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException) { }
            var stream = new MemoryStream(defaultFile);
            return StringTable.FromTbl(stream);
        }

        static SfxData LoadSfx(IFilesystem fsys, string path, byte[] defaultFile)
        {
            try
            {
                using var file = fsys.OpenFile(path);
                return SfxData.FromJson(file);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException) { }
            var stream = new MemoryStream(defaultFile);
            return SfxData.FromJson(stream);
        }

        /// File will be kept open as it is lazily read.
        static DdsGrp LoadDdsGrp(IFilesystem fsys, string path, byte[] defaultFile)
        {
            try
            {
                var file = fsys.OpenFile(path);
                return new DdsGrp(file);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException) { }
            var stream = new MemoryStream(defaultFile);
            return new DdsGrp(stream);
        }

        static void WidenDatField(DatTable table, uint field)
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

        static uint[] ArrayPush(uint[] array, uint value)
        {
            var newArr = new uint[array.Length + 1];
            array.CopyTo(newArr, 0);
            newArr[array.Length] = value;
            return newArr;
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
                   EqualityComparer<DatTable>.Default.Equals(PortData, data.PortData) &&
                   EqualityComparer<DatTable>.Default.Equals(Orders, data.Orders) &&
                   EqualityComparer<DatTable>.Default.Equals(Buttons, data.Buttons);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                HashCode.Combine(Units, Weapons, Upgrades, TechData, Flingy, Sprites, Images),
                HashCode.Combine(Orders, Buttons, PortData)
            );
        }

        public DatTable Units { get; }
        public DatTable Weapons { get; }
        public DatTable Upgrades { get; }
        public DatTable TechData { get; }
        public DatTable Flingy { get; }
        public DatTable Sprites { get; }
        public DatTable Images { get; }
        public DatTable PortData { get; }
        public DatTable Orders { get; }
        public DatTable Buttons { get; }
        public StringTable StatTxt { get; }
        public DdsGrp CmdIcons { get; }
        public StringTable ImagesTbl { get; }
        public StringTable PortDataTbl { get; }
        public SfxData Sfx { get; }

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
