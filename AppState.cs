using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        // Just to allow syntactic sugar for xaml
        public class DatTableRef : INotifyPropertyChanged
        {
            public class FieldsRef
            {
                public FieldsRef(DatTableRef parent)
                {
                    this.parent = parent;
                }

                public FieldRef this[uint index]
                {
                    get => parent.GetFieldRef(index);
                    set => throw new InvalidOperationException();
                }

                DatTableRef parent;
            }

            public class FieldRef : INotifyPropertyChanged
            {
                public event PropertyChangedEventHandler? PropertyChanged;

                public FieldRef(DatTableRef parent, uint fieldIndex)
                {
                    this.parent = parent;
                    this.fieldIndex = fieldIndex;
                    this.item = 0;
                    this.entryIndex = 0;
                    UpdateItem();
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

                public uint Item
                {
                    get => item;
                    set
                    {
                        if (parent.table != null && item != value)
                        {
                            parent.table.SetFieldUint(entryIndex, fieldIndex, value);
                            item = value;
                        }
                    }
                }

                // Somewhat hacky, but getting gives the entire value and a dummy bool,
                // setting must be either `(mask, true)` to set, or `(mask, false)` to clear
                public (uint, bool) ItemBits
                {
                    get => (item, true);
                    set
                    {
                        if (parent.table != null)
                        {
                            var bit = value.Item1;
                            var combined = value.Item2 ? (bit | item) : (~bit & item);
                            if (item != combined)
                            {
                                parent.table.SetFieldUint(entryIndex, fieldIndex, combined);
                                item = combined;
                            }
                        }
                    }
                }

                // Expected to be called when the numeric values of dat may have changed
                void UpdateItem()
                {
                    if (parent.table != null)
                    {
                        entryIndex = (uint)parent.state.selections[parent.selectionIndex];
                        var newItem = parent.table.GetFieldUint(entryIndex, fieldIndex);
                        if (newItem != item)
                        {
                            item = newItem;
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ItemBits"));
                        }
                    }
                }

                DatTableRef parent;
                uint fieldIndex;
                uint item;
                uint entryIndex;
            }

            public class RequirementsRef
            {
                public RequirementsRef(DatTableRef parent, uint offsetFieldId, uint dataFieldId)
                {
                    this.parent = parent;
                    this.offsetFieldId = offsetFieldId;
                    this.dataFieldId = dataFieldId;
                    this.entryIndex = 0;
                    this.Requirements = new ObservableCollection<Requirement>();
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
                    // TODO event for table changed
                }

                void UpdateReqs()
                {
                    Requirements.Clear();
                    if (parent.table != null)
                    {
                        entryIndex = (uint)parent.state.selections[parent.selectionIndex];
                        var reqs = parent.table.GetRequirements(entryIndex, offsetFieldId);
                        foreach (var req in reqs)
                        {
                            Requirements.Add(req);
                        }
                    }
                }

                public ObservableCollection<Requirement> Requirements { get; }

                DatTableRef parent;
                uint offsetFieldId;
                uint dataFieldId;
                uint entryIndex;
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
                state.NamesChanged += (obj, args) => {
                    if (args.Type == arrayFileType)
                    {
                        names = null;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Names"));
                    }
                };
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
            public AppState Root { get => state; }
            Dictionary<uint, FieldRef> fieldRefs = new Dictionary<uint, FieldRef>();
            Dictionary<(uint, uint), RequirementsRef> requirementRefs =
                new Dictionary<(uint, uint), RequirementsRef>();

            FieldRef GetFieldRef(uint index)
            {
                FieldRef? val;
                if (!fieldRefs.TryGetValue(index, out val))
                {
                    val = new FieldRef(this, index);
                    fieldRefs[index] = val;
                }
                return val;
            }

            public RequirementsRef GetRequirementsRef(uint offsets, uint data)
            {
                RequirementsRef? val;
                if (!requirementRefs.TryGetValue((offsets, data), out val))
                {
                    val = new RequirementsRef(this, offsets, data);
                    requirementRefs[(offsets, data)] = val;
                }
                return val;
            }

            AppState state;
            GameData.DatTable? table;
            /// Is -1 for non-dat files
            int selectionIndex;
            ArrayFileType arrayFileType;
            List<string>? names;
        }

        public AppState(GameData.GameData? gameData)
        {
            selections = new ObservableCollection<int>();
            for (int i = 0; i < 8; i++)
            {
                selections.Add(0);
            }
            Dat = new RootDatRef(this);
            GameData = gameData;
            CmdIcons = new LazyDdsGrp(null);
            GameDataUpdated();
            SelectDat(ArrayFileType.Units);
            selections.CollectionChanged += (obj, args) => {
                var selectionIndex = DatFileTypeToIndex(this.currentDat);
                if (selectionIndex == args.NewStartingIndex)
                {
                    UpdateCurrentBackRef();
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
                _ => throw new ArgumentException($"There is no dat table for {type.ToString()}"),
            };
        }

        public IEnumerable<(ArrayFileType, GameData.DatTable)> IterDats()
        {
            ArrayFileType[] dats =
            {
                ArrayFileType.Units, ArrayFileType.Weapons, ArrayFileType.Flingy,
                ArrayFileType.Sprites, ArrayFileType.Images, ArrayFileType.Upgrades,
                ArrayFileType.TechData,
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
            switch (type)
            {
                case ArrayFileType.Units:
                case ArrayFileType.Weapons:
                case ArrayFileType.Flingy:
                case ArrayFileType.Sprites:
                case ArrayFileType.Images:
                case ArrayFileType.Upgrades:
                case ArrayFileType.TechData:
                    return true;
                default:
                    return false;
            }
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
                return input.Substring(2);
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
            this.BackRefsChanged += (obj, args) => {
                switch (args.Type)
                {
                    case ArrayFileType.Flingy:
                    case ArrayFileType.Sprites:
                    case ArrayFileType.Images:
                    case ArrayFileType.CmdIcon:
                        OnNamesChanged(args.Type);
                        break;
                    default:
                        break;
                }
            };
            ArrayFileType[] usesBackRefsForNames = {
                ArrayFileType.Flingy,
                ArrayFileType.Sprites,
                ArrayFileType.Images,
                ArrayFileType.CmdIcon,
            };
            // When names first array of tuple change,
            // invalidate the second array's names as they
            // contain names derived from first array.
            var refLinkedNames = new Dictionary<ArrayFileType, List<ArrayFileType>>();
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
            this.NamesChanged += (obj, args) => {
                if (refLinkedNames.TryGetValue(args.Type, out List<ArrayFileType>? linked))
                {
                    foreach (var type in linked)
                    {
                        OnNamesChanged(type);
                    }
                }
            };

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
            CmdIcons = new LazyDdsGrp(GameData?.CmdIcons);
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
            List<EntryListData>? result;
            if (indexPrefixedEntryNames.TryGetValue(type, out result))
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
                    for (int j = (int)UnitNoneEntry; j <= (int)UnitFactoriesEntry; j++)
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
                default:
                    break;
            }
            indexPrefixedEntryNames[type] = result;
            return result;
        }

        public List<string> ArrayFileNames(ArrayFileType type)
        {
            List<string>? entries;
            if (entryNames.TryGetValue(type, out entries))
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
                            var name = statTxt.GetByIndex(i + 1) ?? $"(Invalid)";
                            entries.Add(name);
                        }
                        while (entries.Count <= (int)UnitFactoriesEntry)
                        {
                            entries.Add($"Invalid");
                        }
                        entries[(int)UnitNoneEntry] = "None";
                        entries[(int)UnitNoneEntry + 1] = "(Trigger) Any unit";
                        entries[(int)UnitNoneEntry + 2] = "(Trigger) Men";
                        entries[(int)UnitNoneEntry + 3] = "(Trigger) Buildings";
                        entries[(int)UnitNoneEntry + 4] = "(Trigger) Factories";
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
                        entries = NamesFromBackRefs(dat.Entries, ArrayFileType.Sprites, i => $"Sprite #{i}");
                        break;
                    case ArrayFileType.Images:
                        entries = NamesFromBackRefs(dat.Entries, ArrayFileType.Images, i => $"Image #{i}");
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
                    default:
                        break;
                }
            }
            else
            {
                switch (type)
                {
                    case ArrayFileType.StatTxt:
                        var statTxt = GameData?.StatTxt;
                        if (statTxt != null)
                        {
                            entries = statTxt.ListByIndex();
                        }
                        break;
                    case ArrayFileType.CmdIcon:
                        if (GameData != null)
                        {
                            var limit = (uint)GameData.CmdIcons.Count;
                            entries = NamesFromBackRefs(limit, ArrayFileType.CmdIcon, i => $"Icon #{i}");
                        }
                        break;
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
            List<BackRef>? entries;
            if (backRefs.TryGetValue(type, out entries))
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
                switch (type)
                {
                    case ArrayFileType.CmdIcon:
                        limit = (uint?)GameData?.CmdIcons.Count;
                        break;
                    default:
                        limit = null;
                        break;
                }
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
                                if (!dat.IsInvalidIndex(i))
                                {
                                    var fieldVal = dat.GetFieldUint(i, field);
                                    if (fieldVal < (uint)entries.Count)
                                    {
                                        if (!refField.ZeroIsNone || fieldVal != 0)
                                        {
                                            var tuple = (otherType, i);
                                            entries[(int)fieldVal].Set.Add(tuple);
                                        }
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

        Dictionary<ArrayFileType, DatTableRef> datTableRefs = new Dictionary<ArrayFileType, DatTableRef>();

        DatTableRef GetDatTableRef(ArrayFileType type)
        {
            DatTableRef? val;
            if (!datTableRefs.TryGetValue(type, out val))
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
        HashSet<ArrayFileType> backRefChangeHandlersAdded = new HashSet<ArrayFileType>();

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
        const uint UnitFactoriesEntry = 232;
        const uint WeaponNameField = 0x0;
        const uint WeaponNoneEntry = 130;
        const uint UpgradeNoneEntry = 61;
        const uint TechNoneEntry = 44;
        const uint UpgradeNameField = 0x8;
        const uint TechNameField = 0x7;
    }
}
