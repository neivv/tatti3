using System;
using System.Buffers.Binary;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

using Tatti3.GameData.BinaryWriterExt;
using static Tatti3.GameData.ValueHelpers.Helpers;

namespace Tatti3.GameData
{
    class DatTable
    {
        DatTable(LegacyDatDecl decl)
        {
            fields = new Dictionary<uint, DatValue>();
            listFields = new Dictionary<uint, ListFieldState>();
            Entries = 0;
            RefFields = new List<RefField>(decl.RefFields);
            invalidIndexStart = decl.InvalidIndexStart;
            invalidIndexCount = decl.InvalidIndexCount;
            legacyDecl = decl;
        }

        public DatTable(DatTable other)
        {
            fields = new Dictionary<uint, DatValue>();
            listFields = new Dictionary<uint, ListFieldState>();
            RefFields = new List<RefField>();
            Assign(other);
        }

        public void Assign(DatTable other)
        {
            fields = DictClone(other.fields, v => new DatValue(v));
            listFields = DictClone(other.listFields, v => new ListFieldState(v));
            fields = new Dictionary<uint, DatValue>(other.fields.Count);
            foreach ((uint k, DatValue v) in other.fields)
            {
                fields.Add(k, new DatValue(v));
            }
            RefFields = other.RefFields;
            Entries = other.Entries;
            invalidIndexStart = other.invalidIndexStart;
            invalidIndexCount = other.invalidIndexCount;
            legacyDecl = other.legacyDecl;
        }

        static Dictionary<K, V> DictClone<K, V>(Dictionary<K, V> input, Func<V, V> cloneValue)
        where K: notnull
        {
            var copy = new Dictionary<K, V>(input.Count);
            foreach ((K k, V v) in input)
            {
                copy.Add(k, cloneValue(v));
            }
            return copy;
        }

        /// Initializes DatTable from data in the original .dat format
        public static DatTable LoadLegacy(Stream input, LegacyDatDecl decl)
        {
            var reader = new BinaryReader(input);
            var self = new DatTable(decl);
            self.Entries = decl.entries;
            uint index = 0;
            foreach (var field in decl.fields)
            {
                if (field.DefaultValue.Length != field.Size)
                {
                    throw new InvalidDataException(
                        $"Index {index:x} size is declared wrong. " +
                        $"Default length {field.DefaultValue.Length} size {field.Size}"
                    );
                }

                var data = new List<byte>();
                // Add default if the data doesn't start at 0
                for (uint i = 0; i < field.startIndex; i++)
                {
                    data.AddRange(field.DefaultValue);
                }
                uint readSize = field.Size * field.length;
                var bytes = reader.ReadBytes((int)readSize);
                if (bytes.Length != readSize)
                {
                    throw new EndOfStreamException();
                }
                data.AddRange(bytes);
                // Add rest as default if it isn't in file
                for (uint i = field.startIndex + field.length; i < decl.entries; i++)
                {
                    data.AddRange(field.DefaultValue);
                }

                self.fields.Add(index, new DatValue(data, field.format, field.SubIndexCount));
                index += 1;
            }
            InitListFields(self, decl);
            if (reader.BaseStream.Position != decl.FileSize)
            {
                throw new InvalidDataException(
                    $"Dat file format is wrong. Read {reader.BaseStream.Position:x} bytes, " +
                    $"expected {decl.FileSize:x} bytes."
                );
            }
            return self;
        }

        /// Initializes DatTable from data in the extended .dat format
        public static DatTable LoadNew(Stream data, LegacyDatDecl decl)
        {
            // Format:
            //  u16 major_version (1)
            //  u16 minor_version (4)
            //      Minor version is something that can increase while staying compatible with
            //      users that only understand older minor version - even if they won't support
            //      it completely. Major is a fully breaking change.
            //  u32 entry_count
            //  u32 field_count
            //  Field fields[]
            //
            // Field:
            //  u16 id
            //  u16 flags
            //      0x03 = Int size: 0x00 = u8, 0x01 = u16, 0x02 = u32, 0x03 = u64
            //          Users should handle differently sized int arrays, and widen/narrow
            //          them to the size they can handle. If they have to lose data due to
            //          narrowing it is fine to reject the file; but otherwise they should
            //          try to handle narrowing correctly.
            //          For fields containing multiple integers, e.g. dimensionbox, this
            //          is size of a single of such integers (u16 for dimensionbox)
            //  u32 file_offset
            //  u32 length
            //      Users should accept lengths larger than what they expect, and leave any
            //      trailing data they cannot understand unused/unread.
            var reader = new BinaryReader(data);
            var self = new DatTable(decl);
            var magic = reader.ReadUInt32();
            if (magic != 0x2b746144)
            {
                // Support files without magic
                reader.BaseStream.Seek(0, SeekOrigin.Begin);
            }
            var major = reader.ReadUInt16();
            var minor = reader.ReadUInt16();
            if (major != 1 || minor == 0)
            {
                throw new InvalidDataException($"Invalid dat file version {major:02x}:{minor:02x}");
            }
            if (minor > 4)
            {
                throw new InvalidDataException($"The dat appears to be saved with a newer version of this program");
            }
            self.Version = minor;
            self.Entries = reader.ReadUInt32();
            var fieldCount = reader.ReadInt32();
            if (fieldCount > 1024)
            {
                throw new InvalidDataException($"Too many fields: {fieldCount}");
            }
            var fields = new List<FieldDecl>(fieldCount);
            for (int i = 0; i < fieldCount; i++)
            {
                var fieldId = reader.ReadUInt16();
                var flags = reader.ReadUInt16();
                var offset = (long)reader.ReadUInt32();
                var length = reader.ReadInt32();
                fields.Add(new FieldDecl(fieldId, flags, offset, length));
            }
            foreach (var field in fields)
            {
                reader.BaseStream.Seek(field.Offset, SeekOrigin.Begin);
                byte[] bytes = new byte[field.Length];
                reader.Read(bytes, 0, field.Length);
                uint subIndexCount = field.FieldId < decl.fields.Length ?
                    decl.fields[(int)field.FieldId].SubIndexCount : 1;
                self.fields.Add(
                    field.FieldId,
                    new DatValue(new List<byte>(bytes), field.Format, subIndexCount)
                );
            }
            InitListFields(self, decl);
            return self;
        }

