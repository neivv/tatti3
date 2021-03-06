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
    /// <summary>
    /// Interaction logic for IconStat.xaml
    /// </summary>
    public partial class IconStat : UserControl, IStatControl
    {
        public IconStat()
        {
            label = new TextBlock();
            InitializeComponent();
            InputMethod.SetPreferredImeState(numeric, InputMethodState.Off);
            this.DataContextChanged += (o, e) => this.UpdateBinding();
            this.dropdown.SelectionChanged += (o, e) => {
                var selection = this.dropdown.SelectedIndex;
                if (selection != -1 && this.inited)
                {
                    object[] ctx = (object[])this.DataContext;
                    if (ctx == null)
                    {
                        return;
                    }
                    if (ctx[0] is AppState.IDatEntryView dat)
                    {
                        dat.SetField(FieldId, (uint)selection);
                    }
                }
            };
        }

        private TextBlock label;
        public string Text
        {
            set
            {
                label.Text = value;
            }
        }
        public GameData.ArrayFileType Dat
        {
            get; set;
        }
        FrameworkElement IStatControl.LabelText
        {
            get { return label; }
        }
        FrameworkElement IStatControl.Value
        {
            get { return this; }
        }
        double IStatControl.Height() { return 38.0; }

        public uint FieldId
        {
            get => field;
            set
            {
                field = value;
                inited = true;
                UpdateBinding();
            }
        }

        void UpdateBinding()
        {
            object[] ctx = (object[])this.DataContext;
            if (ctx == null)
            {
                return;
            }
            var root = (AppState)ctx[1];
            var namesPath = $"[1].Dat[{this.Dat}].Names";
            var path = $"[0].Fields[{this.FieldId}].Item";
            var binding = new Binding
            {
                Path = new PropertyPath(path),
                NotifyOnTargetUpdated = true,
                NotifyOnSourceUpdated = true,
            };
            var binding3 = new MultiBinding()
            {
                Converter = new ItemsConverter(root),
                Mode = BindingMode.OneWay,
                NotifyOnTargetUpdated = true,
            };
            binding3.Bindings.Add(new Binding {
                Path = new PropertyPath(path),
            });
            binding3.Bindings.Add(new Binding {
                Path = new PropertyPath(namesPath),
            });
            var self = this;
            EventHandler<DataTransferEventArgs> UpdateDropdownIndex = (obj, args) => {
                object[] ctx = (object[])self.DataContext;
                if (ctx == null)
                {
                    return;
                }
                var root = (AppState)ctx[1];
                var names = root.ArrayFileNames(self.Dat);
                int index = -1;
                // This isn't ideal but DatTableRef is so hacky it's not nice to refactor :l
                // And would also not prefer actually setting on using an interface; it
                // should have been just designed better.
                if (ctx[0] is AppState.IDatEntryView dat)
                {
                    index = (int)dat.GetField(self.FieldId);
                }
                dropdown.SelectedIndex = index < names.Count ? index : -1;
            };
            Binding.AddTargetUpdatedHandler(dropdown, UpdateDropdownIndex);
            Binding.AddTargetUpdatedHandler(numeric, UpdateDropdownIndex);
            Binding.AddSourceUpdatedHandler(numeric, UpdateDropdownIndex);

            BindingOperations.SetBinding(numeric, TextBox.TextProperty, binding);
            BindingOperations.SetBinding(dropdown, ComboBox.ItemsSourceProperty, binding3);
        }

        void OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            var target = (ComboBoxItem)e.TargetObject;
            // Prevents mouse moving from scrolling the list
            // (From https://stackoverflow.com/questions/29638148/ )
            if (Keyboard.IsKeyDown(Key.Down) || Keyboard.IsKeyDown(Key.Up))
            {
                return;
            }
            if (dropdown.SelectedItem == null)
            {
                return;
            }
            if (((Item)target.Content).Index == ((Item)dropdown.SelectedItem).Index)
            {
                return;
            }

            e.Handled = true;
        }

        uint field = 0;
        bool inited = false;

        readonly struct Item
        {
            public Item(string name, AppState state, int index)
            {
                this.Name = name;
                this.index = index;
                this.state = state;
            }

            readonly AppState state;
            readonly int index;
            public string Name { get; }
            public int Index { get => index; }
            public BitmapSource? Image
            {
                get => state.CmdIcons.Image(index);
            }
        }

        // Converts (IconId, IconNameList) => Item,
        // converting Icon id to image source.
        // TODO Actually IMultiValueConverter was unnecessary.
        // Though the icon id being a binding dependency triggers updates to
        // dropdown whenever a selected entry is changed, refactoring this
        // IMultiValueConverter away need that implemented in some other way.
        class ItemsConverter : IMultiValueConverter
        {
            public ItemsConverter(AppState root)
            {
                this.root = root;
            }

            AppState root;

            object? IMultiValueConverter.Convert(
                object[] values,
                Type targetType,
                object parameter,
                System.Globalization.CultureInfo culture
            ) {
                // Probably relatively wasteful to build a new large list each time,
                // but too lazy to make some cheaply constructable list-like class.
                // names. Count is still just ~300
                var names = (List<string>)values[1];
                var list = new List<Item>(names.Count);
                for (int i = 0; i < names.Count; i++)
                {
                    list.Add(new Item(names[i], this.root, i));
                }
                return list;
            }

            object[] IMultiValueConverter.ConvertBack(
                object value,
                Type[] targetTypes,
                object parameter,
                System.Globalization.CultureInfo culture
            ) {
                throw new NotSupportedException();
            }
        }
    }
}
