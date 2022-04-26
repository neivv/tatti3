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

using ArrayFileType = Tatti3.GameData.ArrayFileType;

namespace Tatti3
{
    // Mostly same as DatRefStat, but changes referred dat depending on EnumFieldId,
    // also requires List<(string, ArrayFileType?)> button condition/action array to
    // be set to know what dat it should refer to
    public partial class ButtonParamStat : UserControl, IStatControl
    {
        public ButtonParamStat()
        {
            label = new TextBlock();
            InitializeComponent();
            Dat = null;
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
                        dat.SetField(ParamFieldId, (uint)selection);
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
        public ArrayFileType? Dat
        {
            get => datArray;
            set
            {
                datArray = value;
                if (datArray == null || !AppState.IsDatType((ArrayFileType)datArray))
                {
                    button.IsEnabled = false;
                }
                else
                {
                    button.IsEnabled = true;
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

        public uint EnumFieldId
        {
            get => enumField;
            set
            {
                enumField = value;
                UpdateBinding();
            }
        }
        public uint ParamFieldId
        {
            get => field;
            set
            {
                field = value;
                UpdateBinding();
            }
        }

        public void SetList(List<(string, ArrayFileType?)> list)
        {
            this.list = list;
        }

        public uint Index { get; private set; }

        void UpdateBinding()
        {
            var ctx = (object[])this.DataContext;
            if (ctx == null)
            {
                return;
            }
            AppState root = (AppState)ctx[1];
            if (!inited)
            {
                if (ctx[0] is AppState.IDatEntryView datView)
                {
                    var dat = root.GetDat(datView.GetDatType());
                    if (dat == null)
                    {
                        return;
                    }
                    dat.FieldChanged += (o, args) => {
                        // NOTE: Could check args.Field or args.Index,
                        // but field would be 0 since buttons are lists
                        this.UpdateDatType();
                    };
                }
                else
                {
                    return;
                }
                inited = true;
            }
            var path = $"[0].Fields[{this.ParamFieldId}].Item";
            var binding = new Binding
            {
                Path = new PropertyPath(path),
                NotifyOnTargetUpdated = true,
                NotifyOnSourceUpdated = true,
            };
            Binding? binding3 = null;
            var datType = this.Dat;
            if (datType != null)
            {
                var namesPath = $"[1].Dat[{datType}].Names";
                binding3 = new Binding
                {
                    Path = new PropertyPath(namesPath),
                    Mode = BindingMode.OneWay,
                    NotifyOnTargetUpdated = true,
                };
            }
            var self = this;
            Binding.AddTargetUpdatedHandler(dropdown, UpdateDropdownIndex);
            Binding.AddTargetUpdatedHandler(numeric, UpdateDropdownIndex);
            Binding.AddSourceUpdatedHandler(numeric, UpdateDropdownIndex);

            BindingOperations.SetBinding(numeric, TextBox.TextProperty, binding);
            if (binding3 != null)
            {
                BindingOperations.SetBinding(dropdown, ComboBox.ItemsSourceProperty, binding3);
            }
            else
            {
                BindingOperations.ClearBinding(dropdown, ComboBox.ItemsSourceProperty);
            }
            UpdateDatType();
        }

        // Called when enum field may have changed what dat type should be shown in dropdown
        // for param.
        void UpdateDatType()
        {
            var ctx = (object[])this.DataContext;
            if (ctx == null)
            {
                return;
            }
            if (ctx[0] is AppState.IDatEntryView dat)
            {
                var index = dat.GetField(EnumFieldId);
                var newDat = list.Count > index ? list[(int)index].Item2 : null;
                if (Dat != newDat)
                {
                    Dat = newDat;
                    UpdateDropdownIndex();
                }
            }
        }

        void UpdateDropdownIndex(object? obj, DataTransferEventArgs args)
        {
            UpdateDropdownIndex();
        }

        void UpdateDropdownIndex()
        {
            var ctx = (object[])this.DataContext;
            if (ctx == null)
            {
                return;
            }
            var root = (AppState)ctx[1];
            var datType = this.Dat;
            var nameCount = datType != null ?
                root.ArrayFileNames((ArrayFileType)datType).Count :
                0;
            int index = -1;
            if (ctx[0] is AppState.IDatEntryView dat)
            {
                index = (int)dat.GetField(this.ParamFieldId);
            }
            dropdown.SelectedIndex = index < nameCount ? index : -1;
        }

        void OnJumpClicked(object sender, RoutedEventArgs e)
        {
            var ctx = (object[])this.DataContext;
            var datType = Dat;
            if (ctx == null || datType == null)
            {
                return;
            }
            var dat = (AppState.IDatEntryView)ctx[0];
            MainWindow.JumpCommand.Execute(((ArrayFileType)datType, dat.GetField(ParamFieldId)), this);
        }

        uint field = 0;
        uint enumField = 0;
        bool inited = false;
        ArrayFileType? datArray;
        List<(string, ArrayFileType?)> list = new();
    }
}