        public static DatTable Empty(uint entries, LegacyDatDecl decl)
        {
            var self = new DatTable(decl);
            self.Entries = entries;
            InitListFields(self, decl);
            return self;
        }

        static private void InitListFields(DatTable self, LegacyDatDecl decl)
        {
            foreach (var listField in decl.ListFields)
            {
                if (listField.FiregraftId != 0)
                {
                    uint dataId = listField.DataFieldIds[0];
                    if (self.fields.ContainsKey(dataId))
                    {
                        self.fields[dataId].DataFormat = DatFieldFormat.VariableLengthData;
                    }
                    self.listFields[listField.OffsetFieldId] =
                        ListFieldState.Requirements(listField.OffsetFieldId, dataId);
                }
                else
                {
                    self.listFields[listField.OffsetFieldId] = ListFieldState.WithLengthField(
                        listField.OffsetFieldId,
                        listField.DataFieldIds,
                        listField.LengthFieldId
                    );
                }
            }
        }

        public void Write(Stream output)
        {
            var old = new DatTable(this);
            try
            {
                var stream = new MemoryStream();
                DoWrite(stream);
                // To avoid corruption bugs, verify that the saved file would
                // load to a file equal to this, and that it would again save
                // to a file equal to first save.
                var bytes = stream.ToArray();
                var newTable = DatTable.LoadNew(new MemoryStream(bytes), legacyDecl);
                if (old != newTable)
                {
                    throw new Exception("Saved file does not produce an equivalent dat table");
                }
                var stream2 = new MemoryStream();
                newTable.DoWrite(stream2);
                var bytes2 = stream2.ToArray();
                if (!bytes.SequenceEqual(bytes2))
                {
                    throw new Exception("Saved file changes when saved again");
                }
                output.Write(bytes, 0, bytes.Length);
            }
            catch (Exception)
            {
                Assign(old);
                throw;
            }
        }

