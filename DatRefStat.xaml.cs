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
    /// Interaction logic for DatRefStat.xaml
    /// </summary>
    public partial class DatRefStat : UserControl, IStatControl
    {
        public DatRefStat()
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
                NotifyOnTargetUpdated = true,
            };
            var binding2 = new Binding
            {
                Path = new PropertyPath(path),
                Converter = new EnumStat.DropdownFilterInvalid<string>(names),
                Mode = BindingMode.OneWay,
            };
            var binding3 = new Binding
            {
                Path = new PropertyPath(namesPath),
                Mode = BindingMode.OneWay,
                NotifyOnTargetUpdated = true,
            };
            Binding.AddTargetUpdatedHandler(dropdown, (obj, args) => {
                dropdown.SelectedIndex = (int)dat.Fields[this.FieldId].Item;
            });
            Binding.AddTargetUpdatedHandler(numeric, (obj, args) => {
                dropdown.SelectedIndex = (int)dat.Fields[this.FieldId].Item;
            });

            BindingOperations.SetBinding(numeric, TextBox.TextProperty, binding);
            BindingOperations.SetBinding(dropdown, ComboBox.SelectedIndexProperty, binding2);
            BindingOperations.SetBinding(dropdown, ComboBox.ItemsSourceProperty, binding3);
        }

        uint field = 0;
        bool inited = false;
    }
}
