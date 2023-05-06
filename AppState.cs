using System;
using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;

using ArrayFileType = Tatti3.GameData.ArrayFileType;
using Requirement = Tatti3.GameData.Requirement;

namespace Tatti3
{
    class AppState : INotifyPropertyChanged
    {
        // Just to allow syntactic sugar for xaml
        public class RootDatRef
        {
            public RootDatRef(AppState state)
            {
                this.state = state;
            }

            public DatTableRef this[ArrayFileType type]
            {
                get => state.GetDatTableRef(type);
            }

            AppState state;
        }

        // Abstracts over DatTableRef and Button DatTableRef, which both
        // can read/write from a currently selected field.
        public interface IDatEntryView
        {
            public uint GetField(uint field);
            public void SetField(uint field, uint value);
            public ArrayFileType GetDatType();
        }

        // Just to allow syntactic sugar for xaml
        public class DatTableRef : INotifyPropertyChanged, IDatEntryView
        {
            public class FieldsRef
            {
                public FieldsRef(DatTableRef parent)
                {
                    this.parent = parent;
                }

                public FieldRef this[uint index]
                {
                    get
                    {
                        return parent.GetFieldRef(index, 0);
                    }
                    set => throw new InvalidOperationException();
                }

                public FieldRef this[string key]
                {
                    get
                    {
                        var tokens = key.Split('~');
                        var index = ParseUint(tokens[0]);
                        var subIndex = tokens.Length > 1 ? ParseUint(tokens[1]) : 0;
                        return parent.GetFieldRef(index, subIndex);
                    }
                    set => throw new InvalidOperationException();
                }

                static uint ParseUint(string val)
                {
                    if (val.StartsWith("0x"))
                    {
                        return UInt32.Parse(val[2..], NumberStyles.HexNumber);
                    }
                    else
                    {
                        return UInt32.Parse(val);
                    }
                }

                DatTableRef parent;
            }

            public class FieldRef : INotifyPropertyChanged
            {
                public event PropertyChangedEventHandler? PropertyChanged;

                public FieldRef(DatTableRef parent, uint fieldIndex, uint subIndex)
                {
                    this.parent = parent;
                    this.fieldIndex = fieldIndex;
                    this.subIndex = subIndex;
                    this.item = 0;
                    this.currentEntry = -1;
                    UpdateItem();
                }

                public uint Item
                {
                    get => item;
                    set
                    {
                        if (parent.table != null && item != value)
                        {
                            uint entryIndex = (uint)parent.entryIndex;
                            parent.table.SetFieldSubIndexUint(entryIndex, fieldIndex, subIndex, value);
                            item = value;
                        }
                    }
                }

                // Somewhat hacky, but getting gives the entire value and a dummy value,
                // setting must be `(mask, new_value)`
                public (uint, uint) ItemBits
                {
                    get => (item, 0U);
                    set
                    {
                        if (parent.table != null)
                        {
                            var mask = value.Item1;
                            var combined = (mask & value.Item2) | (~mask & item);
                            if (item != combined)
                            {
                                uint entryIndex = (uint)parent.entryIndex;
                                parent.table.SetFieldSubIndexUint(entryIndex, fieldIndex, subIndex, combined);
                                item = combined;
                            }
                        }
                    }
                }

                public void UpdateItem()
                {
                    if (parent.table != null)
                    {
                        int entryIndex = parent.entryIndex;
                        var newItem = parent.table.GetFieldSubIndexUint((uint)entryIndex, fieldIndex, subIndex);
                        if (item != newItem || currentEntry != entryIndex)
                        {
                            item = newItem;
                            currentEntry = entryIndex;
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ItemBits"));
                        }
                    }
                }

                DatTableRef parent;
                uint fieldIndex;
                uint subIndex;
                uint item;
                int currentEntry;
            }

            public class RequirementsRef
            {
                public RequirementsRef(DatTableRef parent, uint offsetFieldId, uint dataFieldId)
                {
                    this.parent = parent;
                    this.offsetFieldId = offsetFieldId;
                    this.dataFieldId = dataFieldId;
                    this.entryIndex = 0;
                    this.Requirements = new RequirementList();
                    this.currentReqs = Array.Empty<Requirement>();
                    UpdateReqs();

                    if (parent.table != null)
                    {
                        parent.table.FieldChanged += (table, args) => {
                            if (
                                ReferenceEquals(table, parent.table) &&
                                (args.Field == this.offsetFieldId || args.Field == this.dataFieldId)
                            )
                            {
                                UpdateReqs();
                            }
                        };
                        if (parent.selectionIndex != -1)
                        {
                            parent.state.PropertyChanged += (obj, args) => {
                                if (obj != parent.state)
                                {
                                    return;
                                }
                                if (args.PropertyName == "Selections")
                                {
                                    var selected = parent.state.Selections[parent.selectionIndex];
                                    if ((uint)selected != entryIndex)
                                    {
                                        UpdateReqs();
                                    }
                                }
                            };
                        }
                    }
                    this.Requirements.Mutated += (obj, args) => {
                        if (parent.table != null)
                        {
                            currentReqs = Requirements.ToArray();
                            parent.table.SetRequirements(entryIndex, offsetFieldId, currentReqs);
                        }
                    };
                    // TODO event for table changed
                }