        void DoWrite(Stream output)
        {
            // Regenerate requirement data for changed lists (Mutating this)
            foreach (var field in listFields.Values)
            {
                if (field.ChangedEntries.Count != 0)
                {
                    var offsetsMem = new MemoryStream();
                    var offsets = new BinaryWriter(offsetsMem);
                    if (field.IsRequirement)
                    {
                        var dataMem = new MemoryStream();
                        var data = new BinaryWriter(dataMem);
                        data.WriteU16(0);
                        for (uint i = 0; i < Entries; i++)
                        {
                            var reqs = GetListRaw(i, field.OffsetFieldId)[0];
                            if (reqs.Length == 0)
                            {
                                offsets.WriteU16(0);
                            }
                            else
                            {
                                // Writing the entry id, to keep same format as BW uses,
                                // even though it isn't really used by anything
                                data.WriteU16((UInt16)i);
                                long pos = dataMem.Position / 2;
                                if (pos > 0xffff)
                                {
                                    throw new Exception("Too many dat requirements");
                                }
                                offsets.WriteU16((UInt16)pos);
                                foreach (var op in reqs)
                                {
                                    data.WriteU16((UInt16)op);
                                }
                                data.WriteU16(0xffff);
                            }
                        }
                        data.WriteU16(0xffff);
                        ResetListField(
                            field.OffsetFieldId,
                            offsetsMem.ToArray(),
                            new byte[][] { dataMem.ToArray() }
                        );
                    }
                    else
                    {
                        var lengthsMem = new MemoryStream();
                        var lengths = new BinaryWriter(lengthsMem);
                        var data = Enumerable.Range(0, field.DataFieldIds.Length)
                            .Select(x => new BinaryWriter(new MemoryStream()))
                            .ToArray();
                        int pos = 0;
                        for (uint i = 0; i < Entries; i++)
                        {
                            var lists = GetListRaw(i, field.OffsetFieldId);
                            offsets.WriteI32(pos);
                            pos += lists[0].Length;
                            lengths.WriteU8((byte)lists[0].Length);
                            for (int j = 0; j < lists.Length; j++)
                            {
                                var writer = data[j];
                                var format = fields[field.DataFieldIds[j]].DataFormat;
                                foreach (uint val in lists[j])
                                {
                                    switch (format)
                                    {
                                        case DatFieldFormat.Uint8:
                                            writer.WriteU8((byte)val);
                                            break;
                                        case DatFieldFormat.Uint16:
                                            writer.WriteU16((UInt16)val);
                                            break;
                                        case DatFieldFormat.Uint32:
                                            writer.WriteU32((UInt32)val);
                                            break;
                                        case DatFieldFormat.Uint64:
                                            writer.WriteU64((UInt64)val);
                                            break;
                                        default:
                                            throw new InvalidOperationException("Can't write uint list {field.DataFieldIds[j]:x} which has format {format}");
                                    }
                                }
                            }
                        }
                        AddField(field.OffsetFieldId, DatFieldFormat.Uint32, new List<byte>(offsetsMem.ToArray()));
                        AddField(
                            (uint)field.LengthFieldId!,
                            DatFieldFormat.Uint8,
                            new List<byte>(lengthsMem.ToArray())
                        );
                        for (int j = 0; j < field.DataFieldIds.Length; j++)
                        {
                            var array = ((MemoryStream)data[j].BaseStream).ToArray();
                            var format = fields[field.DataFieldIds[j]].DataFormat;
                            AddField(field.DataFieldIds[j], format, new List<byte>(array));
                        }
                    }
                }
            }

            using (var writer = new BinaryWriter(output, Encoding.UTF8, true))
            {
                writer.WriteU32(0x2b746144);
                writer.WriteU16(1);
                writer.WriteU16(4);
                writer.WriteU32(Entries);
                writer.WriteI32(fields.Count);
                var fieldIds = new List<uint>(fields.Keys);
                fieldIds.Sort();
                var fieldOffsets = new List<long>(fieldIds.Count);
                // Write field definitions
                foreach (uint fieldId in fieldIds)
                {
                    var field = fields[fieldId];
                    ushort flags = 0;
                    switch (field.DataFormat)
                    {
                        case DatFieldFormat.Uint8:
                            break;
                        case DatFieldFormat.Uint16:
                            flags |= 0x1;
                            break;
                        case DatFieldFormat.Uint32:
                            flags |= 0x2;
                            break;
                        case DatFieldFormat.Uint64:
                            flags |= 0x3;
                            break;
                        default:
                            break;
                    }

                    writer.WriteU16((UInt16)fieldId);
                    writer.WriteU16(flags);
                    fieldOffsets.Add(output.Position);
                    writer.WriteU32(0);
                    writer.WriteI32(field.Data.Count);
                }
                // Write fields + fix definition offset
                for (int i = 0; i < fieldIds.Count; i++)
                {
                    var fieldId = fieldIds[i];
                    var offsetPos = fieldOffsets[i];
                    var field = fields[fieldId];

                    var dataPos = output.Position;
                    writer.Write(field.Data.ToArray());
                    var newPos = output.Position;
                    output.Position = offsetPos;
                    writer.WriteU32((uint)dataPos);
                    output.Position = newPos;
                }
            }
        }

        public bool IsInvalidIndex(uint index)
        {
            return index >= invalidIndexStart && index < invalidIndexStart + invalidIndexCount;
        }

        public uint GetFieldUint(uint index, uint fieldId)
        {
            return GetFieldSubIndexUint(index, fieldId, 0);
        }

        public uint GetFieldSubIndexUint(uint index, uint fieldId, uint subIndex)
        {
            if (index >= Entries)
            {
                throw new ArgumentOutOfRangeException($"Index {index} is greater than maximum index {Entries}");
            }
            var field = fields[fieldId];
            if (field.SubIndexCount <= subIndex)
            {
                throw new ArgumentOutOfRangeException($"Invalid subindex {subIndex:x} for field {fieldId:x}");
            }
            uint offset = index * field.SubIndexCount + subIndex;
            return field.DataFormat switch
            {
                DatFieldFormat.Uint8 => field.Data[(int)offset],
                DatFieldFormat.Uint16 => ReadU16(field.Data, offset),
                DatFieldFormat.Uint32 => ReadU32(field.Data, offset),
                _ => throw new ArgumentException($"Dat field 0x{fieldId:x} cannot be read as uint"),
            };
        }

        static UInt16 ReadU16(List<byte> list, uint index)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(
                new ReadOnlySpan<byte>(list.GetRange((int)index * 2, 2).ToArray())
            );
        }

