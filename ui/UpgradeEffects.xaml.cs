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
    public partial class UpgradeEffects : UserControl
    {
        public UpgradeEffects()
        {
            InitializeComponent();
            this.DataContextChanged += (o, e) => this.UpdateBinding();
            var effectNames = new string[]{
                "Modify Movement Speed",
                "Increase Attack Speed",
                "Modify Weapon Range",
                "Modify Sight Range",
            };
            foreach (var name in effectNames)
            {
                effectStat.Add(name);
            }
            ((EffectNameConverter)Resources["EffectNameConverter"]).Entries = new(effectNames);
        }

        void UpdateBinding()
        {
            var state = (AppState)this.DataContext;
            if (state == null)
            {
                return;
            }
            var dat = state.GetDatTableRef(ArrayFileType.Buttons);
            ((EffectNameConverter)Resources["EffectNameConverter"]).UnitNames =
                state.ArrayFileNames(ArrayFileType.Units);
        }

    }

    class UpgradeValueStat : UserControl, IStatControl
    {
        public UpgradeValueStat()
        {
            datRefStat.Dat = ArrayFileType.Units;
            datRefStat.DropdownWidth = 180;
            intStatControl = ((IStatControl)intStat).Value;
            intStatControl.Visibility = Visibility.Hidden;
            datRefStatControl = ((IStatControl)datRefStat).Value;
            datRefStatControl.Visibility = Visibility.Hidden;
            AddChild(grid);
            grid.Children.Add(intStatControl);
            grid.Children.Add(datRefStatControl);
            this.DataContextChanged += (o, e) => this.UpdateBinding();
            UpdateBinding();
        }

        IntStat intStat = new();
        DatRefStat datRefStat = new();
        FrameworkElement intStatControl;
        FrameworkElement datRefStatControl;
        Grid grid = new();

        void UpdateBinding()
        {
            var state = (AppState.IDatEntryView)this.DataContext;
            if (state == null)
            {
                return;
            }
            var binding = new Binding
            {
                Path = new PropertyPath("Fields[0x16].Item"),
                Mode = BindingMode.OneWay,
            };
            BindingOperations.SetBinding(this, UpgradeValueStat.EffectTypeProperty, binding);
        }

        void EffectUpdated(DependencyPropertyChangedEventArgs args)
        {
            var state = (AppState.IDatEntryView)this.DataContext;
            if (state == null)
            {
                return;
            }
            var effect = state.GetField(0x16);
            if (this.FieldId == 0x1a)
            {
                bool percent = false;
                bool signed = true;
                uint scale = 1;
                var visibility = Visibility.Visible;
                switch (effect)
                {
                    case 0:
                        scale = 1024;
                        intStat.Text = "Value (%)";
                        percent = true;
                        break;
                    case 1:
                        visibility = Visibility.Hidden;
                        intStat.Text = "";
                        break;
                    case 2:
                        intStat.Text = "Value (Pixels)";
                        break;
                    case 3:
                        intStat.Text = "Value (Tiles)";
                        break;
                    default:
                        intStat.Text = "Value";
                        signed = false;
                        break;
                }
                intStat.Visibility = visibility;
                intStat.Scale = scale;
                intStat.Signed = signed;
                intStat.Percent = percent;
            }
            else
            {
                var datVisibility = Visibility.Hidden;
                var intVisibility = Visibility.Hidden;
                string text = "";
                switch (effect)
                {
                    case 0:
                    case 1:
                    case 3:
                        break;
                    case 2:
                        datRefStat.Dat = ArrayFileType.Weapons;
                        datVisibility = Visibility.Visible;
                        text = "Limit to Weapon";
                        break;
                    default:
                        // Allow viewing/editing the value when unknown.
                        text = "Value 2";
                        intVisibility = Visibility.Visible;
                        break;
                }
                // Always using intStat for text control
                intStat.Text = text;
                datRefStat.Visibility = datVisibility;
                intStat.Visibility = intVisibility;
            }
        }

        public static readonly DependencyProperty EffectTypeProperty = DependencyProperty.Register(
            "EffectType",
            typeof(uint),
            typeof(UpgradeValueStat),
            new FrameworkPropertyMetadata(
                (uint)0xffff_ffff,
                FrameworkPropertyMetadataOptions.AffectsRender,
                (o, args) => {
                    ((UpgradeValueStat)o).EffectUpdated(args);
                },
                (d, baseValue) => baseValue,
                false,
                UpdateSourceTrigger.PropertyChanged
            )
        );
        public uint EffectType
        {
            get { return (uint)GetValue(EffectTypeProperty); }
            set
            {
                SetValue(EffectTypeProperty, value);
            }
        }

        public uint FieldId
        {
            get => datRefStat.FieldId;
            set
            {
                intStat.FieldId = value.ToString();
                datRefStat.FieldId = value;
            }
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

        FrameworkElement IStatControl.LabelText
        {
            get { return ((IStatControl)intStat).LabelText; }
        }
        FrameworkElement IStatControl.Value
        {
            get { return this; }
        }
    }

    class EffectNameConverter : IValueConverter
    {
        public EffectNameConverter()
        {
        }

        object? IValueConverter.Convert(
            object value,
            Type targetType,
            object parameter,
            System.Globalization.CultureInfo culture
        ) {
            var arr = (SoaStruct)value;
            int val = (int)arr[0];
            uint minLevel = arr[1];
            uint maxLevel = arr[2];
            int unit = (int)arr[3];
            string effectName;
            if (val < 0 || val >= Entries.Count)
            {
                effectName = $"#0x{val:x}";
            }
            else
            {
                effectName = Entries[val];
            }
            string unitName;
            if (unit < 0 || unit >= UnitNames.Count)
            {
                unitName = $"Unit {unit}";
            }
            else
            {
                unitName = UnitNames[unit];
            }
            if (maxLevel != 255)
            {
                return $"[Lv {minLevel}-{maxLevel}] {unitName}:\n    {effectName}";
            }
            else
            {
                return $"[Lv {minLevel}] {unitName}:\n    {effectName}";
            }
        }

        object? IValueConverter.ConvertBack(
            object value,
            Type targetType,
            object parameter,
            System.Globalization.CultureInfo culture
        ) {
            throw new NotImplementedException();
        }

        public List<string> UnitNames
        {
            get;
            set;
        } = new();
        public List<string> Entries
        {
            get;
            set;
        } = new();
    }
}