                void UpdateReqs()
                {
                    if (parent.table == null)
                    {
                        return;
                    }
                    entryIndex = (uint)parent.state.selections[parent.selectionIndex];
                    var reqs = parent.table.GetRequirements(entryIndex, offsetFieldId);
                    if (reqs.SequenceEqual(currentReqs!))
                    {
                        return;
                    }
                    currentReqs = reqs.ToArray();
                    Requirements.Rebuild(add => {
                        foreach (var req in reqs)
                        {
                            add(req);
                        }
                    });
                }

                public RequirementList Requirements { get; }

                Requirement[] currentReqs;
                DatTableRef parent;
                uint offsetFieldId;
                uint dataFieldId;
                uint entryIndex;
            }

            public class ListFieldRef : INotifyPropertyChanged
            {
                public event PropertyChangedEventHandler? PropertyChanged;

                public ListFieldRef(DatTableRef parent, uint fieldIndex)
                {
                    this.parent = parent;
                    this.fieldIndex = fieldIndex;
                    UpdateItem();
                    view.CollectionChanged += (v, args) => {
                        if (ReferenceEquals(v, this.view))
                        {
                            entryIndex = (uint)this.parent.state.selections[this.parent.selectionIndex];
                            if (this.parent.table != null)
                            {
                                this.parent.table.SetListRaw(entryIndex, fieldIndex, view.Arrays);
                            }
                        }
                    };
                    if (parent.table != null)
                    {
                        parent.table.FieldChanged += (table, args) => {
                            if (ReferenceEquals(table, parent.table) && args.Field == this.fieldIndex)
                            {
                                UpdateItem();
                            }
                        };
                        if (parent.selectionIndex != -1)
                        {
                            parent.state.PropertyChanged += (obj, args) => {
                                if (obj != parent.state)
                                {
                                    return;
                                }
                                if (args.PropertyName == "Selections")
                                {
                                    var selected = parent.state.Selections[parent.selectionIndex];
                                    if ((uint)selected != entryIndex)
                                    {
                                        UpdateItem();
                                    }
                                }
                            };
                        }
                    }
                    // TODO event for table changed
                }

                public SoaView Item
                {
                    get => view;
                    set
                    {

                    }
                }

                // Expected to be called when the numeric values of dat may have changed
                void UpdateItem()
                {
                    if (parent.table != null)
                    {
                        entryIndex = (uint)parent.state.selections[parent.selectionIndex];
                        var newItem = parent.table.GetListRaw(entryIndex, fieldIndex);
                        if (!Tatti3.GameData.ValueHelpers.Helpers.NestedArrayEqual(newItem, view.Arrays))
                        {
                            view.Arrays = newItem;
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
                        }
                    }
                }

                public void Insert(int index, uint[] value)
                {
                    if (parent.table != null)
                    {
                        entryIndex = (uint)parent.state.selections[parent.selectionIndex];
                        var oldArr = parent.table.GetListRaw(entryIndex, fieldIndex);
                        var newArr = oldArr.Select((x, i) => {
                            return x.Take(index)
                                .Append(value[i])
                                .Concat(x.Skip(index))
                                .ToArray();
                        }).ToArray();
                        parent.table.SetListRaw(entryIndex, fieldIndex, newArr);
                    }
                }

                public void Remove(int index)
                {
                    if (parent.table != null)
                    {
                        entryIndex = (uint)parent.state.selections[parent.selectionIndex];
                        var oldArr = parent.table.GetListRaw(entryIndex, fieldIndex);
                        var newArr = oldArr.Select(x => {
                            return x.Where((val, i) => i != index).ToArray();
                        }).ToArray();
                        parent.table.SetListRaw(entryIndex, fieldIndex, newArr);
                    }
                }

                DatTableRef parent;
                uint fieldIndex;
                uint entryIndex;
                SoaView view = new SoaView();
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            public DatTableRef(AppState state, ArrayFileType type)
            {
                this.state = state;
                if (AppState.IsDatType(type))
                {
                    table = state.GetDat(type);
                }
                Fields = new FieldsRef(this);
                selectionIndex = AppState.DatFileTypeToIndex(type);
                arrayFileType = type;
                names = null;
                indexPrefixedNames = null;
                state.NamesChanged += (obj, args) => {
                    if (args.Type == arrayFileType)
                    {
                        names = null;
                        indexPrefixedNames = null;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Names"));
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IndexPrefixedNames"));
                    }
                };
                if (table != null)
                {
                    table.FieldChanged += (table, args) => {
                        if (ReferenceEquals(table, this.table) && (int)args.Index == this.entryIndex)
                        {
                            foreach (var pair in fieldRefs)
                            {
                                if (pair.Key.Item1 == args.Field)
                                {
                                    pair.Value.UpdateItem();
                                }
                            }
                        }
                    };
                    if (selectionIndex != -1)
                    {
                        entryIndex = state.Selections[selectionIndex];
                        state.PropertyChanged += (obj, args) => {
                            if (obj != state)
                            {
                                return;
                            }
                            if (args.PropertyName == "Selections")
                            {
                                var selected = state.Selections[selectionIndex];
                                if (selected != this.entryIndex)
                                {
                                    this.entryIndex = selected;
                                    foreach (var val in fieldRefs.Values)
                                    {
                                        val.UpdateItem();
                                    }
                                }
                            }
                        };
                    }
                }
                // TODO event for state changed
            }

