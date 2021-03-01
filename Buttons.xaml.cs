using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Requirement = Tatti3.GameData.Requirement;
using ArrayFileType = Tatti3.GameData.ArrayFileType;

namespace Tatti3
{
    public partial class Buttons : UserControl
    {
        public Buttons()
        {
            InitializeComponent();
            this.DataContextChanged += (o, e) => this.UpdateBinding();
            this.conditionStat.SetList(ConditionList);
            this.conditionParam.SetList(ConditionList);
            this.actionStat.SetList(ActionList);
            this.actionParam.SetList(ActionList);
        }

        void UpdateBinding()
        {
            var state = (AppState)this.DataContext;
            if (state == null)
            {
                return;
            }
            var dat = state.GetDatTableRef(ArrayFileType.Buttons);
            ((StringTableLookupConverter)Resources["StatTxtLookupConverter"]).Table = state.GameData?.StatTxt;
            var view = ((CollectionViewSource)Resources["ButtonView"]);
            SoaView soa = dat.GetListFieldRef(0).Item;
            view.Source = soa;
        }

        void OnAddClick(object sender, RoutedEventArgs e)
        {
            var state = (AppState)this.DataContext;
            if (state == null)
            {
                return;
            }
            var dat = state.GetDatTableRef(ArrayFileType.Buttons);
            var list = dat.GetListFieldRef(0);
            var pos = buttonList.Items.Count;
            uint[] values = { 1, 0, 0, 0, 0, 0, 0, 0 };
            list.Insert(pos, values);
            buttonList.SelectedIndex = pos;
        }

        void OnCopyClick(object sender, RoutedEventArgs e)
        {
            var state = (AppState)this.DataContext;
            if (state == null)
            {
                return;
            }
            var dat = state.GetDatTableRef(ArrayFileType.Buttons);
            var list = dat.GetListFieldRef(0);
            var pos = buttonList.SelectedIndex;
            if (pos == -1 || pos >= buttonList.Items.Count)
            {
                return;
            }
            var values = list.Item[pos].Values;
            list.Insert(pos, values);
            buttonList.SelectedIndex = pos;
        }

        void OnMoveUpClick(object sender, RoutedEventArgs e)
        {
            var pos =  buttonList.SelectedIndex;
            if (pos < 1)
            {
                return;
            }
            if (Swap(pos - 1))
            {
                buttonList.SelectedIndex = pos - 1;
            }
        }

        void OnMoveDownClick(object sender, RoutedEventArgs e)
        {
            var pos = buttonList.SelectedIndex;
            if (pos == -1 || pos == buttonList.Items.Count - 1)
            {
                return;
            }
            if (Swap(pos))
            {
                buttonList.SelectedIndex = pos + 1;
            }
        }

        void OnRemoveClick(object sender, RoutedEventArgs e)
        {
            var state = (AppState)this.DataContext;
            if (state == null)
            {
                return;
            }
            var dat = state.GetDatTableRef(ArrayFileType.Buttons);
            var list = dat.GetListFieldRef(0);
            var pos = buttonList.SelectedIndex;
            if (pos == -1 || pos >= buttonList.Items.Count)
            {
                return;
            }
            list.Remove(pos);
            if (buttonList.Items.Count > pos)
            {
                buttonList.SelectedIndex = pos;
            }
            else if (buttonList.Items.Count > pos - 1)
            {
                buttonList.SelectedIndex = pos - 1;
            }
        }

        bool Swap(int pos)
        {
            var state = (AppState)this.DataContext;
            if (state == null)
            {
                return false;
            }
            var dat = state.GetDatTableRef(ArrayFileType.Buttons);
            var list = dat.GetListFieldRef(0);
            if (pos >= buttonList.Items.Count - 1)
            {
                return false;
            }
            var first = list.Item[pos].Values;
            var second = list.Item[pos + 1].Values;
            list.Item[pos].Values = second;
            list.Item[pos + 1].Values = first;
            return true;
        }

