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
                    var dat = (AppState.DatTableRef)this.DataContext;
                    if (dat == null)
                    {
                        return;
                    }
                    dat.Fields[FieldId].Item = (uint)selection;
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
            var dat = (AppState.DatTableRef)this.DataContext;
            if (dat == null)
            {
                return;
            }
            var namesPath = $"Root.Dat[{this.Dat}].Names";
            var path = $"Fields[{this.FieldId}].Item";
            AppState root = dat.Root;
            var names = root.ArrayFileNames(this.Dat);
            var binding = new Binding
            {
                Path = new PropertyPath(path),
            };
            var binding2 = new Binding
            {
                Path = new PropertyPath(path),
                Converter = new EnumStat.DropdownFilterInvalid<string>(names),
                Mode = BindingMode.OneWay,
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
            Binding.AddTargetUpdatedHandler(dropdown, (obj, args) => {
                dropdown.SelectedIndex = (int)dat.Fields[this.FieldId].Item;
            });

            BindingOperations.SetBinding(numeric, TextBox.TextProperty, binding);
            BindingOperations.SetBinding(dropdown, ComboBox.SelectedIndexProperty, binding2);
            BindingOperations.SetBinding(dropdown, ComboBox.ItemsSourceProperty, binding3);
        }

        void OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            var target = (ComboBoxItem)e.TargetObject;
            var item = (Item)target.Content;
            // Prevents mouse moving from scrolling the list
            // (From https://stackoverflow.com/questions/29638148/ )
            if (Keyboard.IsKeyDown(Key.Down) || Keyboard.IsKeyDown(Key.Up))
                return;            

            if (((Item)target.Content).Index == ((Item)dropdown.SelectedItem).Index)
                return;

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