            public FieldsRef Fields { get; }
            public List<string> Names
            {
                get
                {
                    if (names == null)
                    {
                        names = state.ArrayFileNames(arrayFileType);
                        if (names == null)
                        {
                            names = new List<string>();
                        }
                    }
                    return names;
                }
            }
            public List<string> IndexPrefixedNames
            {
                get
                {
                    if (indexPrefixedNames == null)
                    {
                        var names = state.IndexPrefixedArrayFileNames(arrayFileType);
                        indexPrefixedNames = new(names.Select(x => x.Text));
                    }
                    return indexPrefixedNames;
                }
            }
            public AppState Root { get => state; }
            Dictionary<(uint, uint), FieldRef> fieldRefs = new Dictionary<(uint, uint), FieldRef>();
            Dictionary<(uint, uint), RequirementsRef> requirementRefs =
                new Dictionary<(uint, uint), RequirementsRef>();
            Dictionary<uint, ListFieldRef> listFieldRefs = new Dictionary<uint, ListFieldRef>();

            FieldRef GetFieldRef(uint index, uint subIndex)
            {
                if (!fieldRefs.TryGetValue((index, subIndex), out FieldRef? val))
                {
                    val = new FieldRef(this, index, subIndex);
                    fieldRefs[(index, subIndex)] = val;
                }
                return val;
            }

            public RequirementsRef GetRequirementsRef(uint offsets, uint data)
            {
                if (!requirementRefs.TryGetValue((offsets, data), out RequirementsRef? val))
                {
                    val = new RequirementsRef(this, offsets, data);
                    requirementRefs[(offsets, data)] = val;
                }
                return val;
            }

            public ListFieldRef GetListFieldRef(uint index)
            {
                if (!listFieldRefs.TryGetValue(index, out ListFieldRef? val))
                {
                    val = new ListFieldRef(this, index);
                    listFieldRefs[index] = val;
                }
                return val;
            }

            uint IDatEntryView.GetField(uint field)
            {
                return GetFieldRef(field, 0).Item;
            }

            void IDatEntryView.SetField(uint field, uint value)
            {
                GetFieldRef(field, 0).Item = value;
            }

            ArrayFileType IDatEntryView.GetDatType() => arrayFileType;

            AppState state;
            GameData.DatTable? table;
            /// Is -1 for non-dat files
            int selectionIndex;
            int entryIndex = 0;
            readonly ArrayFileType arrayFileType;
            List<string>? names;
            List<string>? indexPrefixedNames;
        }

        public AppState(GameData.GameData? gameData)
        {
            selections = new ObservableCollection<int>();
            for (int i = 0; i < 16; i++)
            {
                selections.Add(0);
            }
            Dat = new RootDatRef(this);
            GameData = gameData;
            CmdIcons = new LazyDdsGrp(null, (0.0f, 0.0f, 0.0f));
            GameDataUpdated();
            SelectDat(ArrayFileType.Units);
            selections.CollectionChanged += (obj, args) => {
                var selectionIndex = DatFileTypeToIndex(this.currentDat);
                if (selectionIndex == args.NewStartingIndex)
                {
                    UpdateCurrentBackRef();
                }
            };

            this.BackRefsChanged += (obj, args) => {
                switch (args.Type)
                {
                    case ArrayFileType.Flingy:
                    case ArrayFileType.Sprites:
                    case ArrayFileType.Images:
                    case ArrayFileType.PortData:
                    case ArrayFileType.Buttons:
                    case ArrayFileType.CmdIcon:
                        OnNamesChanged(args.Type);
                        break;
                    default:
                        break;
                }
            };
            this.NamesChanged += (obj, args) => {
                if (refLinkedNames.TryGetValue(args.Type, out List<ArrayFileType>? linked))
                {
                    foreach (var type in linked)
                    {
                        OnNamesChanged(type);
                    }
                }
            };
        }

        public void Save(string path)
        {
            if (GameData != null)
            {
                GameData.Save(path);
                OriginalData = new GameData.GameData(this.GameData);
            }
        }

        public GameData.DatTable? GetDat(ArrayFileType type)
        {
            return type switch
            {
                ArrayFileType.Units => GameData?.Units,
                ArrayFileType.Weapons => GameData?.Weapons,
                ArrayFileType.Flingy => GameData?.Flingy,
                ArrayFileType.Sprites => GameData?.Sprites,
                ArrayFileType.Images => GameData?.Images,
                ArrayFileType.Upgrades => GameData?.Upgrades,
                ArrayFileType.TechData => GameData?.TechData,
                ArrayFileType.PortData => GameData?.PortData,
                ArrayFileType.MapData => GameData?.MapData,
                ArrayFileType.Orders => GameData?.Orders,
                ArrayFileType.Buttons => GameData?.Buttons,
                _ => throw new ArgumentException($"There is no dat table for {type}"),
            };
        }

