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
