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
        public FiregraftData(string root)
        {
            this.root = root;
            this.data = new byte[0];
        }

        public Requirements GetRequirements(UInt32 id, uint entryCount)
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

        private void LoadData()
        {
            using (var file = GetFgpStream())
            {
                data = new byte[file.Length];
                file.Read(data, 0, (int)file.Length);
            }
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                while (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    var id = reader.ReadUInt32();
                    var len = reader.ReadInt32();
                    var offset = (int)reader.BaseStream.Position;
                    reader.BaseStream.Position += len;
                    sections.Add(new Section {
                        Id = id,
                        Offset = offset,
                        Length = len,
                    });
                }
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
                var path = Path.Join(root, "samase/firegraft.fgp");
                return File.OpenRead(path);
            }
            catch (FileNotFoundException) { }
            var file = Directory.EnumerateFiles(Path.Join(root, "Firegraft"), "*.fgp")
                .FirstOrDefault();
            if (file != null)
            {
                return File.OpenRead(file);
            }

            return new MemoryStream(Properties.Resources.firegraft_default_fgp);
        }

        string root;
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
    }
}