        public IEnumerable<(ArrayFileType, GameData.DatTable)> IterDats()
        {
            ArrayFileType[] dats =
            {
                ArrayFileType.Units, ArrayFileType.Weapons, ArrayFileType.Flingy,
                ArrayFileType.Sprites, ArrayFileType.Images, ArrayFileType.Upgrades,
                ArrayFileType.TechData, ArrayFileType.Orders, ArrayFileType.PortData,
                ArrayFileType.MapData, ArrayFileType.Buttons,
            };
            foreach (var type in dats)
            {
                var dat = GetDat(type);
                if (dat != null)
                {
                    yield return (type, dat);
                }
            }
        }

        public static bool IsDatType(ArrayFileType type)
        {
            return type switch
            {
                ArrayFileType.Units or ArrayFileType.Weapons or ArrayFileType.Flingy or ArrayFileType.Sprites or 
                    ArrayFileType.Images or ArrayFileType.Upgrades or ArrayFileType.TechData or 
                    ArrayFileType.PortData or ArrayFileType.MapData or ArrayFileType.Orders or
                    ArrayFileType.Buttons => true,
                _ => false,
            };
        }

        static string RemoveHotkeyControlChars(string input)
        {
            // Hotkey strings are formatted as <hotkey_char><hotkey type><string>
            if (input.Length < 2)
            {
                return input;
            }
            if (input[1] < 0x10)
            {
                return input[2..];
            }
            else
            {
                return input;
            }
        }

        void GameDataUpdated()
        {
            OriginalData = this.GameData != null ? new GameData.GameData(this.GameData) : null;
            entryNames.Clear();
            indexPrefixedEntryNames.Clear();
            backRefs.Clear();
            backRefChangeHandlersAdded.Clear();
            datTableRefs.Clear();
            // Name change events
            var weapons = GetDat(ArrayFileType.Weapons);
            if (weapons != null)
            {
                weapons.FieldChanged += (obj, args) => {
                    if (args.Field == WeaponNameField && ReferenceEquals(obj, weapons))
                    {
                        OnNamesChanged(ArrayFileType.Weapons);
                    }
                };
            }
            var orders = GetDat(ArrayFileType.Orders);
            if (orders != null)
            {
                orders.FieldChanged += (obj, args) => {
                    if (args.Field == OrderNameField && ReferenceEquals(obj, orders))
                    {
                        OnNamesChanged(ArrayFileType.Orders);
                    }
                };
            }
            ArrayFileType[] usesBackRefsForNames = {
                ArrayFileType.Flingy,
                ArrayFileType.Sprites,
                ArrayFileType.Images,
                ArrayFileType.PortData,
                ArrayFileType.Buttons,
                ArrayFileType.CmdIcon,
            };
            // When names first array of tuple change,
            // invalidate the second array's names as they
            // contain names derived from first array.
            refLinkedNames.Clear();
            foreach (var type in usesBackRefsForNames)
            {
                foreach ((var otherType, var dat) in IterDats())
                {
                    foreach (var refField in dat.RefFields)
                    {
                        if (refField.File == type)
                        {
                            if (!refLinkedNames.ContainsKey(otherType))
                            {
                                refLinkedNames.Add(otherType, new List<ArrayFileType>());
                            }
                            var list = refLinkedNames[otherType];
                            if (!list.Contains(type))
                            {
                                list.Add(type);
                            }
                        }
                    }
                }
            }

            var upgrades = GetDat(ArrayFileType.Upgrades);
            if (upgrades != null)
            {
                upgrades.FieldChanged += (obj, args) => {
                    if (args.Field == UpgradeNameField && ReferenceEquals(obj, upgrades))
                    {
                        OnNamesChanged(ArrayFileType.Upgrades);
                    }
                };
            }
            var techdata = GetDat(ArrayFileType.TechData);
            if (techdata != null)
            {
                techdata.FieldChanged += (obj, args) => {
                    if (args.Field == TechNameField && ReferenceEquals(obj, techdata))
                    {
                        OnNamesChanged(ArrayFileType.TechData);
                    }
                };
            }
            foreach ((var type, var dat) in IterDats())
            {
                dat.EntryCountChanged += (obj, args) => {
                    OnEntryCountChanged(type);
                };
            }
            CmdIcons = new LazyDdsGrp(GameData?.CmdIcons, (1.0f, 1.0f, 0.0f));
        }

        void OnEntryCountChanged(ArrayFileType type)
        {
            backRefs.Clear();
            OnNamesChanged(type);
        }

        void OnNamesChanged(ArrayFileType type)
        {
            entryNames.Remove(type);
            indexPrefixedEntryNames.Remove(type);
            NamesChanged?.Invoke(this, new NamesChangedEventArgs(type));
            if (type == currentDat)
            {
                CurrentEntryNames = IndexPrefixedArrayFileNames(type);
            }
        }