        static UInt32 ReadU32(List<byte> list, uint index)
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(
                new ReadOnlySpan<byte>(list.GetRange((int)index * 4, 4).ToArray())
            );
        }

        public void SetFieldUint(uint index, uint fieldId, uint value)
        {
            SetFieldSubIndexUint(index, fieldId, 0, value);
        }

        public void SetFieldSubIndexUint(uint index, uint fieldId, uint subIndex, uint value)
        {
            if (index >= Entries)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            var field = fields[fieldId];
            if (field.SubIndexCount <= subIndex)
            {
                throw new ArgumentOutOfRangeException($"Invalid subindex {subIndex:x} for field {fieldId:x}");
            }
            uint offset = index * field.SubIndexCount + subIndex;
            switch (field.DataFormat)
            {
                case DatFieldFormat.Uint8:
                    field.Data[(int)offset] = (byte)value;
                    break;
                case DatFieldFormat.Uint16:
                    WriteU16(field.Data, offset, value);
                    break;
                case DatFieldFormat.Uint32:
                    WriteU32(field.Data, offset, value);
                    break;
                case DatFieldFormat.Uint64:
                    WriteU64(field.Data, offset, value);
                    break;
                default:
                    throw new ArgumentException($"Dat field 0x{fieldId:x} cannot be written as uint");
            }
            FieldChanged?.Invoke(this, new FieldChangedEventArgs(fieldId, index));
        }

        static void WriteU16(List<byte> list, uint index, uint value)
        {
            int i = (int)index * 2;
            list[i] = (byte)value;
            list[i + 1] = (byte)(value >> 8);
        }

        static void WriteU32(List<byte> list, uint index, uint value)
        {
            int i = (int)index * 4;
            list[i] = (byte)value;
            list[i + 1] = (byte)(value >> 8);
            list[i + 2] = (byte)(value >> 16);
            list[i + 3] = (byte)(value >> 24);
        }

        static void WriteU64(List<byte> list, uint index, ulong value)
        {
            int i = (int)index * 8;
            list[i] = (byte)value;
            list[i + 1] = (byte)(value >> 8);
            list[i + 2] = (byte)(value >> 16);
            list[i + 3] = (byte)(value >> 24);
            list[i + 4] = (byte)(value >> 32);
            list[i + 5] = (byte)(value >> 40);
            list[i + 6] = (byte)(value >> 48);
            list[i + 7] = (byte)(value >> 56);
        }

        public void DuplicateEntry(uint sourceIndex)
        {
            var destIndex = Entries;
            Entries += 1;
            IterFields(
                listField => {
                    var field = fields[listField.OffsetFieldId];
                    AppendToField(field, 0);
                    if (listField.LengthFieldId != null)
                    {
                        var field2 = fields[(uint)listField.LengthFieldId];
                        AppendToField(field2, 0);
                    }
                    var opcodes = GetListRaw(sourceIndex, listField.OffsetFieldId);
                    SetListRaw(destIndex, listField.OffsetFieldId, opcodes);
                },
                (fieldId, field) => {
                    for (uint i = 0; i < field.SubIndexCount; i++)
                    {
                        var old = GetFieldSubIndexUint(sourceIndex, fieldId, i);
                        AppendToField(field, old);
                    }
                }
            );
            EntryCountChanged?.Invoke(this, new EventArgs());
        }

        // Duplicate helper.
        // Adds the field value for the new entry.
        private static void AppendToField(DatValue field, uint value)
        {
            int len = field.DataFormat switch
            {
                DatFieldFormat.Uint8 => 1,
                DatFieldFormat.Uint16 => 2,
                DatFieldFormat.Uint32 => 4,
                DatFieldFormat.Uint64 => 8,
                _ => throw new NotImplementedException(),
            };
            for (int i = 0; i < len; i++)
            {
                field.Data.Add((byte)value);
                value = value >> 8;
            }
        }

        public string SerializeEntryToJson(uint entry)
        {
            var result = new Dictionary<string, object>();
            var buffer = new List<uint>();
            IterFields(
                listField => {
                    var opcodes = GetListRaw(entry, listField.OffsetFieldId);
                    result.Add($"field_{listField.OffsetFieldId}", opcodes);
                },
                (fieldId, field) => {
                    if (field.SubIndexCount == 1)
                    {
                        result.Add($"field_{fieldId}", GetFieldSubIndexUint(entry, fieldId, 0));
                    }
                    else
                    {
                        buffer.Clear();
                        for (uint i = 0; i < field.SubIndexCount; i++)
                        {
                            buffer.Add(GetFieldSubIndexUint(entry, fieldId, i));
                        }
                        result.Add($"field_{fieldId}", buffer.ToArray());
                    }
                }
            );
            return JsonSerializer.Serialize(
                result,
                typeof(Dictionary<string, object>),
                new JsonSerializerOptions() {
                    WriteIndented = true,
                }
            );
        }

        public bool IsValidEntryJson(string json)
        {
            Dictionary<string, JsonElement> data;
            try
            {
                data = (Dictionary<string, JsonElement>)
                    JsonSerializer.Deserialize(json, typeof(Dictionary<string, JsonElement>))!;
            }
            catch (JsonException)
            {
                return false;
            }
            bool ValidateArray(JsonElement val, Func<JsonElement, bool> cb)
            {
                if (val.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }
                return val.EnumerateArray().All(x => cb(x));
            }
            bool IsJsonU32(JsonElement val)
            {
                return val.ValueKind == JsonValueKind.Number && val.TryGetUInt32(out _);
            }
            bool IsJsonU32Array(JsonElement val)
            {
                return ValidateArray(val, x => IsJsonU32(x));
            }
            uint JsonArrayLength(JsonElement val)
            {
                if (val.ValueKind != JsonValueKind.Array)
                {
                    return 0;
                }
                return (uint)val.GetArrayLength();
            }
            bool result = true;
            IterFields(
                listField => {
                    if (data.TryGetValue($"field_{listField.OffsetFieldId}", out JsonElement val))
                    {
                        if (!ValidateArray(val, x => IsJsonU32Array(x)))
                        {
                            result = false;
                        }
                    }
                    else
                    {
                        result = false;
                    }
                },
                (fieldId, field) => {
                    if (data.TryGetValue($"field_{fieldId}", out JsonElement val))
                    {
                        if ((field.SubIndexCount == 1 && !IsJsonU32(val)) ||
                            (field.SubIndexCount != 1 && !IsJsonU32Array(val)) ||
                            (field.SubIndexCount != 1 && JsonArrayLength(val) != field.SubIndexCount))
                        {
                            result = false;
                        }
                    }
                    else
                    {
                        result = false;
                    }
                }
            );
            return result;
        }

        public void DeserializeEntryFromJson(uint entry, string json)
        {
            Dictionary<string, JsonElement> data;
            data = (Dictionary<string, JsonElement>)
                JsonSerializer.Deserialize(json, typeof(Dictionary<string, JsonElement>))!;
            IterFields(
                listField => {
                    JsonElement val = data[$"field_{listField.OffsetFieldId}"];
                    UInt32[][] opcodes = val.EnumerateArray()
                        .Select(x => x.EnumerateArray().Select(y => y.GetUInt32()).ToArray())
                        .ToArray();
                    SetListRaw(entry, listField.OffsetFieldId, opcodes);
                },
                (fieldId, field) => {
                    JsonElement val = data[$"field_{fieldId}"];
                    if (field.SubIndexCount == 1)
                    {
                        SetFieldSubIndexUint(entry, fieldId, 0, val.GetUInt32());
                    }
                    else
                    {
                        uint i = 0;
                        foreach (uint value in val.EnumerateArray().Select(x => x.GetUInt32()))
                        {
                            SetFieldSubIndexUint(entry, fieldId, i, value);
                            i += 1;
                        }
                    }
                }
            );
        }

        /// Calls the callbacks for each field; list fields get the first callback
        /// and other get the second called.
        void IterFields(Action<ListFieldState> listCb, Action<uint, DatValue> normalCb)
        {
            var listFieldIds = new HashSet<uint>();
            foreach (var listField in listFields.Values)
            {
                foreach (var id in listField.DataFieldIds)
                {
                    listFieldIds.Add(id);
                }
                listFieldIds.Add(listField.OffsetFieldId);
                if (listField.LengthFieldId != null)
                {
                    listFieldIds.Add((uint)listField.LengthFieldId);
                }
                listCb(listField);
            }
            foreach (var pair in fields)
            {
                if (listFieldIds.Contains(pair.Key))
                {
                    // Lists were handled above
                    continue;
                }
                normalCb(pair.Key, pair.Value);
            }
        }

        public bool IsListField(uint fieldId)
        {
            return listFields.ContainsKey(fieldId);
        }

        public List<Requirement> GetRequirements(uint index, uint fieldId)
        {
            var raw = GetListRaw(index, fieldId)[0];
            UInt16[] rawu16 = raw.Select(x => (UInt16)x).ToArray();
            return Requirement.ListFromRaw(rawu16);
        }

        public UInt32[][] GetListRaw(uint index, uint fieldId)
        {
            var field = listFields[fieldId];
            if (field.ChangedEntries.ContainsKey(index))
            {
                return NestedArrayDeepCopy(field.ChangedEntries[index]);
            }
            else
            {
                return GetListFromData(index, field);
            }
        }

        // Reads list from .dat, skipping the mutation store layer
        // Returned value is { { entry_0_field1, entry_1_field2, ... }, { entry_0_field2, ...}, ... },
        // each field being a single array containing values for all entries.
        private UInt32[][] GetListFromData(uint index, ListFieldState field)
        {
            var offset = GetFieldUint(index, field.OffsetFieldId);
            var result = new List<UInt32[]>(field.DataFieldIds.Length);
            uint length = field.LengthFieldId != null ? GetFieldUint(index, (uint)field.LengthFieldId) : 0;
            foreach (var id in field.DataFieldIds)
            {
                var data = fields[id];
                result.Add(field.ReadData(offset, data, length));
            }
            return result.ToArray();
        }

        public void SetRequirements(uint index, uint fieldId, Requirement[] value)
        {
            if (index >= Entries || index > 0xffff)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            var raw = Requirement.ToRaw(value);
            UInt32[] rawu32 = raw.Select(x => (UInt32)x).ToArray();
            SetListRaw(index, fieldId, new UInt32[][] { rawu32 });
        }

        public void SetListRaw(uint index, uint fieldId, UInt32[][] value)
        {
            if (value.Select(x => x.Length).Distinct().Count() > 1)
            {
                throw new InvalidOperationException($"Cannot set list with different array lengths");
            }
            var field = listFields[fieldId];
            if (value.Length != field.DataFieldIds.Length)
            {
                throw new InvalidOperationException($"Cannot set dat list field 0x{fieldId:x}. Invalid field count. " +
                    $"Received {value.Length}, expected {field.DataFieldIds.Length}");
            }
            if (!NestedArrayEqual(GetListFromData(index, field), value))
            {
                field.ChangedEntries[index] = value;
            }
            else
            {
                field.ChangedEntries.Remove(index);
            }
            FieldChanged?.Invoke(this, new FieldChangedEventArgs(fieldId, index));
        }

        // Enumerable of (firegraft id, offset field id)
        // Firegraft datreqs that weren't saved in dat yet.
        public IEnumerable<(uint, uint)> MissingRequirements()
        {
            return legacyDecl.ListFields
                .Where(x => x.FiregraftId != 0)
                .Where(x => !fields.ContainsKey(x.DataFieldIds[0]))
                .Select(x => (x.FiregraftId, x.OffsetFieldId));
        }

        // This actually is only valid for requirements.. Since it sets format to VariableLengthData
        public void ResetListField(uint fieldId, byte[] offsets, byte[][] data)
        {
            var field = listFields[fieldId];
            field.ChangedEntries.Clear();
            fields[fieldId].Data = new List<byte>(offsets);
            for (int i = 0; i < field.DataFieldIds.Length; i++)
            {
                var dataId = field.DataFieldIds[i];
                fields[dataId] = new DatValue(
                    new List<byte>(data[i]),
                    DatFieldFormat.VariableLengthData,
                    1
                );
            }
        }

        public DatFieldFormat FieldFormat(uint fieldId)
        {
            return fields[fieldId].DataFormat;
        }

        public bool HasField(uint fieldId)
        {
            return fields.ContainsKey(fieldId);
        }

        public void AddZeroField(uint fieldId, DatFieldFormat format)
        {
            AddZeroFieldWithEntryCount(fieldId, format, this.Entries);
        }

        /// Adds zero field that does not necessarily use same entry count as the root file.
        /// Can be used to init list fields too that way.
        private void AddZeroFieldWithEntryCount(uint fieldId, DatFieldFormat format, uint entries)
        {
            var size = format switch
            {
                DatFieldFormat.Uint8 => entries,
                DatFieldFormat.Uint16 => entries * 2,
                DatFieldFormat.Uint32 => entries * 4,
                DatFieldFormat.Uint64 => entries * 8,
                _ => throw new Exception($"Invalid format for zero field ${format}"),
            };
            var data = new List<byte>(new byte[size]);
            AddField(fieldId, format, data);
        }

        public void AddField(uint fieldId, DatFieldFormat format, List<byte> data)
        {
            fields[fieldId] = new DatValue(data, format, 1);
        }

        /// Adds new list field to an existing list.
        ///
        /// Should be only called before the dat has been mutated at all, and the list
        /// must already have existed.
        public void AddListField(uint listId, uint fieldId, DatFieldFormat format, uint defaultValue)
        {
            var listField = listFields[listId];
            var existingField = fields[listField.DataFieldIds[0]];
            var entryCount = existingField.DataFormat switch
            {
                DatFieldFormat.Uint8 => (uint)existingField.Data.Count,
                DatFieldFormat.Uint16 => (uint)existingField.Data.Count / 2,
                DatFieldFormat.Uint32 => (uint)existingField.Data.Count / 4,
                DatFieldFormat.Uint64 => (uint)existingField.Data.Count / 8,
                _ => throw new Exception($"Invalid format in list field"),
            };
            AddZeroFieldWithEntryCount(fieldId, format, entryCount);
            for (uint i = 0; i < entryCount; i++) {
                SetFieldUint(i, fieldId, defaultValue);
            }
        }

        public override bool Equals(object? obj)
        {
            if (obj is DatTable other && Entries == other.Entries)
            {
                if (fields.Count != other.fields.Count)
                {
                    return false;
                }
                // Compare fields
                HashSet<uint> listFieldIds = new HashSet<uint>();
                foreach (var listField in listFields.Values)
                {
                    if (listField.LengthFieldId != null)
                    {
                        listFieldIds.Add((uint)listField.LengthFieldId);
                    }
                    foreach (uint id in listField.DataFieldIds)
                    {
                        listFieldIds.Add(id);
                    }
                }

                foreach (var k in fields.Keys)
                {
                    if (listFieldIds.Contains(k))
                    {
                        continue;
                    }
                    var field = fields[k];
                    if (!other.fields.TryGetValue(k, out DatValue? otherField))
                    {
                        return false;
                    }

                    if (listFields.TryGetValue(k, out var listField))
                    {
                        foreach (var dataFieldId in listField.DataFieldIds)
                        {
                            var data = fields[dataFieldId];
                            if (!other.listFields.TryGetValue(k, out ListFieldState? otherListField))
                            {
                                return false;
                            }
                            if (!other.fields.TryGetValue(dataFieldId, out DatValue? otherData))
                            {
                                return false;
                            }

                            bool quickEq = field == otherField && data == otherData &&
                                listField.ChangedEntries.Count == 0 &&
                                otherListField.ChangedEntries.Count == 0;
                            if (!quickEq)
                            {
                                // Compare list fields one entry at a time
                                for (uint i = 0; i < Entries; i++)
                                {
                                    var ownReqs = GetListRaw(i, k);
                                    var otherReqs = other.GetListRaw(i, k);
                                    if (!NestedArrayEqual(ownReqs, otherReqs))
                                    {
                                        return false;
                                    }
                                }
                            }
                        }
                    }
                    else if (field.DataFormat != DatFieldFormat.VariableLengthData)
                    {
                        // Normal binary array compare
                        if (field != otherField)
                        {
                            return false;
                        }
                    }
                }
                return true;
            }
            return false;
        }

        static bool DictEqualsWith<K, V> (Dictionary<K, V> a, Dictionary<K, V> b, Func<V, V, bool> Compare)
        where K: notnull
        {
            return a.Count == b.Count &&
                a.Keys.All(k => {
                    if (!b.ContainsKey(k))
                    {
                        return false;
                    }
                    var first = a[k];
                    var second = b[k];
                    return (first == null && second == null) ||
                        (first != null && Compare(first, second));
                });
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Entries, fields, listFields);
        }

        public event EventHandler<FieldChangedEventArgs>? FieldChanged;
        public class FieldChangedEventArgs : EventArgs
        {
            public FieldChangedEventArgs(uint field, uint index)
            {
                Field = field;
                Index = index;
            }
            public uint Field { get; set; }
            public uint Index { get; set; }
        }

        public event EventHandler<EventArgs>? EntryCountChanged;

        public uint Entries { get; private set; }
        public UInt16 Version { get; private set; }
        public List<RefField> RefFields { get; private set; }

        Dictionary<uint, DatValue> fields;
        // Key is offset field id
        Dictionary<uint, ListFieldState> listFields;
        uint invalidIndexStart;
        uint invalidIndexCount;
        LegacyDatDecl legacyDecl;

        public static bool operator ==(DatTable? left, DatTable? right)
        {
            return EqualityComparer<DatTable>.Default.Equals(left, right);
        }

        public static bool operator !=(DatTable? left, DatTable? right)
        {
            return !(left == right);
        }

        // Temp struct used during reading the file
        readonly struct FieldDecl
        {
            public FieldDecl(uint id, ushort flags, long offset, int length)
            {
                FieldId = id;
                Offset = offset;
                Length = length;
                Format = (flags & 0x3) switch
                {
                    0 => DatFieldFormat.Uint8,
                    1 => DatFieldFormat.Uint16,
                    2 => DatFieldFormat.Uint32,
                    3 => DatFieldFormat.Uint64,
                    _ => throw new Exception("Unreachable"),
                };
            }

            public long Offset { get; }
            public int Length { get; }
            public uint FieldId { get; }
            public DatFieldFormat Format { get; }
        }

        // State for fields where data is a variable.length list (e.g. requirements)
        // Any mutated lists are stored to ChangedEntries and the actual data is only
        // rebuilt on file save.
        class ListFieldState
        {
            ListFieldState(uint offset, uint[] data)
            {
                OffsetFieldId = offset;
                DataFieldIds = data;
                ChangedEntries = new Dictionary<uint, UInt32[][]>();
                IsRequirement = false;
            }

            public ListFieldState(ListFieldState other)
            {
                OffsetFieldId = other.OffsetFieldId;
                DataFieldIds = (uint[])other.DataFieldIds.Clone();
                ChangedEntries = DictClone(other.ChangedEntries, x => NestedArrayDeepCopy(x));
                LengthFieldId = other.LengthFieldId;
                IsRequirement = other.IsRequirement;
            }

            public static ListFieldState Requirements(uint offset, uint data)
            {
                return new ListFieldState(offset, new uint[] { data })
                {
                    IsRequirement = true
                };
            }

            public static ListFieldState WithLengthField(uint offset, uint[] data, uint length)
            {
                return new ListFieldState(offset, data)
                {
                    LengthFieldId = length
                };
            }

            public uint OffsetFieldId { get; set; }
            public uint[] DataFieldIds { get; set; }
            public uint? LengthFieldId { get; private set; }
            // If true, list is using requirement encoding
            public bool IsRequirement { get; private set; }

            public Dictionary<uint, UInt32[][]> ChangedEntries { get; }

            public override bool Equals(object? obj)
            {
                return obj is ListFieldState state &&
                    OffsetFieldId == state.OffsetFieldId &&
                    DataFieldIds.SequenceEqual(state.DataFieldIds) &&
                    LengthFieldId == state.LengthFieldId &&
                    IsRequirement == state.IsRequirement &&
                    DictEqualsWith(ChangedEntries, state.ChangedEntries, (a, b) => NestedArrayEqual(a, b));
            }

            public override int GetHashCode()
            {
                throw new Exception("no");
            }

            // Length is only used if IsRequirement == false;
            // Requirements are terminated by 0xffff
            public UInt32[] ReadData(uint offset, DatValue data, uint length)
            {
                if (IsRequirement)
                {
                    if (offset == 0)
                    {
                        return Array.Empty<uint>();
                    }
                    if (data.DataFormat != DatFieldFormat.VariableLengthData)
                    {
                        throw new InvalidDataException(
                            $"List field {OffsetFieldId:x}:{DataFieldIds}" +
                            $"had invalid format {data.DataFormat}"
                        );
                    }
                    uint len = RequirementsLength(data.Data, offset);
                    UInt32[] result = new UInt32[(int)len];
                    for (uint i = 0; i < len; i++)
                    {
                        result[(int)i] = (uint)ReadU16(data.Data, offset + i);
                    }
                    return result;
                }
                else
                {
                    UInt32[] result = new UInt32[(int)length];
                    for (uint i = 0; i < length; i++)
                    {
                        result[(int)i] = data.DataFormat switch
                        {
                            DatFieldFormat.Uint8 => (uint)data.Data[(int)(offset + i)],
                            DatFieldFormat.Uint16 => ReadU16(data.Data, offset + i),
                            DatFieldFormat.Uint32 => ReadU32(data.Data, offset + i),
                            _ => throw new ArgumentException($"List values of {data.DataFormat} cannot be read as uint"),
                        };
                    }
                    return result;
                }
            }

            static private uint RequirementsLength(List<byte> data, uint offset)
            {
                uint len = 0;
                bool upgradeLevelJumpSeen = false;
                while (true)
                {
                    var opcode = ReadU16(data, offset + len);
                    if (opcode == 0xffff)
                    {
                        if (!upgradeLevelJumpSeen || (offset + len + 1) * 2 == data.Count)
                        {
                            break;
                        }
                        var next = ReadU16(data, offset + len + 1);
                        if (next != 0xff20 && next != 0xff21)
                        {
                            break;
                        }
                    }
                    if (opcode == 0xff1f)
                    {
                        upgradeLevelJumpSeen = true;
                    }
                    if (opcode == 0xff21)
                    {
                        upgradeLevelJumpSeen = false;
                    }
                    len += 1;
                }
                return len;
            }

            static UInt16 ReadU16(List<byte> list, uint index)
            {
                return BinaryPrimitives.ReadUInt16LittleEndian(
                    new ReadOnlySpan<byte>(list.GetRange((int)index * 2, 2).ToArray())
                );
            }

            static UInt32 ReadU32(List<byte> list, uint index)
            {
                return BinaryPrimitives.ReadUInt32LittleEndian(
                    new ReadOnlySpan<byte>(list.GetRange((int)index * 4, 4).ToArray())
                );
            }
        }
    }

    public struct RefField
    {
        public RefField(ArrayFileType file, uint field, bool zeroIsNone)
        {
            FieldId = field;
            File = file;
            ZeroIsNone = zeroIsNone;
        }

        public uint FieldId { get; }
        public ArrayFileType File { get; }
        public bool ZeroIsNone { get; }
    }

    public struct ListField
    {
        public ListField(uint offset, uint[] data, UInt32 firegraft, uint length)
        {
            OffsetFieldId = offset;
            LengthFieldId = length;
            DataFieldIds = data;
            FiregraftId = firegraft;
        }

        public uint OffsetFieldId { get; }
        public uint[] DataFieldIds { get; }
        public uint FiregraftId { get; }
        public uint LengthFieldId { get; }
    }

    class DatValue
    {
        public DatValue(List<byte> data, DatFieldFormat format, uint subIndexCount)
        {
            Data = data;
            DataFormat = format;
            SubIndexCount = subIndexCount;
        }

        public DatValue(DatValue other)
        {
            Data = new List<byte>(other.Data);
            DataFormat = other.DataFormat;
            SubIndexCount = other.SubIndexCount;
        }

        public List<byte> Data { get; set; }
        public DatFieldFormat DataFormat { get; set; }
        public uint SubIndexCount { get; private set; }

        public override bool Equals(object? obj)
        {
            return obj is DatValue value &&
                   Data.SequenceEqual(value.Data) &&
                   SubIndexCount == value.SubIndexCount &&
                   DataFormat == value.DataFormat;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Data, DataFormat, SubIndexCount);
        }

        public static bool operator ==(DatValue? left, DatValue? right)
        {
            return EqualityComparer<DatValue>.Default.Equals(left, right);
        }

        public static bool operator !=(DatValue? left, DatValue? right)
        {
            return !(left == right);
        }
    }

    enum DatFieldFormat
    {
        Uint8,
        Uint16,
        Uint32,
        Uint64,
        VariableLengthData,
    }
}

// I prefer explicitly specifying size of written types and not just having Write
// be overloaded for all int sizes
namespace Tatti3.GameData.BinaryWriterExt
{
    public static class Ext
    {
        public static void WriteU8(this BinaryWriter output, byte val)
        {
            output.Write(val);
        }

        public static void WriteU16(this BinaryWriter output, UInt16 val)
        {
            output.Write(val);
        }

        public static void WriteU32(this BinaryWriter output, UInt32 val)
        {
            output.Write(val);
        }

        public static void WriteI32(this BinaryWriter output, Int32 val)
        {
            output.Write(val);
        }

        public static void WriteU64(this BinaryWriter output, UInt64 val)
        {
            output.Write(val);
        }
    }
}

namespace Tatti3.GameData.ValueHelpers
{
    public static class Helpers
    {
        public static T[][] NestedArrayDeepCopy<T>(T[][] input)
        where T: notnull
        {
            return input.Select(x => x.Select(y => y).ToArray()).ToArray();
        }

        public static bool NestedArrayEqual<T>(T[][] a, T[][] b)
        where T: notnull
        {
            if (a.Length != b.Length)
            {
                return false;
            }
            return a.Zip(b).All(tp => tp.Item1.SequenceEqual(tp.Item2));
        }
    }
}
