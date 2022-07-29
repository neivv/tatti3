using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

using static System.Buffers.Binary.BinaryPrimitives;

using Tatti3.GameData.BinaryWriterExt;

namespace Tatti3.GameData
{
    // Firegraft data is only read if the .dat files don't contain the data in themselves yet.
    // This class handles the data finding, file parsing, and keeps some of the work cached
    // after first load if multiple .dats need it.
    // (That is, it's a helper for the load function, not something that is kept alive after load)
    class FiregraftData
    {
        public FiregraftData(IFilesystem fsys)
        {
            this.fsys = fsys;
            this.data = Array.Empty<byte>();
        }

        public Requirements GetRequirements(UInt32 id, uint entryCount)
        {
            var section = GetSection(id);
            var span = new ReadOnlySpan<byte>(data, section.Offset, section.Length);
            var count = ReadUInt16LittleEndian(span);
            var pos = 2;
            var i = 0;
            var reqBuffer = new MemoryStream();
            var reqData = new BinaryWriter(reqBuffer);
            var offsetData = new byte[entryCount * 2];
            reqData.WriteU16(0);
            while (i < count)
            {
                var entryId = (int)span[pos];
                var reqCount = (int)span[pos + 1];
                var enabled = span[pos + 2 + reqCount * 2] != 0;
                if (enabled)
                {
                    // This entry id prefix isn't really necessary if bw doesn't need to
                    // build the offsets at runtime, but going to keep it anyway
                    reqData.WriteU16((UInt16)entryId);
                    var offset = reqData.BaseStream.Position / 2;
                    if (offset > 0xffff)
                    {
                        throw new InvalidDataException($"Firegraft reqs {id:x08} is too long");
                    }
                    if ((uint)entryId >= entryCount)
                    {
                        throw new InvalidDataException(
                            $"Firegraft reqs {id:x08} has invalid entry id ({entryId:x}, max is {entryCount:x})"
                        );
                    }

                    WriteUInt16LittleEndian(new Span<byte>(offsetData, entryId * 2, 2), (UInt16)offset);
                    var opcodes = span[(pos + 2)..][..(reqCount * 2)];
                    reqData.Write(opcodes);
                    // I hope firegraft usually ends with 0xffff, but if it doesn't, handle that
                    // as well
                    if (ReadUInt16LittleEndian(span[(pos + reqCount * 2)..]) != 0xffff)
                    {
                        reqData.WriteU16(0xffff);
                    }
                }
                pos += 3 + reqCount * 2;
                i += 1;
            }
            reqData.WriteU16(0xffff);
            if (pos != span.Length)
            {
                throw new InvalidDataException($"Firegraft reqs {id:x08} ended too early");
            }
            return new Requirements
            {
                Offsets = offsetData,
                Data = reqBuffer.ToArray(),
            };
        }

        public List<ButtonSet> Buttons()
        {
            var section = GetSection(U32Code("Buts"));
            var span = new ReadOnlySpan<byte>(data, section.Offset, section.Length);
            var count = ReadUInt16LittleEndian(span);
            var pos = 2;
            var i = 0;
            var result = new List<ButtonSet>((int)count);
            while (i < count)
            {
                var id = (uint)span[pos];
                var buttonCount = (int)span[pos + 1];
                pos += 2;
                var buttons = new ButtonSet
                {
                    ButtonSetId = id,
                    Buttons = new List<Button>(buttonCount),
                };
                for (int j = 0; j < buttonCount; j++)
                {
                    var position = ReadUInt16LittleEndian(span[(pos + 0)..]);
                    var icon = ReadUInt16LittleEndian(span[(pos + 2)..]);
                    var condition = ReadUInt32LittleEndian(span[(pos + 4)..]);
                    var action = ReadUInt32LittleEndian(span[(pos + 8)..]);
                    var condition_param = ReadUInt16LittleEndian(span[(pos + 12)..]);
                    var action_param = ReadUInt16LittleEndian(span[(pos + 14)..]);
                    var enabled_string = ReadUInt16LittleEndian(span[(pos + 16)..]);
                    var disabled_string = ReadUInt16LittleEndian(span[(pos + 18)..]);
                    buttons.Buttons.Add(new Button {
                        Position = (byte)position,
                        Icon = icon,
                        Condition = (UInt16)condition,
                        Action = (UInt16)action,
                        ConditionParam = condition_param,
                        ActionParam = action_param,
                        DisabledString = disabled_string,
                        EnabledString = enabled_string,
                    });
                    pos += 0x14;
                }
                result.Add(buttons);
                i += 1;
            }
            if (pos != span.Length)
            {
                throw new InvalidDataException($"Firegraft buttons ended too early");
            }
            return result;
        }