        // Adds [index_number] in front of the file names so that they can be displayed
        // on the left side entry list
        public List<EntryListData> IndexPrefixedArrayFileNames(ArrayFileType type)
        {
            if (indexPrefixedEntryNames.TryGetValue(type, out List<EntryListData>? result))
            {
                return result;
            }
            var entries = ArrayFileNames(type);
            result = new List<EntryListData>(entries.Count);
            if (entries.Count == 0)
            {
                indexPrefixedEntryNames[type] = result;
                return result;
            }
            var i = 0;
            foreach (var x in entries)
            {
                var data = new EntryListData
                {
                    Text = $"[{i}] {x}",
                    Enabled = true,
                };
                result.Add(data);
                i += 1;
            }
            switch (type)
            {
                case ArrayFileType.Units:
                    for (int j = (int)UnitNoneEntry; j <= (int)UnitLastReserved; j++)
                    {
                        result[j].Enabled = false;
                    }
                    break;
                case ArrayFileType.Weapons:
                    result[(int)WeaponNoneEntry].Enabled = false;
                    break;
                case ArrayFileType.Upgrades:
                    result[(int)UpgradeNoneEntry].Enabled = false;
                    break;
                case ArrayFileType.TechData:
                    result[(int)TechNoneEntry].Enabled = false;
                    break;
                case ArrayFileType.Buttons:
                    result[0].Enabled = false;
                    break;
                default:
                    break;
            }
            indexPrefixedEntryNames[type] = result;
            return result;
        }

