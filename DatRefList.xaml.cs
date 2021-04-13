using System;
using System.Collections.Generic;
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

namespace Tatti3
{
    public partial class DatRefList : UserControl
    {
        public DatRefList()
        {
            InitializeComponent();
            this.DataContextChanged += (o, e) => this.UpdateBinding();
        }

        public GameData.ArrayFileType Dat
        {
            get => datArray;
            set
            {
                datArray = value;
                UpdateBinding();
            }
        }

        public uint OffsetFieldId
        {
            get => offsetField;
            set
            {
                offsetField = value;
                UpdateBinding();
            }
        }

        public uint Index { get; private set; }

        void UpdateBinding()
        {
            var ctx = (object[])this.DataContext;
            if (ctx == null)
            {
                return;
            }
            var dat = (AppState.DatTableRef)ctx[0];
            var root = (AppState)ctx[1];
            var namesPath = $"[1].Dat[{this.Dat}].IndexPrefixedNames";
            var dropdownNameBinding = new Binding
            {
                Path = new PropertyPath(namesPath),
                Mode = BindingMode.OneWay,
                NotifyOnTargetUpdated = true,
            };
            datRefList.DataContext = dat.GetListFieldRef(offsetField).Item;
            var binding = new Binding();
            BindingOperations.SetBinding(datRefList, ListBox.ItemsSourceProperty, binding);
            BindingOperations.SetBinding(dropdown, ComboBox.ItemsSourceProperty, dropdownNameBinding);

            var refDat = root.Dat[this.Dat];
            ((ListIndexConverter)Resources["ListIndexConverter"]).List = refDat.IndexPrefixedNames;
            root.NamesChanged += (o, args) => {
                var ctx = (object[])this.DataContext;
                var root = (AppState)ctx[1];
                if (ReferenceEquals(root, o) && args.Type == this.Dat) {
                    var refDat = root.Dat[this.Dat];
                    ((ListIndexConverter)Resources["ListIndexConverter"]).List = refDat.IndexPrefixedNames;
                }
            };
        }

        void OnAddClick(object sender, RoutedEventArgs e)
        {
            var ctx = (object[])this.DataContext;
            var entry = dropdown.SelectedIndex;
            if (ctx == null || entry < 0)
            {
                return;
            }
            var dat = (AppState.DatTableRef)ctx[0];
            var soa = dat.GetListFieldRef(offsetField).Item;
            var vals = new List<uint>(soa.Arrays[0]);
            if (!vals.Contains((uint)entry)) {
                vals.Add((uint)entry);
                vals.Sort();
                soa.Arrays = new uint[][] { vals.ToArray() };
            }
        }

        void OnRemoveClick(object sender, RoutedEventArgs e)
        {
            var ctx = (object[])this.DataContext;
            var index = datRefList.SelectedIndex;
            if (ctx == null || index < 0)
            {
                return;
            }
            var dat = (AppState.DatTableRef)ctx[0];
            var soa = dat.GetListFieldRef(offsetField).Item;
            var vals = new List<uint>(soa.Arrays[0]);
            vals.RemoveAt(index);
            soa.Arrays = new uint[][] { vals.ToArray() };
        }

        uint offsetField = 0;
        GameData.ArrayFileType datArray;
    }

    class ListIndexConverter : IValueConverter
    {
        public ListIndexConverter()
        {
            List = new List<string>();
        }

        object? IValueConverter.Convert(
            object value,
            Type targetType,
            object parameter,
            System.Globalization.CultureInfo culture
        ) {
            int index = (int)(uint)value;
            return this.List.Count > (int)index ? this.List[index] : $"#{index}";
        }

        object? IValueConverter.ConvertBack(
            object value,
            Type targetType,
            object parameter,
            System.Globalization.CultureInfo culture
        ) {
            throw new NotSupportedException();
        }

        public List<string> List { get; set; }
    }
}