        static Buttons()
        {
            ConditionList = new(new (string, ArrayFileType?)[]{
                ("Slow Down Replay", null),
                ("Speed Up Replay", null),
                ("Play / Pause Replay", null),
                ("Always", null),
                ("Can Create Unit", ArrayFileType.Units),
                ("Can Build Fighter", ArrayFileType.Units),
                ("Mixed Group Condition", null),
                ("Mixed Group Stop", null),
                ("Unit Has a Weapon", null),
                ("Attacking Building", null),
                ("Cancel Last", null),
                ("Cancel Addon", null),
                ("Larva Exists (Upgrading Only)", null),
                ("Rally Point", null),
                ("Rally Point (Upgrading Only)", null),
                ("Construction / Mutation Underway", null),
                ("Upgrade Underway", null),
                ("Research Underway", null),
                ("Probe Harvest", null),
                ("Probe Return Cargo", null),
                ("Transport Capacity Not Met", null),
                ("Carrying Some Units", null),
                ("Tech Not Researched", ArrayFileType.TechData),
                ("Tech Researched", ArrayFileType.TechData),
                ("Has Mines", ArrayFileType.TechData),
                ("Upgrade not at Max Level", ArrayFileType.Upgrades),
                ("Can Cloak", ArrayFileType.TechData),
                ("Can Decloak", null),
                ("Can Cloak Group", ArrayFileType.TechData),
                ("Can Decloak Group", null),
                ("Tank in Tank Mode", null),
                ("Tank in Siege Mode", null),
                ("Tank Move", null),
                ("Nuke Available", null),
                ("Recharge Shields", null),
                ("Building Move", null),
                ("Building Has Lifted Off", null),
                ("Building Has Landed", null),
                ("Burrowing Researched", ArrayFileType.TechData),
                ("Burrowed", ArrayFileType.TechData),
                ("Lurker Morph", null),
                ("Burrower Move", null),
                ("Burrower Attack", null),
                ("Drone Harvest", null),
                ("Drone Return Cargo", null),
                ("Lurker Stop", null),
                ("Nydus Exit", ArrayFileType.Units),
                ("Zerg Basic Buildings", null),
                ("Zerg Advanced Buildings", null),
                ("Carrier Attack", null),
                ("Reaver Attack", null),
                ("Archon Warp Not Researched", null),
                ("Archon Warp Researched", null),
                ("Dark Archon Meld Not Researched", null),
                ("Dark Archon Meld Researched", null),
                ("Protoss Basic Buildings", null),
                ("Protoss Advanced Buildings", null),
                ("SCV Cancel", null),
                ("SCV Move", null),
                ("SCV Stop", null),
                ("SCV Attack", null),
                ("SCV Repair", null),
                ("SCV Harvest", null),
                ("SCV Return Cargo", null),
                ("Terran Basic Buildings", null),
                ("Terran Advanced Buildings", null),
                ("Nuke Train", null),
                ("Upgrade at Max Level", ArrayFileType.Upgrades),
                ("Upgrade at Level 1 or Higher", ArrayFileType.Upgrades),
                ("Upgrade at Level 2 or Higher", ArrayFileType.Upgrades),
                ("Upgrade at Level 3 or Higher", ArrayFileType.Upgrades),
            });
            ActionList = new(new (string, ArrayFileType?)[]{
                ("Cancel Infestation", null),
                ("Rally Point", null),
                ("Select Larva", null),
                ("Create Unit", ArrayFileType.Units),
                ("Cancel Last", null),
                ("Tank Mode", null),
                ("Siege Mode", null),
                ("Cancel Construction", null),
                ("Cancel Morph", null),
                ("Move", null),
                ("Stop", null),
                ("Attack", null),
                ("Suicide Attack", null),
                ("Building Attack", null),
                ("Carrier Move", null),
                ("Carrier Stop", null),
                ("Reaver Stop", null),
                ("Carrier Attack", null),
                ("Reaver Attack", null),
                ("Build Fighter", ArrayFileType.Units),
                ("Patrol", null),
                ("Hold Position", null),
                ("Research Tech", ArrayFileType.TechData),
                ("Cancel Research", null),
                ("Use Tech", ArrayFileType.TechData),
                ("Use Stim Packs", ArrayFileType.TechData),
                ("Upgrade", ArrayFileType.Upgrades),
                ("Cancel Upgrade", null),
                ("Cancel Addon", null),
                ("Terran Build", ArrayFileType.Units),
                ("Flag Beacon COP", null),
                ("Protoss Build", ArrayFileType.Units),
                ("Build Addon", null),
                ("Zerg Build", ArrayFileType.Units),
                ("Nydus Exit", ArrayFileType.Units),
                ("Building Morph", ArrayFileType.Units),
                ("Land", ArrayFileType.Units), // ?
                ("SCV Repair", null),
                ("Unit Morph", ArrayFileType.Units),
                ("Harvest", null),
                ("Return Cargo", null),
                ("Burrow", ArrayFileType.TechData),
                ("Unburrow", null),
                ("Cloak", ArrayFileType.TechData),
                ("Decloak", null),
                ("Lift Off", null),
                ("Load", null),
                ("Unload", null),
                ("Archon Warp", null),
                ("Dark Archon Meld", null),
                ("Recharge Shields", null),
                ("Nuke", null),
                ("Nuke Cancel", null),
                ("Heal", null),
                ("Slow Down Replay", null),
                ("Speed Up Replay", null),
                ("Play / Pause Replay", null),
                ("Cancel", null),
                ("Cancel Building Placement", null),
                ("Change Displayed Buttons", ArrayFileType.Units),
            });
        }