        public List<string> ArrayFileNames(ArrayFileType type)
        {
            if (entryNames.TryGetValue(type, out List<string>? entries))
            {
                return entries;
            }
            entries = new List<string>();
            if (IsDatType(type))
            {
                var dat = GetDat(type);
                var statTxt = GameData?.StatTxt;
                if (dat == null || statTxt == null)
                {
                    return new List<string>();
                }
                switch (type)
                {
                    case ArrayFileType.Units:
                        for (uint i = 0; i < dat.Entries; i++)
                        {
                            string[] keys = i switch
                            {
                                0 => new string[] { "FIRST_UNIT_STRING" },
                                227 => new string[] { "LAST_UNIT_STRING" },
                                _ => new string[] { $"FIRST_UNIT_STRING-{i}", $"FIRST_UNIT_STRING_{i}" },
                            };
                            string name = $"(Missing name {keys[0]})";
                            foreach (string key in keys)
                            {
                                var val = statTxt.GetByKey(key);
                                if (val != null)
                                {
                                    name = val;
                                    break;
                                }
                            }
                            entries.Add(name);
                        }
                        while (entries.Count <= (int)UnitLastReserved)
                        {
                            entries.Add($"(Invalid)");
                        }
                        entries[(int)UnitNoneEntry] = "None";
                        //entries[(int)UnitNoneEntry + 1] = "(Trigger) Any unit";
                        //entries[(int)UnitNoneEntry + 2] = "(Trigger) Men";
                        //entries[(int)UnitNoneEntry + 3] = "(Trigger) Buildings";
                        //entries[(int)UnitNoneEntry + 4] = "(Trigger) Factories";
                        entries[(int)UnitNoneEntry + 1] = "(Buttons) Cancel";
                        entries[(int)UnitNoneEntry + 2] = "(Buttons) Cancel Building Placement";
                        entries[(int)UnitNoneEntry + 3] = "(Buttons) Cancel Construction";
                        entries[(int)UnitNoneEntry + 4] = "(Buttons) Cancel Construction + Rally";
                        entries[(int)UnitNoneEntry + 5] = "(Buttons) Cancel Mutation";
                        entries[(int)UnitNoneEntry + 6] = "(Buttons) Cancel Mutation + Rally";
                        entries[(int)UnitNoneEntry + 7] = "(Buttons) Cancel Infestation";
                        entries[(int)UnitNoneEntry + 8] = "(Buttons) Morphing Hatchery";
                        entries[(int)UnitNoneEntry + 9] = "(Buttons) Cancel Nuclear Strike";
                        entries[(int)UnitNoneEntry + 10] = "(Buttons) Basic Zerg Buildings";
                        entries[(int)UnitNoneEntry + 11] = "(Buttons) Basic Terran Buildings";
                        entries[(int)UnitNoneEntry + 12] = "(Buttons) Basic Protoss Buildings";
                        entries[(int)UnitNoneEntry + 13] = "(Buttons) Advanced Zerg Buildings";
                        entries[(int)UnitNoneEntry + 14] = "(Buttons) Advanced Terran Buildings";
                        entries[(int)UnitNoneEntry + 15] = "(Buttons) Advanced Protoss Buildings";
                        entries[(int)UnitNoneEntry + 16] = "(Buttons) Group";
                        entries[(int)UnitNoneEntry + 17] = "(Buttons) Group - Workers";
                        entries[(int)UnitNoneEntry + 18] = "(Buttons) Group - Cloakers";
                        entries[(int)UnitNoneEntry + 19] = "(Buttons) Group - Burrowers";
                        entries[(int)UnitNoneEntry + 20] = "(Buttons) Replay Paused";
                        entries[(int)UnitNoneEntry + 21] = "(Buttons) Replay Playing";
                        for (uint i = UnitNoneEntry + 22; i <= UnitLastReserved; i++) {
                            entries[(int)i] = "(Reserved)";
                        }
                        break;
                    case ArrayFileType.Weapons:
                        for (uint i = 0; i < dat.Entries; i++)
                        {
                            uint label = dat.GetFieldUint(i, WeaponNameField);
                            var name = statTxt.GetByIndex(label) ?? "(Invalid)";
                            name = RemoveHotkeyControlChars(name);
                            entries.Add(name);
                        }
                        while (entries.Count <= (int)WeaponNoneEntry)
                        {
                            entries.Add($"Invalid");
                        }
                        entries[(int)WeaponNoneEntry] = "None";
                        break;
                    case ArrayFileType.Flingy:
                        entries = NamesFromBackRefs(dat.Entries, ArrayFileType.Flingy, i => $"Flingy #{i}");
                        break;
                    case ArrayFileType.Sprites:
                        entries = NamesFromBackRefs(
                            dat.Entries,
                            ArrayFileType.Sprites,
                            i => {
                                uint image = dat.GetFieldUint(i, 0);
                                uint grp = GameData?.Images.GetFieldUint(image, 0) ?? 0;
                                string? path = GameData?.ImagesTbl.GetByIndex(grp);
                                return path ?? $"Sprite #{i}";
                            }
                        );
                        break;
                    case ArrayFileType.Images:
                        entries = NamesFromBackRefs(
                            dat.Entries,
                            ArrayFileType.Images,
                            i => {
                                uint grp = dat.GetFieldUint(i, 0);
                                string? path = GameData?.ImagesTbl.GetByIndex(grp);
                                return path ?? $"Image #{i}";
                            }
                        );
                        break;
                    case ArrayFileType.PortData:
                        entries = NamesFromBackRefs(dat.Entries, ArrayFileType.PortData, i => $"Portrait #{i}");
                        break;
                    case ArrayFileType.MapData:
                        for (uint i = 0; i < dat.Entries; i++)
                        {
                            uint label = dat.GetFieldUint(i, 0);
                            string name;
                            if (label == 0)
                            {
                                name = $"Mission #{i}";
                            }
                            else
                            {
                                name = GameData?.MapDataTbl.GetByIndex(label) ?? "(Invalid)";
                            }
                            entries.Add(name);
                        }
                        break;
                    case ArrayFileType.Buttons:
                        entries = NamesFromBackRefs(dat.Entries, ArrayFileType.Buttons, i => $"Buttonset #{i}");
                        entries[0] = "None";
                        break;
                    case ArrayFileType.Upgrades:
                        for (uint i = 0; i < dat.Entries; i++)
                        {
                            uint label = dat.GetFieldUint(i, UpgradeNameField);
                            string name;
                            if (label == 0)
                            {
                                name = $"Upgrade #{i}";
                            }
                            else
                            {
                                name = statTxt.GetByIndex(label) ?? "(Invalid)";
                            }
                            entries.Add(name);
                        }
                        while (entries.Count <= (int)UpgradeNoneEntry)
                        {
                            entries.Add($"Invalid");
                        }
                        entries[(int)UpgradeNoneEntry] = "None";
                        break;
                    case ArrayFileType.TechData:
                        for (uint i = 0; i < dat.Entries; i++)
                        {
                            uint label = dat.GetFieldUint(i, TechNameField);
                            string name;
                            if (label == 0)
                            {
                                name = $"Tech #{i}";
                            }
                            else
                            {
                                name = statTxt.GetByIndex(label) ?? "(Invalid)";
                            }
                            entries.Add(name);
                        }
                        while (entries.Count <= (int)TechNoneEntry)
                        {
                            entries.Add($"Invalid");
                        }
                        entries[(int)TechNoneEntry] = "None";
                        break;
                    case ArrayFileType.Orders:
                        for (uint i = 0; i < dat.Entries; i++)
                        {
                            uint label = dat.GetFieldUint(i, OrderNameField);
                            var name = statTxt.GetByIndex(label) ?? "(Invalid)";
                            name = RemoveHotkeyControlChars(name);
                            entries.Add(name);
                        }
                        break;
                    default:
                        break;
                }
            }
            else
            {
                switch (type)
                {
                    case ArrayFileType.StatTxt:
                    {
                        var statTxt = GameData?.StatTxt;
                        if (statTxt != null)
                        {
                            entries = statTxt.ListByIndex();
                            for (int i = 0; i < entries.Count; i++)
                            {
                                // Remove hotkey char & magic char if any
                                if (entries[i].Length > 2 && entries[i][1] < 0x20)
                                {
                                    entries[i] = entries[i][2..];
                                }
                            }
                        }
                        break;
                    }
                    case ArrayFileType.StatTxtRank:
                    {
                        var statTxt = GameData?.StatTxt;
                        if (statTxt != null)
                        {
                            entries = statTxt.ListByIndex();
                            entries.RemoveRange(0, 1302);
                            entries[0] = "None";
                            if (entries.Count > 256)
                            {
                                entries.RemoveRange(256, entries.Count - 256);
                            }
                        }
                        break;
                    }
                    case ArrayFileType.ImagesTbl:
                    {
                        var tbl = GameData?.ImagesTbl;
                        if (tbl != null)
                        {
                            entries = tbl.ListByIndex();
                        }
                        break;
                    }
                    case ArrayFileType.PortDataTbl:
                    {
                        var tbl = GameData?.PortDataTbl;
                        if (tbl != null)
                        {
                            entries = tbl.ListByIndex();
                        }
                        break;
                    }
                    case ArrayFileType.MapDataTbl:
                    {
                        var tbl = GameData?.MapDataTbl;
                        if (tbl != null)
                        {
                            entries = tbl.ListByIndex();
                        }
                        break;
                    }
                    case ArrayFileType.CmdIcon:
                        if (GameData != null)
                        {
                            var limit = (uint)GameData.CmdIcons.Count;
                            entries = NamesFromBackRefs(limit, ArrayFileType.CmdIcon, i => $"Icon #{i}");
                        }
                        break;
                    case ArrayFileType.SfxData:
                    {
                        var sfx = GameData?.Sfx;
                        if (sfx != null)
                        {
                            entries = sfx.Names;
                        }
                        break;
                    }
                    default:
                        break;
                }
            }
            entryNames[type] = entries;
            return entries;
        }