        public List<FiregraftUnit> Units()
        {
            var section = GetSection(U32Code("Unit"));
            var span = new ReadOnlySpan<byte>(data, section.Offset, section.Length);
            var count = ReadUInt16LittleEndian(span);
            var pos = 2;
            var i = 0;
            var result = new List<FiregraftUnit>((int)count);
            while (i < count)
            {
                var id = (uint)span[pos];
                var buttons = (uint)span[pos + 2];
                var linked = ReadUInt16LittleEndian(span[(pos + 3)..]);
                result.Add(new FiregraftUnit {
                    UnitId = id,
                    ButtonSetId = buttons,
                    Linked = linked,
                });
                if (span[pos + 5] == 0)
                {
                    // Not real unit (id >= 0xe4) for buttons. Doesn't matter here
                    // since we don't care about status screen fns.
                    pos += 6;
                }
                else
                {
                    pos += 9;
                }
                i += 1;
            }
            if (pos != span.Length)
            {
                throw new InvalidDataException($"Firegraft buttons ended too early");
            }
            return result;
        }

        Section GetSection(UInt32 id)
        {
            if (data.Length == 0)
            {
                LoadData();
            }
            var section = sections.Where(x => x.Id == id).FirstOrDefault();
            if (section.Length == 0)
            {
                throw new InvalidDataException($"Firegraft section {id:x08} does not exist");
            }
            return section;
        }

        static private UInt32 U32Code(string input) {
            UInt32 result = 0;
            foreach (char x in input.Reverse())
            {
                result = (result << 8) | ((byte)x);
            }
            return result;
        }

        private void LoadData()
        {
            using (var file = GetFgpStream())
            {
                data = new byte[file.Length];
                file.Read(data, 0, (int)file.Length);
            }
            using var reader = new BinaryReader(new MemoryStream(data));
            while (reader.BaseStream.Position != reader.BaseStream.Length)
            {
                var id = reader.ReadUInt32();
                var len = reader.ReadInt32();
                var offset = (int)reader.BaseStream.Position;
                reader.BaseStream.Position += len;
                sections.Add(new Section
                {
                    Id = id,
                    Offset = offset,
                    Length = len,
                });
            }
        }

        private Stream GetFgpStream()
        {
            // Try to get .fgp from
            // 1) samase/firegraft.fgp
            // 2) Firegraft/*.fgp
            // 3) Embedded default resource
            try
            {
                return fsys.OpenFile("samase/firegraft.fgp");
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException) {}
            if (fsys.DirectoryExists("Firegraft"))
            {
                var file = fsys.EnumerateFiles("Firegraft", "*.fgp")
                    .FirstOrDefault();
                if (file != null)
                {
                    return fsys.OpenFile(file);
                }
            }

            return new MemoryStream(Properties.Resources.firegraft_default_fgp);
        }

        IFilesystem fsys;
        byte[] data;
        List<Section> sections = new List<Section>();

        private struct Section
        {
            public UInt32 Id;
            public int Offset;
            public int Length;
        }

        public struct Requirements
        {
            public byte[] Offsets;
            public byte[] Data;
        }

        public struct ButtonSet
        {
            public List<Button> Buttons;
            public uint ButtonSetId;
        }

        public struct Button
        {
            public byte Position;
            public UInt16 Icon;
            public UInt16 Condition;
            public UInt16 Action;
            public UInt16 ConditionParam;
            public UInt16 ActionParam;
            public UInt16 DisabledString;
            public UInt16 EnabledString;
        }

        public struct FiregraftUnit
        {
            public uint UnitId;
            public uint ButtonSetId;
            public uint Linked;
        }
    }
}
