using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
using FieldRef = Tatti3.AppState.DatTableRef.FieldRef;

namespace Tatti3
{
    public class ListFieldEdit : UserControl
    {
        public ListFieldEdit()
        {
            this.DataContextChanged += (o, e) => this.UpdateBinding();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var dock = new DockPanel();
            buttonList.MinWidth = 150.0;
            buttonList.MinHeight = 200.0;
            var itemBinding = new Binding
            {
                Source = datListView,
            };
            BindingOperations.SetBinding(buttonList, ListBox.ItemsSourceProperty, itemBinding);
            dock.Children.Add(buttonList);
            var buttons = new StackPanel
            {
                Orientation = Orientation.Vertical,
            };
            var childBinding = new Binding
            {
                Source = datListView,
                Path = new PropertyPath("/"),
                Converter = tableRef,
            };
            BindingOperations.SetBinding(childPanel, FrameworkElement.DataContextProperty, childBinding);
            var buttonDecls = new (RoutedEventHandler, string)[] {
                (OnAddClick, "Add"),
                (OnCopyClick, "Copy"),
                (OnMoveUpClick, "Move up"),
                (OnMoveDownClick, "Move down"),
                (OnRemoveClick, "Remove"),
            };
            foreach (var (act, label) in buttonDecls)
            {
                var button = new Button();
                button.Content = new TextBlock
                {
                    Text = label,
                    Margin = new Thickness(2.0, 0.0, 2.0, 0.0),
                };
                button.Click += act;
                buttons.Children.Add(button);
            }
            Grid.SetColumn(dock, 0);
            Grid.SetColumn(buttons, 1);
            Grid.SetColumn(childPanel, 2);
            grid.Children.Add(dock);
            grid.Children.Add(buttons);
            grid.Children.Add(childPanel);
            AddChild(grid);
        }

        CreateListDatTableRef tableRef = new();
        CollectionViewSource datListView = new();
        Grid grid = new();
        ListBox buttonList = new();
        StackPanel childPanel = new();

        public FrameworkElement EditControl
        {
            set
            {
                childPanel.Children.Add(value);
            }
        }

        public DataTemplate ListBoxTemplate
        {
            set
            {
                buttonList.ItemTemplate = value;
            }
        }

        public uint[] DefaultValues
        {
            get;
            set;
        } = new uint[] {};

        public uint ListFieldId
        {
            get;
            set;
        } = 0;

        public ArrayFileType Dat
        {
            get => this.tableRef.Dat;
            set => this.tableRef.Dat = value;
        }

        public int FirstArrayField
        {
            set => this.tableRef.FirstArrayField = value;
        }

        public float ListHeight
        {
            set
            {
                this.buttonList.MinHeight = value;
                this.buttonList.MaxHeight = value;
            }
        }

        void UpdateBinding()
        {
            var state = (AppState)this.DataContext;
            if (state == null || state.GameData == null)
            {
                return;
            }
            var dat = state.GetDatTableRef(Dat);
            SoaView soa = dat.GetListFieldRef(ListFieldId).Item;
            if (soa.Arrays.Length != DefaultValues.Length)
            {
                throw new Exception($"Invalid ListFieldEdit DefaultValues size: {DefaultValues.Length}, expected {soa.Arrays.Length}");
            }
            this.datListView.Source = soa;
        }

        void OnAddClick(object sender, RoutedEventArgs e)
        {
            var state = (AppState)this.DataContext;
            if (state == null)
            {
                return;
            }
            var dat = state.GetDatTableRef(Dat);
            var list = dat.GetListFieldRef(ListFieldId);
            var pos = buttonList.Items.Count;
            list.Insert(pos, this.DefaultValues);
            buttonList.SelectedIndex = pos;
        }

        void OnCopyClick(object sender, RoutedEventArgs e)
        {
            var state = (AppState)this.DataContext;
            if (state == null)
            {
                return;
            }
            var dat = state.GetDatTableRef(Dat);
            var list = dat.GetListFieldRef(ListFieldId);
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
            var dat = state.GetDatTableRef(Dat);
            var list = dat.GetListFieldRef(ListFieldId);
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
            var dat = state.GetDatTableRef(Dat);
            var list = dat.GetListFieldRef(ListFieldId);
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

    class CreateListDatTableRef : IValueConverter
    {
        public CreateListDatTableRef()
        {
        }

        public ArrayFileType Dat
        {
            get;
            set;
        }

        public int FirstArrayField
        {
            get;
            set;
        } = -1;

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
            if (FirstArrayField < 0)
            {
                throw new Exception("FirstArrayField wasn't set");
            }
            cached = new DatTableRef((SoaStruct)value, Dat, (uint)FirstArrayField);
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
            public DatTableRef(SoaStruct data, ArrayFileType dat, uint baseIndex)
            {
                this.data = data;
                this.baseIndex = baseIndex;
                this.dat = dat;
            }

            public DatTableRef Fields { get => this; }
            uint baseIndex;
            ArrayFileType dat;

            public AppState.DatTableRef.FieldRef this[uint index]
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
                    val = new AppState.DatTableRef.FieldRef(data, index - baseIndex);
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

            ArrayFileType AppState.IDatEntryView.GetDatType() => this.dat;

            public void Update()
            {
                foreach (var fref in fieldRefs.Values)
                {
                    fref.UpdateItem();
                }
            }
        }
    }
}
