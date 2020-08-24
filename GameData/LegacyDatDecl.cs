using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace Tatti3.GameData
{
    struct LegacyDatDecl
    {
        public struct Field
        {
            /// Bytes per entry
            public uint Size;
            /// E.g. Only certain range of units have addon position data, this and length
            /// tell the valid range of values
            public uint startIndex;
            public uint length;
            public uint SubIndexCount;
            /// Default value for fields which aren't defined for each entry. Must be
            /// same size as `Size`
            public byte[] DefaultValue;
            public DatFieldFormat format;
        }

        public Field[] fields;
        public RefField[] RefFields;
        public ListField[] ListFields;
        public uint entries;
        public uint FileSize;
        public uint InvalidIndexStart;
        public uint InvalidIndexCount;

        public byte[] defaultFile;

        static LegacyDatDecl()
        {
            Func<uint, uint, uint, byte[], DatFieldFormat, Field> MakeField =
                (size, start, length, defaultValue, format) =>
            {
                return new Field
                {
                    Size = size,
                    startIndex = start,
                    length = length,
                    DefaultValue = defaultValue,
                    format = format,
                    SubIndexCount = 1,
                };
            };
            Func<uint, uint, uint, byte[], DatFieldFormat, uint, Field> MakeSubIndexField =
                (size, start, length, defaultValue, format, subIndexCount) =>
            {
                return new Field
                {
                    Size = size,
                    startIndex = start,
                    length = length,
                    DefaultValue = defaultValue,
                    format = format,
                    SubIndexCount = subIndexCount,
                };
            };
            Func<ArrayFileType, uint, RefField> MakeRefField = (a, b) => new RefField(a, b, false);
            Func<ArrayFileType, uint, RefField> MakeRefFieldZeroOptional =
                (a, b) => new RefField(a, b, true);
            Func<string, UInt32> U32Code = input => {
                UInt32 result = 0;
                foreach (char x in input.Reverse())
                {
                    result = (result << 8) | ((byte)x);
                }
                return result;
            };
            byte[] u8Zero = { 0 };
            byte[] u16Zero = { 0, 0, };
            byte[] u32Zero = { 0, 0, 0, 0 };
            byte[] unitNone = { 228, 0 };
            byte[] weaponNone = { 130 };
            byte[] upgradeDefault = { 60 };
            {
                Func<Field> Uint8 = () => MakeField(1, 0, 228, u8Zero, DatFieldFormat.Uint8);
                Func<Field> Uint16 = () => MakeField(2, 0, 228, u16Zero, DatFieldFormat.Uint16);
                Func<Field> Uint32 = () => MakeField(4, 0, 228, u32Zero, DatFieldFormat.Uint32);
                Units = new LegacyDatDecl
                {
                    entries = 228 + 33,
                    FileSize = 19876,
                    InvalidIndexStart = 228,
                    InvalidIndexCount = 33,
                    defaultFile = Properties.Resources.arr_units_dat,
                    fields = new Field[] {
                        // 0x00 Flingy
                        Uint8(),
                        // 0x01 Subunit
                        Uint16(),
                        // 0x02 Subunit 2
                        Uint16(),
                        // 0x03 Infestation
                        MakeField(2, 106, 96, unitNone, DatFieldFormat.Uint16),
                        // 0x04 Construction image
                        Uint32(),
                        // 0x05 Direction
                        Uint8(),
                        // 0x06 Has shields
                        Uint8(),
                        // 0x07 Shields
                        Uint16(),
                        // 0x08 Hitpoints
                        Uint32(),
                        // 0x09 Elevation level
                        Uint8(),
                        // 0x0a Floating
                        Uint8(),
                        // 0x0b Rank
                        Uint8(),
                        // 0x0c Ai idle order
                        Uint8(),
                        // 0x0d Human idle order
                        Uint8(),
                        // 0x0e Return to idle order
                        Uint8(),
                        // 0x0f Attack unit order
                        Uint8(),
                        // 0x10 Attack move order
                        Uint8(),
                        // 0x11 Ground weapon
                        MakeField(1, 0, 228, weaponNone, DatFieldFormat.Uint8),
                        // 0x12 Ground weapon hits
                        Uint8(),
                        // 0x13 Air weapon
                        MakeField(1, 0, 228, weaponNone, DatFieldFormat.Uint8),
                        // 0x14 Air weapon hits
                        Uint8(),
                        // 0x15 AI flags
                        Uint8(),
                        // 0x16 Flags
                        Uint32(),
                        // 0x17 Target acquisition range
                        Uint8(),
                        // 0x18 Sight range
                        Uint8(),
                        // 0x19 Armor upgrade
                        MakeField(1, 0, 228, upgradeDefault, DatFieldFormat.Uint8),
                        // 0x1a Armor type
                        Uint8(),
                        // 0x1b Armor
                        Uint8(),
                        // 0x1c Rclick action
                        Uint8(),
                        // 0x1d Ready sound
                        MakeField(2, 0, 106, u16Zero, DatFieldFormat.Uint16),
                        // 0x1e First what sound
                        Uint16(),
                        // 0x1f Last what sound
                        Uint16(),
                        // 0x20 First annoyed sound
                        MakeField(2, 0, 106, u16Zero, DatFieldFormat.Uint16),
                        // 0x21 Last annoyed sound
                        MakeField(2, 0, 106, u16Zero, DatFieldFormat.Uint16),
                        // 0x22 First yes sound
                        MakeField(2, 0, 106, u16Zero, DatFieldFormat.Uint16),
                        // 0x23 Last yes sound
                        MakeField(2, 0, 106, u16Zero, DatFieldFormat.Uint16),
                        // 0x24 Placement box
                        MakeSubIndexField(4, 0, 228, u32Zero, DatFieldFormat.Uint16, 2),
                        // 0x25 Addon position
                        MakeSubIndexField(4, 0, 96, u32Zero, DatFieldFormat.Uint16, 2),
                        // 0x26 Dimension box
                        MakeSubIndexField(
                            8, 0, 228, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, DatFieldFormat.Uint16, 4
                        ),
                        // 0x27 Portrait
                        Uint16(),
                        // 0x28 Mineral cost
                        Uint16(),
                        // 0x29 Gas cost
                        Uint16(),
                        // 0x2a Build time
                        Uint16(),
                        // 0x2b Datreq offset
                        Uint16(),
                        // 0x2c Group flags
                        Uint8(),
                        // 0x2d Supply provided
                        Uint8(),
                        // 0x2e Supply cost
                        Uint8(),
                        // 0x2f Space required
                        Uint8(),
                        // 0x30 Space provided
                        Uint8(),
                        // 0x31 Build score
                        Uint16(),
                        // 0x32 Kill score
                        Uint16(),
                        // 0x33 Map label
                        Uint16(),
                        // 0x34 ???
                        Uint8(),
                        // 0x35 Misc flags
                        Uint16(),
                    },
                    RefFields = new RefField[] {
                        MakeRefField(ArrayFileType.Units, 0x01),
                        MakeRefField(ArrayFileType.Units, 0x02),
                        MakeRefField(ArrayFileType.Units, 0x03),
                        MakeRefField(ArrayFileType.Weapons, 0x11),
                        MakeRefField(ArrayFileType.Weapons, 0x13),
                        MakeRefField(ArrayFileType.Flingy, 0x00),
                        MakeRefField(ArrayFileType.Upgrades, 0x19),
                        MakeRefField(ArrayFileType.SfxData, 0x1d),
                        MakeRefField(ArrayFileType.SfxData, 0x1e),
                        MakeRefField(ArrayFileType.SfxData, 0x1f),
                        MakeRefField(ArrayFileType.SfxData, 0x20),
                        MakeRefField(ArrayFileType.SfxData, 0x21),
                        MakeRefField(ArrayFileType.SfxData, 0x22),
                        MakeRefField(ArrayFileType.SfxData, 0x23),
                        MakeRefField(ArrayFileType.CmdIcon, 0x43),
                    },
                    ListFields = new ListField[] {
                        new ListField(0x2b, 0x40, U32Code("UntR")),
                    },
                };
            }

            {
                Func<Field> Uint8 = () => MakeField(1, 0, 130, u8Zero, DatFieldFormat.Uint8);
                Func<Field> Uint16 = () => MakeField(2, 0, 130, u16Zero, DatFieldFormat.Uint16);
                Func<Field> Uint32 = () => MakeField(4, 0, 130, u32Zero, DatFieldFormat.Uint32);
                Weapons = new LegacyDatDecl
                {
                    entries = 130 + 1,
                    FileSize = 5460,
                    InvalidIndexStart = 130,
                    InvalidIndexCount = 1,
                    defaultFile = Properties.Resources.arr_weapons_dat,
                    fields = new Field[] {
                        // 0x00 Label
                        Uint16(),
                        // 0x01 Flingy
                        Uint32(),
                        // 0x02 ???
                        Uint8(),
                        // 0x03 Flags
                        Uint16(),
                        // 0x04 Min range
                        Uint32(),
                        // 0x05 Max range
                        Uint32(),
                        // 0x06 Upgrade
                        MakeField(1, 0, 130, upgradeDefault, DatFieldFormat.Uint8),
                        // 0x07 Damage type
                        Uint8(),
                        // 0x08 Behaviour
                        Uint8(),
                        // 0x09 Death time
                        Uint8(),
                        // 0x0a Effect
                        Uint8(),
                        // 0x0b Inner splash
                        Uint16(),
                        // 0x0c Middle splash
                        Uint16(),
                        // 0x0d Outer splash
                        Uint16(),
                        // 0x0e Damage
                        Uint16(),
                        // 0x0f Upgrade bonus
                        Uint16(),
                        // 0x10 Cooldown
                        Uint8(),
                        // 0x11 Factor
                        Uint8(),
                        // 0x12 Attack angle
                        Uint8(),
                        // 0x13 Launch spin
                        Uint8(),
                        // 0x14 X offset
                        Uint8(),
                        // 0x15 Y offset
                        Uint8(),
                        // 0x16 Error msg
                        Uint16(),
                        // 0x17 Icon
                        Uint16(),
                    },
                    RefFields = new RefField[] {
                        MakeRefField(ArrayFileType.StatTxt, 0x00),
                        MakeRefFieldZeroOptional(ArrayFileType.Flingy, 0x01),
                        MakeRefField(ArrayFileType.Upgrades, 0x06),
                        MakeRefField(ArrayFileType.StatTxt, 0x16),
                        MakeRefField(ArrayFileType.CmdIcon, 0x17),
                    },
                    ListFields = new ListField[] {},
                };
            }

            {
                Func<Field> Uint8 = () => MakeField(1, 0, 209, u8Zero, DatFieldFormat.Uint8);
                Func<Field> Uint16 = () => MakeField(2, 0, 209, u16Zero, DatFieldFormat.Uint16);
                Func<Field> Uint32 = () => MakeField(4, 0, 209, u32Zero, DatFieldFormat.Uint32);
                Flingy = new LegacyDatDecl
                {
                    entries = 209,
                    FileSize = 3135,
                    InvalidIndexStart = 0,
                    InvalidIndexCount = 0,
                    defaultFile = Properties.Resources.arr_flingy_dat,
                    fields = new Field[] {
                        // 0x00 Sprite ID
                        Uint16(),
                        // 0x01 Top speed
                        Uint32(),
                        // 0x02 Acceleration
                        Uint16(),
                        // 0x03 Halt distance
                        Uint32(),
                        // 0x04 Turn speed
                        Uint8(),
                        // 0x05 Unused
                        Uint8(),
                        // 0x06 Movement type
                        Uint8(),
                    },
                    RefFields = new RefField[] {
                        MakeRefField(ArrayFileType.Sprites, 0x00),
                    },
                    ListFields = new ListField[] {},
                };
            }

            {
                Func<Field> Uint8 = () => MakeField(1, 0, 517, u8Zero, DatFieldFormat.Uint8);
                Func<Field> Uint16 = () => MakeField(2, 0, 517, u16Zero, DatFieldFormat.Uint16);
                Func<Field> Uint32 = () => MakeField(4, 0, 517, u32Zero, DatFieldFormat.Uint32);
                Sprites = new LegacyDatDecl
                {
                    entries = 517,
                    FileSize = 3229,
                    InvalidIndexStart = 0,
                    InvalidIndexCount = 0,
                    defaultFile = Properties.Resources.arr_sprites_dat,
                    fields = new Field[] {
                        // 0x00 Image
                        Uint16(),
                        // 0x01 Healthbar
                        MakeField(1, 130, 387, u8Zero, DatFieldFormat.Uint8),
                        // 0x02 Unknown2
                        Uint8(),
                        // 0x03 Start as visible
                        Uint8(),
                        // 0x04 Selection circle
                        MakeField(1, 130, 387, u8Zero, DatFieldFormat.Uint8),
                        // 0x05 Image pos
                        MakeField(1, 130, 387, u8Zero, DatFieldFormat.Uint8),
                    },
                    RefFields = new RefField[] {
                        MakeRefField(ArrayFileType.Images, 0x00),
                    },
                    ListFields = new ListField[] {},
                };
            }

            {
                Func<Field> Uint8 = () => MakeField(1, 0, 999, u8Zero, DatFieldFormat.Uint8);
                Func<Field> Uint16 = () => MakeField(2, 0, 999, u16Zero, DatFieldFormat.Uint16);
                Func<Field> Uint32 = () => MakeField(4, 0, 999, u32Zero, DatFieldFormat.Uint32);
                Images = new LegacyDatDecl
                {
                    entries = 999,
                    FileSize = 37962,
                    InvalidIndexStart = 0,
                    InvalidIndexCount = 0,
                    defaultFile = Properties.Resources.arr_images_dat,
                    fields = new Field[] {
                        // 0x00 Grp
                        Uint32(),
                        // 0x01 Can turn
                        Uint8(),
                        // 0x02 Clickable
                        Uint8(),
                        // 0x03 Full iscript
                        Uint8(),
                        // 0x04 Draw if cloaked
                        Uint8(),
                        // 0x05 Drawfunc
                        Uint8(),
                        // 0x06 Remapping
                        Uint8(),
                        // 0x07 Iscript header
                        Uint32(),
                        // 0x08 Overlay
                        Uint32(),
                        // 0x09 Overlay
                        Uint32(),
                        // 0x0a Damage Overlay
                        Uint32(),
                        // 0x0b Special Overlay
                        Uint32(),
                        // 0x0c Landing Overlay
                        Uint32(),
                        // 0x0d Liftoff Overlay
                        Uint32(),
                    },
                    RefFields = new RefField[] {},
                    ListFields = new ListField[] {},
                };
            }

            {
                Func<Field> Uint8 = () => MakeField(1, 0, 61, u8Zero, DatFieldFormat.Uint8);
                Func<Field> Uint16 = () => MakeField(2, 0, 61, u16Zero, DatFieldFormat.Uint16);
                Func<Field> Uint32 = () => MakeField(4, 0, 61, u32Zero, DatFieldFormat.Uint32);
                Upgrades = new LegacyDatDecl
                {
                    entries = 62,
                    FileSize = 1281,
                    InvalidIndexStart = 61,
                    InvalidIndexCount = 1,
                    defaultFile = Properties.Resources.arr_upgrades_dat,
                    fields = new Field[] {
                        // 0x00 Mineral cost
                        Uint16(),
                        // 0x01 Mineral factor
                        Uint16(),
                        // 0x02 Gas cost
                        Uint16(),
                        // 0x03 Gas factor
                        Uint16(),
                        // 0x04 Time cost
                        Uint16(),
                        // 0x05 Time factor
                        Uint16(),
                        // 0x06 Dat req offset
                        Uint16(),
                        // 0x07 Icon
                        Uint16(),
                        // 0x08 Label
                        Uint16(),
                        // 0x09 Race
                        Uint8(),
                        // 0x0a Repeat count
                        Uint8(),
                        // 0x0b Brood war
                        Uint8(),
                    },
                    RefFields = new RefField[] {
                        MakeRefField(ArrayFileType.CmdIcon, 0x07),
                        MakeRefField(ArrayFileType.StatTxt, 0x08),
                    },
                    ListFields = new ListField[] {
                        new ListField(0x06, 0x10, U32Code("UpgR")),
                    },
                };
            }

            {
                Func<Field> Uint8 = () => MakeField(1, 0, 44, u8Zero, DatFieldFormat.Uint8);
                Func<Field> Uint16 = () => MakeField(2, 0, 44, u16Zero, DatFieldFormat.Uint16);
                Func<Field> Uint32 = () => MakeField(4, 0, 44, u32Zero, DatFieldFormat.Uint32);
                TechData = new LegacyDatDecl
                {
                    entries = 45,
                    FileSize = 836,
                    InvalidIndexStart = 44,
                    InvalidIndexCount = 1,
                    defaultFile = Properties.Resources.arr_techdata_dat,
                    fields = new Field[] {
                        // 0x00 Mineral cost
                        Uint16(),
                        // 0x01 Gas cost
                        Uint16(),
                        // 0x02 Time cost
                        Uint16(),
                        // 0x03 Energy cost
                        Uint16(),
                        // 0x04 Dat req offset
                        Uint16(),
                        // 0x05 Dat req offset 2
                        Uint16(),
                        // 0x06 Icon
                        Uint16(),
                        // 0x07 Label
                        Uint16(),
                        // 0x08 Unk?
                        Uint8(),
                        // 0x09 Misc?
                        Uint8(),
                        // 0x0a Brood War
                        Uint8(),
                    },
                    RefFields = new RefField[] {
                        MakeRefField(ArrayFileType.CmdIcon, 0x06),
                        MakeRefField(ArrayFileType.StatTxt, 0x07),
                    },
                    ListFields = new ListField[] {
                        new ListField(0x04, 0x10, U32Code("TecR")),
                        new ListField(0x05, 0x11, U32Code("TecU")),
                    },
                };
            }

            {
                Func<Field> Uint8 = () => MakeField(1, 0, 189, u8Zero, DatFieldFormat.Uint8);
                Func<Field> Uint16 = () => MakeField(2, 0, 189, u16Zero, DatFieldFormat.Uint16);
                Func<Field> Uint32 = () => MakeField(4, 0, 189, u32Zero, DatFieldFormat.Uint32);
                Orders = new LegacyDatDecl
                {
                    entries = 189,
                    FileSize = 4158,
                    InvalidIndexStart = 0,
                    InvalidIndexCount = 0,
                    defaultFile = Properties.Resources.arr_orders_dat,
                    fields = new Field[] {
                        // 0x00 Label
                        Uint16(),
                        // 0x01 Use weapon targeting
                        Uint8(),
                        // 0x02 Secondary order (unused)
                        Uint8(),
                        // 0x03 Non-subunit (unused)
                        Uint8(),
                        // 0x04 Subunit inherits
                        Uint8(),
                        // 0x05 Subunit can use (unused)
                        Uint8(),
                        // 0x06 Interruptable
                        Uint8(),
                        // 0x07 Stop moving before next queued
                        Uint8(),
                        // 0x08 Can be queued
                        Uint8(),
                        // 0x09 Keep target while disabled
                        Uint8(),
                        // 0x0a Clip to walkable terrain
                        Uint8(),
                        // 0x0b Fleeable
                        Uint8(),
                        // 0x0c Requires moving (unused)
                        Uint8(),
                        // 0x0d Order weapon
                        Uint8(),
                        // 0x0e Order tech
                        Uint8(),
                        // 0x0f Animation
                        Uint8(),
                        // 0x10 Icon
                        Uint16(),
                        // 0x11 Requirement offset
                        Uint16(),
                        // 0x12 Obscured order
                        Uint8(),
                    },
                    RefFields = new RefField[] {
                        MakeRefField(ArrayFileType.Weapons, 0x0d),
                        MakeRefField(ArrayFileType.TechData, 0x0e),
                        MakeRefField(ArrayFileType.CmdIcon, 0x10),
                        MakeRefField(ArrayFileType.Orders, 0x12),
                    },
                    ListFields = new ListField[] {
                        new ListField(0x11, 0x20, U32Code("OrdR")),
                    },
                };
            }
        }

        public static readonly LegacyDatDecl Units;
        public static readonly LegacyDatDecl Weapons;
        public static readonly LegacyDatDecl Flingy;
        public static readonly LegacyDatDecl Sprites;
        public static readonly LegacyDatDecl Images;
        public static readonly LegacyDatDecl Upgrades;
        public static readonly LegacyDatDecl TechData;
        public static readonly LegacyDatDecl Orders;
    }
}
