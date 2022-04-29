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
                    var ctx = (object[])this.DataContext;
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

        public double DropdownWidth
        {
            set
            {
                dropdown.Width = value;
            }
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
                UpdateBinding();
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
            var ctx = (object[])this.DataContext;
            if (ctx == null)
            {
                return;
            }
            var namesPath = $"[1].Dat[{this.Dat}].Names";
            var path = $"[0].Fields[{this.FieldId}].Item";
            AppState root = (AppState)ctx[1];
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
                var ctx = (object[])this.DataContext;
                if (ctx == null)
                {
                    return;
                }
                var root = (AppState)ctx[1];
                var names = root.ArrayFileNames(self.Dat);
                int index = -1;
                if (ctx[0] is AppState.IDatEntryView dat)
                {
                    index = (int)dat.GetField(self.FieldId);
                }
                dropdown.SelectedIndex = index < names.Count && index > 0 ? index : -1;
            };
            Binding.AddTargetUpdatedHandler(dropdown, UpdateDropdownIndex);
            Binding.AddTargetUpdatedHandler(numeric, UpdateDropdownIndex);
            Binding.AddSourceUpdatedHandler(numeric, UpdateDropdownIndex);

            BindingOperations.SetBinding(numeric, TextBox.TextProperty, binding);
            BindingOperations.SetBinding(dropdown, ComboBox.ItemsSourceProperty, binding3);
        }

        void OnJumpClicked(object sender, RoutedEventArgs e)
        {
            var ctx = (object[])this.DataContext;
            if (ctx == null)
            {
                return;
            }
            var dat = (AppState.IDatEntryView)ctx[0];
            MainWindow.JumpCommand.Execute((Dat, dat.GetField(FieldId)), this);
        }

        uint field = 0;
        bool inited = false;
        GameData.ArrayFileType datArray;
    }
}