        List<string> NamesFromBackRefs(
            uint entryCount,
            ArrayFileType type,
            Func<uint, string> defaultName)
        {
            var entries = new List<string>();
            var backRefList = BackRefs(type);
            var namesAdded = new HashSet<string>();
            for (uint datIdx = 0; datIdx < entryCount; datIdx++)
            {
                if (backRefList.Count > (int)datIdx)
                {
                    var backRefs = backRefList[(int)datIdx].Set;
                    if (backRefs.Count == 0)
                    {
                        entries.Add(defaultName(datIdx));
                    }
                    else
                    {
                        namesAdded.Clear();
                        var result = $"";
                        foreach ((var backType, var backIndex) in backRefs)
                        {
                            const int limit = 3;
                            string name;
                            if (namesAdded.Count >= limit)
                            {
                                name = "...";
                            }
                            else
                            {
                                var names = ArrayFileNames(backType);
                                if (names.Count > (int)backIndex)
                                {
                                    name = names[(int)backIndex];
                                    // Hackfix: Add (Construction) for unit construction images
                                    if (backType == ArrayFileType.Units && type == ArrayFileType.Images)
                                    {
                                        name = $"{name} (Construction)";
                                    }
                                }
                                else
                                {
                                    name = "???";
                                }
                            }
                            if (namesAdded.Count < limit && namesAdded.Contains(name))
                            {
                                continue;
                            }
                            result = namesAdded.Count == 0 ? name : $"{result}, {name}";
                            if (namesAdded.Count >= limit)
                            {
                                break;
                            }
                            namesAdded.Add(name);
                        }
                        entries.Add(result);
                    }
                }
                else
                {
                    entries.Add(defaultName(datIdx));
                }
            }
            System.Diagnostics.Trace.Assert(entries.Count == (int)entryCount);
            return entries;
        }

