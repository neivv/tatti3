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
            get => datArray;
            set
            {
                datArray = value;
                if (!AppState.IsDatType(datArray))
                {
                    button.Visibility = Visibility.Collapsed;
                }
                else
                {
                    button.Visibility = Visibility.Visible;
                }
            }
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

        public uint Index { get; private set; }

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
            var binding = new Binding
            {
                Path = new PropertyPath(path),
                NotifyOnTargetUpdated = true,
                NotifyOnSourceUpdated = true,
            };
            var binding3 = new Binding
            {
                Path = new PropertyPath(namesPath),
                Mode = BindingMode.OneWay,
                NotifyOnTargetUpdated = true,
            };
            var self = this;
            EventHandler<DataTransferEventArgs> UpdateDropdownIndex = (obj, args) => {
                var ctx = (AppState.DatTableRef)self.DataContext;
                if (ctx == null)
                {
                    return;
                }
                var names = ctx.Root.ArrayFileNames(self.Dat);
                int index = (int)ctx.Fields[self.FieldId].Item;
                dropdown.SelectedIndex = index < names.Count ? index : -1;
            };
            Binding.AddTargetUpdatedHandler(dropdown, UpdateDropdownIndex);
            Binding.AddTargetUpdatedHandler(numeric, UpdateDropdownIndex);
            Binding.AddSourceUpdatedHandler(numeric, UpdateDropdownIndex);

            BindingOperations.SetBinding(numeric, TextBox.TextProperty, binding);
            BindingOperations.SetBinding(dropdown, ComboBox.ItemsSourceProperty, binding3);
        }

        void OnJumpClicked(object sender, RoutedEventArgs e)
        {
            var dat = (AppState.DatTableRef)this.DataContext;
            if (dat == null)
            {
                return;
            }
            MainWindow.JumpCommand.Execute((Dat, dat.Fields[FieldId].Item), this);
        }

        uint field = 0;
        bool inited = false;
        GameData.ArrayFileType datArray;
    }
}