        public static readonly List<(string, ArrayFileType?)> ConditionList;
        public static readonly List<(string, ArrayFileType?)> ActionList;
    }

    class StringTableLookupConverter : IValueConverter
    {
        public StringTableLookupConverter()
        {
        }

        object? IValueConverter.Convert(
            object value,
            Type targetType,
            object parameter,
            System.Globalization.CultureInfo culture
        ) {
            uint index = (uint)value;
            string val = this.Table?.GetByIndex(index) ?? "(None)";
            // Remove hotkey char & magic char if any
            if (val.Length > 2 && val[1] < 0x20)
            {
                return val[2..];
            }
            return val;
        }

        object? IValueConverter.ConvertBack(
            object value,
            Type targetType,
            object parameter,
            System.Globalization.CultureInfo culture
        ) {
            throw new NotSupportedException();
        }

        public Tatti3.GameData.StringTable? Table { get; set; }
    }

    class CreateButtonDatTableRef : IValueConverter
    {
        public CreateButtonDatTableRef()
        {
        }

        object? IValueConverter.Convert(
            object value,
            Type targetType,
            object parameter,
            System.Globalization.CultureInfo culture
        ) {
            if (value == null)
            {
                return null;
            }
            if (ReferenceEquals(value, cachedSoa)) {
                return cached;
            }
            if (cachedSoa != null)
            {
                cachedSoa.CollectionChanged -= CachedCollectionChanged;
            }
            cached = new DatTableRef((SoaStruct)value);
            cachedSoa = (SoaStruct)value;
            cachedSoa.CollectionChanged += CachedCollectionChanged;
            return cached;
        }

        object? IValueConverter.ConvertBack(
            object value,
            Type targetType,
            object parameter,
            System.Globalization.CultureInfo culture
        ) {
            throw new NotSupportedException();
        }

        void CachedCollectionChanged(object? val, NotifyCollectionChangedEventArgs args)
        {
            if (cached != null)
            {
                cached.Update();
            }
        }

        DatTableRef? cached = null;
        SoaStruct? cachedSoa = null;

        // Duck typed to be compatible with AppState.DatTableRef
        public class DatTableRef : AppState.IDatEntryView
        {
            public DatTableRef(SoaStruct data)
            {
                this.data = data;
            }

            public DatTableRef Fields { get => this; }

            public FieldRef this[uint index]
            {
                get
                {
                    return GetFieldRef(index);
                }
                set => throw new InvalidOperationException();
            }

            public FieldRef this[string key]
            {
                get
                {
                    var tokens = key.Split('~');
                    var index = ParseUint(tokens[0]);
                    return GetFieldRef(index);
                }
                set => throw new InvalidOperationException();
            }

            Dictionary<uint, FieldRef> fieldRefs = new Dictionary<uint, FieldRef>();
            FieldRef GetFieldRef(uint index)
            {
                if (!fieldRefs.TryGetValue(index, out FieldRef? val))
                {
                    val = new FieldRef(data, index);
                    fieldRefs[index] = val;
                }
                return val;
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

            SoaStruct data;

            uint AppState.IDatEntryView.GetField(uint field)
            {
                return this[field].Item;
            }

            void AppState.IDatEntryView.SetField(uint field, uint value)
            {
                this[field].Item = value;
            }

            ArrayFileType AppState.IDatEntryView.GetDatType() => ArrayFileType.Buttons;

            public void Update()
            {
                foreach (var fref in fieldRefs.Values)
                {
                    fref.Update();
                }
            }
        }

        // Duck typed to be compatible with AppState.FieldRef
        public class FieldRef : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler? PropertyChanged;

            public FieldRef(SoaStruct data, uint index)
            {
                this.data = data;
                // First index in button list is field 2
                this.index = (int)index - 2;
            }

            public uint Item
            {
                get => data[index];
                set
                {
                    data[index] = value;
                }
            }

            public void Update()
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
            }

            readonly SoaStruct data;
            readonly int index;
        }
    }
}