        List<BackRef> BackRefs(ArrayFileType type)
        {
            if (backRefs.TryGetValue(type, out List<BackRef>? entries))
            {
                return entries;
            }
            entries = new List<BackRef>();
            uint? limit;
            if (IsDatType(type))
            {
                limit = GetDat(type)?.Entries;
            }
            else
            {
                limit = type switch
                {
                    ArrayFileType.CmdIcon => (uint?)GameData?.CmdIcons.Count,
                    _ => null,
                };
            }
            if (limit != null)
            {
                for (int i = 0; i < limit; i++)
                {
                    entries.Add(new BackRef());
                }
                foreach ((var otherType, var dat) in IterDats())
                {
                    foreach (var refField in dat.RefFields)
                    {
                        if (refField.File == type)
                        {
                            uint field = refField.FieldId;
                            for (uint i = 0; i < dat.Entries; i++)
                            {
                                // Show invalid indices for buttons since they are
                                // also (Buttons) xxx etc
                                if (!dat.IsInvalidIndex(i) || type == ArrayFileType.Buttons)
                                {
                                    if (dat.IsListField(field))
                                    {
                                        var list = dat.GetListRaw(i, field);
                                        // Assuming [0] is the relevant one expect for upgrade
                                        // effects.
                                        if (field == 0x14 && otherType == ArrayFileType.Upgrades)
                                        {
                                            if (type == ArrayFileType.Units)
                                            {
                                                foreach (uint fieldVal in list[3])
                                                {
                                                    AddToBackRefs(entries, fieldVal, (otherType, i), refField);
                                                }
                                            }
                                            else if (type == ArrayFileType.Weapons)
                                            {
                                                var effects = list[0];
                                                var value2 = list[5];
                                                for (int j = 0; j < effects.Length; j++)
                                                {
                                                    if (effects[j] == 2 && value2[j] != WeaponNoneEntry)
                                                    {
                                                        AddToBackRefs(entries, value2[j], (otherType, i), refField);
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            foreach (uint fieldVal in list[0])
                                            {
                                                AddToBackRefs(entries, fieldVal, (otherType, i), refField);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Not list field, more straightforward.
                                        var fieldVal = dat.GetFieldUint(i, field);
                                        AddToBackRefs(entries, fieldVal, (otherType, i), refField);
                                    }
                                }
                            }
                            if (!backRefChangeHandlersAdded.Contains(type))
                            {
                                EventHandler<GameData.DatTable.FieldChangedEventArgs> OnChange = (obj, args) => {
                                    var dat = this.GetDat(otherType);
                                    if (args.Field == field && ReferenceEquals(dat, obj))
                                    {
                                        this.backRefs.Remove(type);
                                        this.BackRefsChanged?.Invoke(this, new BackRefsChangedEventArgs(type));
                                        if (this.currentDat == otherType)
                                        {
                                            this.UpdateCurrentBackRef();
                                        }
                                    }
                                };
                                dat.FieldChanged += OnChange;
                            }
                        }
                    }
                }
            }
            backRefChangeHandlersAdded.Add(type);
            backRefs[type] = entries;
            return entries;
        }

        // Helper for BackRefs() building
        void AddToBackRefs(
            List<BackRef> entries,
            uint fieldVal,
            (ArrayFileType, uint) tuple,
            GameData.RefField refField
        ) {
            if (fieldVal < (uint)entries.Count)
            {
                if (!refField.ZeroIsNone || fieldVal != 0)
                {
                    entries[(int)fieldVal].Set.Add(tuple);
                }
            }
        }

        // Expected to be called whenever the active tab is being changed
        public void SelectDat(ArrayFileType type)
        {
            currentDat = type;
            CurrentEntryNames = IndexPrefixedArrayFileNames(type);
            UpdateCurrentBackRef();
        }

        void UpdateCurrentBackRef()
        {
            var selectionIndex = DatFileTypeToIndex(currentDat);
            var datIndex = Selections[selectionIndex];
            var refs = BackRefs(currentDat);
            if (datIndex < refs.Count)
            {
                CurrentBackRefs = refs[datIndex];
            }
            else
            {
                CurrentBackRefs = new BackRef();
            }
        }

        public static int DatFileTypeToIndex(ArrayFileType type)
        {
            return type switch
            {
                ArrayFileType.Units => 0,
                ArrayFileType.Weapons => 1,
                ArrayFileType.Flingy => 2,
                ArrayFileType.Sprites => 3,
                ArrayFileType.Images => 4,
                ArrayFileType.Upgrades => 5,
                ArrayFileType.TechData => 6,
                ArrayFileType.PortData => 7,
                ArrayFileType.MapData => 8,
                ArrayFileType.Orders => 9,
                ArrayFileType.Buttons => 10,
                _ => -1
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<NamesChangedEventArgs>? NamesChanged;
        public event EventHandler<BackRefsChangedEventArgs>? BackRefsChanged;
        public class NamesChangedEventArgs : EventArgs
        {
            public NamesChangedEventArgs(ArrayFileType type)
            {
                Type = type;
            }
            public ArrayFileType Type { get; set; }
        }
        public class BackRefsChangedEventArgs : EventArgs
        {
            public BackRefsChangedEventArgs(ArrayFileType type)
            {
                Type = type;
            }
            public ArrayFileType Type { get; set; }
        }

        public RootDatRef Dat { get; }

        public DatTableRef GetDatTableRef(ArrayFileType type)
        {
            if (!datTableRefs.TryGetValue(type, out DatTableRef? val))
            {
                val = new DatTableRef(this, type);
                datTableRefs[type] = val;
            }
            return val;
        }

        List<EntryListData> currentEntryNames = new List<EntryListData>();
        public List<EntryListData> CurrentEntryNames
        {
            get => currentEntryNames;
            set
            {
                currentEntryNames = value;
                NotifyPropertyChanged();
            }
        }

        BackRef currentBackRefs = new BackRef();
        public BackRef CurrentBackRefs
        {
            get => currentBackRefs;
            set
            {
                currentBackRefs = value;
                NotifyPropertyChanged();
            }
        }

        ObservableCollection<int> selections;
        public ObservableCollection<int> Selections {
            get => selections;
            set
            {
                selections = value;
                NotifyPropertyChanged();
            }
        }

        ArrayFileType currentDat;
        public ArrayFileType CurrentDat
        {
            get => currentDat;
            set
            {
                currentDat = value;
                NotifyPropertyChanged();
            }
        }

        public LazyDdsGrp CmdIcons { get; private set; }

        void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        Dictionary<ArrayFileType, List<string>> entryNames = new Dictionary<ArrayFileType, List<String>>();
        Dictionary<ArrayFileType, List<EntryListData>> indexPrefixedEntryNames =
            new Dictionary<ArrayFileType, List<EntryListData>>();
        Dictionary<ArrayFileType, List<BackRef>> backRefs = new Dictionary<ArrayFileType, List<BackRef>>();
        Dictionary<ArrayFileType, DatTableRef> datTableRefs = new Dictionary<ArrayFileType, DatTableRef>();
        HashSet<ArrayFileType> backRefChangeHandlersAdded = new HashSet<ArrayFileType>();
        Dictionary<ArrayFileType, List<ArrayFileType>> refLinkedNames = new();

        public class EntryListData
        {
            public EntryListData()
            {
                Enabled = true;
                Text = "";
            }
            public bool Enabled { get; set; }
            public string Text { get; set; }
        }

        public bool IsDirty { get => GameData != OriginalData; }

        public GameData.GameData? GameData;
        // For checking if the data has been changed
        GameData.GameData? OriginalData;
        const uint UnitNoneEntry = 228;
        // This is higher than what actually is needed just in case a patch adds more buttons..
        const uint UnitLastReserved = 260;
        const uint WeaponNameField = 0x0;
        const uint WeaponNoneEntry = 130;
        const uint UpgradeNoneEntry = 61;
        const uint TechNoneEntry = 44;
        const uint UpgradeNameField = 0x8;
        const uint TechNameField = 0x7;
        const uint OrderNameField = 0x0;
    }
}
