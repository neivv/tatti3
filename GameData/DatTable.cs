using System;
using System.Buffers.Binary;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Tatti3.GameData.BinaryWriterExt;

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

                self.fields.Add(index, new DatValue(data, field.format));
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
            //  u16 minor_version (1)
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
            var major = reader.ReadUInt16();
            var minor = reader.ReadUInt16();
            if (major != 1 || minor == 0)
            {
                throw new InvalidDataException($"Invalid dat file version {major:02x}:{minor:02x}");
            }
            if (minor > 1)
            {
                throw new InvalidDataException($"The dat appears to be saved with a newer version of this program");
            }
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
                self.fields.Add(field.FieldId, new DatValue(new List<byte>(bytes), field.Format));
            }
            InitListFields(self, decl);
            return self;
        }

        static private void InitListFields(DatTable self, LegacyDatDecl decl)
        {
            foreach (var listField in decl.ListFields)
            {
                if (self.fields.ContainsKey(listField.DataFieldId))
                {
                    self.fields[listField.DataFieldId].DataFormat = DatFieldFormat.VariableLengthData;
                }
                self.listFields[listField.OffsetFieldId] = new
                    ListFieldState(listField.OffsetFieldId, listField.DataFieldId);
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
                    var dataMem = new MemoryStream();
                    var offsetsMem = new MemoryStream();
                    var data = new BinaryWriter(dataMem);
                    var offsets = new BinaryWriter(offsetsMem);
                    data.WriteU16(0);
                    for (uint i = 0; i < Entries; i++)
                    {
                        var reqs = GetRequirementsRaw(i, field.OffsetFieldId);
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
                                data.WriteU16(op);
                            }
                            data.WriteU16(0xffff);
                        }
                    }
                    data.WriteU16(0xffff);
                    ResetListField(field.OffsetFieldId, offsetsMem.ToArray(), dataMem.ToArray());
                }
            }

            using (var writer = new BinaryWriter(output, Encoding.UTF8, true))
            {
                writer.WriteU16(1);
                writer.WriteU16(1);
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
            if (index >= Entries)
            {
                throw new ArgumentOutOfRangeException($"Index {index} is greater than maximum index {Entries}");
            }
            var field = fields[fieldId];
            return field.DataFormat switch
            {
                DatFieldFormat.Uint8 => field.Data[(int)index],
                DatFieldFormat.Uint16 => ReadU16(field.Data, index),
                DatFieldFormat.Uint32 => ReadU32(field.Data, index),
                _ => throw new ArgumentException($"Dat field 0x{index:x} cannot be read as uint"),
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
            if (index >= Entries)
            {
                throw new ArgumentOutOfRangeException();
            }
            var field = fields[fieldId];
            switch (field.DataFormat)
            {
                case DatFieldFormat.Uint8:
                    field.Data[(int)index] = (byte)value;
                    break;
                case DatFieldFormat.Uint16:
                    WriteU16(field.Data, index, value);
                    break;
                case DatFieldFormat.Uint32:
                    WriteU32(field.Data, index, value);
                    break;
                case DatFieldFormat.Uint64:
                    WriteU64(field.Data, index, value);
                    break;
                default:
                    throw new ArgumentException($"Dat field 0x{index:x} cannot be written as uint");
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
            var listFieldIds = new HashSet<uint>();
            foreach (var listField in listFields.Values)
            {
                listFieldIds.Add(listField.DataFieldId);
                listFieldIds.Add(listField.OffsetFieldId);
                var opcodes = GetRequirementsRaw(sourceIndex, listField.OffsetFieldId);
                var field = fields[listField.OffsetFieldId];
                AppendToField(field, 0);
                SetRequirementsRaw(destIndex, listField.OffsetFieldId, opcodes);
            }
            foreach (var pair in fields)
            {
                var fieldId = pair.Key;
                var field = pair.Value;
                if (listFieldIds.Contains(fieldId))
                {
                    // Lists were handled above
                    continue;
                }
                var old = GetFieldUint(sourceIndex, fieldId);
                AppendToField(field, old);
            }
            EntryCountChanged?.Invoke(this, new EventArgs());
        }

        // Duplicate helper.
        // Adds the field value for the new entry.
        private static void AppendToField(DatValue field, uint value)
        {
            int len = 0;
            switch (field.DataFormat)
            {
                case DatFieldFormat.Uint8:
                    len = 1;
                    break;
                case DatFieldFormat.Uint16:
                    len = 2;
                    break;
                case DatFieldFormat.Uint32:
                    len = 4;
                    break;
                case DatFieldFormat.Uint64:
                    len = 8;
                    break;
                default:
                    throw new NotImplementedException();
            }
            for (int i = 0; i < len; i++)
            {
                field.Data.Add((byte)value);
                value = value >> 8;
            }
        }

        public List<Requirement> GetRequirements(uint index, uint fieldId)
        {
            var raw = GetRequirementsRaw(index, fieldId);
            return Requirement.ListFromRaw(raw);
        }

        private UInt16[] GetRequirementsRaw(uint index, uint fieldId)
        {
            var field = listFields[fieldId];
            if (field.ChangedEntries.ContainsKey(index))
            {
                return ValueArrayDeepCopy(field.ChangedEntries[index]);
            }
            else
            {
                return GetRequirementsFromData(index, field);
            }
        }

        // Reads requirements from .dat, skipping the mutation store layer
        private UInt16[] GetRequirementsFromData(uint index, ListFieldState field)
        {
            var offset = GetFieldUint(index, field.OffsetFieldId);
            if (offset == 0)
            {
                return new UInt16[]{};
            }
            var data = fields[field.DataFieldId];
            if (data.DataFormat != DatFieldFormat.VariableLengthData)
            {
                throw new InvalidDataException(
                    $"List field {field.OffsetFieldId:x}:{field.DataFieldId}" +
                    $"had invalid format {data.DataFormat.ToString()}"
                );
            }
            uint len = RequirementsLength(data.Data, offset);
            UInt16[] result = new UInt16[(int)len];
            for (uint i = 0; i < len; i++)
            {
                result[(int)i] = ReadU16(data.Data, offset + i);
            }
            return result;
        }

        private uint RequirementsLength(List<byte> data, uint offset)
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

        public void SetRequirements(uint index, uint fieldId, Requirement[] value)
        {
            if (index >= Entries || index > 0xffff)
            {
                throw new ArgumentOutOfRangeException();
            }
            var raw = Requirement.ToRaw(value);
            SetRequirementsRaw(index, fieldId, raw);
            FieldChanged?.Invoke(this, new FieldChangedEventArgs(fieldId, index));
        }

        private void SetRequirementsRaw(uint index, uint fieldId, UInt16[] value)
        {
            var field = listFields[fieldId];
            if (!GetRequirementsFromData(index, field).SequenceEqual(value))
            {
                field.ChangedEntries[index] = value;
            }
            else
            {
                field.ChangedEntries.Remove(index);
            }
        }

        // Enumerable of (firegraft id, offset field id)
        // Firegraft datreqs that weren't saved in dat yet.
        public IEnumerable<(uint, uint)> MissingRequirements()
        {
            return legacyDecl.ListFields
                .Where(x => !fields.ContainsKey(x.DataFieldId))
                .Select(x => (x.FiregraftId, x.OffsetFieldId));
        }

        public void ResetListField(uint fieldId, byte[] offsets, byte[] data)
        {
            var field = listFields[fieldId];
            field.ChangedEntries.Clear();
            fields[fieldId].Data = new List<byte>(offsets);
            fields[field.DataFieldId] = new DatValue(
                new List<byte>(data),
                DatFieldFormat.VariableLengthData
            );
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
                foreach (var k in fields.Keys)
                {
                    var field = fields[k];
                    DatValue? otherField;
                    if (!other.fields.TryGetValue(k, out otherField))
                    {
                        return false;
                    }

                    if (listFields.TryGetValue(k, out var listField))
                    {
                        var data = fields[listField.DataFieldId];
                        DatValue? otherData;
                        ListFieldState? otherListField;
                        if (!other.listFields.TryGetValue(k, out otherListField))
                        {
                            return false;
                        }
                        if (!other.fields.TryGetValue(listField.DataFieldId, out otherData))
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
                                var ownReqs = GetRequirementsRaw(i, k);
                                var otherReqs = other.GetRequirementsRaw(i, k);
                                if (!ownReqs.SequenceEqual(otherReqs))
                                {
                                    return false;
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

        static bool DictEquals<K, V> (Dictionary<K, V> a, Dictionary<K, V> b)
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
                    return (first == null && second == null) || (first != null && first.Equals(second));
                });
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
                switch (flags & 0x3)
                {
                    case 0:
                        Format = DatFieldFormat.Uint8;
                        break;
                    case 1:
                        Format = DatFieldFormat.Uint16;
                        break;
                    case 2:
                        Format = DatFieldFormat.Uint32;
                        break;
                    case 3:
                        Format = DatFieldFormat.Uint64;
                        break;
                    default:
                        throw new Exception("Unreachable");
                }
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
            public ListFieldState(uint offset, uint data)
            {
                OffsetFieldId = offset;
                DataFieldId = data;
                ChangedEntries = new Dictionary<uint, UInt16[]>();
            }

            public ListFieldState(ListFieldState other)
            {
                OffsetFieldId = other.OffsetFieldId;
                DataFieldId = other.DataFieldId;
                ChangedEntries = DictClone(other.ChangedEntries, x => x.Select(y => y).ToArray());
            }

            public uint OffsetFieldId { get; set; }
            public uint DataFieldId { get; set; }
            public Dictionary<uint, UInt16[]> ChangedEntries { get; }

            public override bool Equals(object? obj)
            {
                return obj is ListFieldState state &&
                    OffsetFieldId == state.OffsetFieldId &&
                    DataFieldId == state.DataFieldId &&
                    DictEqualsWith(ChangedEntries, state.ChangedEntries, (a, b) => a.SequenceEqual(b));
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(OffsetFieldId, DataFieldId, ChangedEntries);
            }
        }

        static T[] ValueArrayDeepCopy<T>(T[] input)
        where T: notnull
        {
            return input.Select(x => x).ToArray();
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
        public ListField(uint offset, uint data, UInt32 firegraft)
        {
            OffsetFieldId = offset;
            DataFieldId = data;
            FiregraftId = firegraft;
        }

        public uint OffsetFieldId { get; }
        public uint DataFieldId { get; }
        public uint FiregraftId { get; }
    }

    class DatValue
    {
        public DatValue(List<byte> data, DatFieldFormat format)
        {
            Data = data;
            DataFormat = format;
        }

        public DatValue(DatValue other)
        {
            Data = new List<byte>(other.Data);
            DataFormat = other.DataFormat;
        }

        public List<byte> Data { get; set; }
        public DatFieldFormat DataFormat { get; set; }

        public override bool Equals(object? obj)
        {
            return obj is DatValue value &&
                   Data.SequenceEqual(value.Data) &&
                   DataFormat == value.DataFormat;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Data, DataFormat);
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
    }
}
