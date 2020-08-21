using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Tatti3.GameData
{
    class GameData
    {
        GameData(string root)
        {
            var firegraft = new FiregraftData(root);
            Units = LoadDatTable(Path.Join(root, "arr/units.dat"), LegacyDatDecl.Units, firegraft);
            Weapons = LoadDatTable(Path.Join(root, "arr/weapons.dat"), LegacyDatDecl.Weapons, firegraft);
            Upgrades =
                LoadDatTable(Path.Join(root, "arr/upgrades.dat"), LegacyDatDecl.Upgrades, firegraft);
            TechData = LoadDatTable(Path.Join(root, "arr/techdata.dat"), LegacyDatDecl.TechData, firegraft);
            Flingy = LoadDatTable(Path.Join(root, "arr/flingy.dat"), LegacyDatDecl.Flingy, firegraft);
            Sprites = LoadDatTable(Path.Join(root, "arr/sprites.dat"), LegacyDatDecl.Sprites, firegraft);
            Images = LoadDatTable(Path.Join(root, "arr/images.dat"), LegacyDatDecl.Images, firegraft);
            Orders = LoadDatTable(Path.Join(root, "arr/orders.dat"), LegacyDatDecl.Orders, firegraft);
            StatTxt = LoadStringTable(Path.Join(root, "rez/stat_txt"), Properties.Resources.rez_stat_txt_json);
            CmdIcons = LoadDdsGrp(
                Path.Join(root, "HD2/unit/cmdicons/cmdicons.dds.grp"),
                Properties.Resources.cmdicons_dds_grp
            );
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
                SaveDatTable(tempFiles, Weapons, Path.Join(root, "arr/weapons.dat"));
                SaveDatTable(tempFiles, Flingy, Path.Join(root, "arr/flingy.dat"));
                SaveDatTable(tempFiles, Upgrades, Path.Join(root, "arr/upgrades.dat"));
                SaveDatTable(tempFiles, TechData, Path.Join(root, "arr/techdata.dat"));
                SaveDatTable(tempFiles, Orders, Path.Join(root, "arr/orders.dat"));
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
                table.ResetListField(fieldId, data.Offsets, data.Data);
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
                   EqualityComparer<DatTable>.Default.Equals(Orders, data.Orders);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Units, Weapons, Upgrades, TechData, Flingy, Sprites, Images,
                    Orders);
        }

        public DatTable Units { get; }
        public DatTable Weapons { get; }
        public DatTable Upgrades { get; }
        public DatTable TechData { get; }
        public DatTable Flingy { get; }
        public DatTable Sprites { get; }
        public DatTable Images { get; }
        public DatTable Orders { get; }
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
