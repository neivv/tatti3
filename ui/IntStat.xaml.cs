using System;
using System.Collections.Generic;
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

namespace Tatti3
{
    /// <summary>
    /// Interaction logic for IntStat.xaml
    /// </summary>
    public partial class IntStat : UserControl, IStatControl
    {
        [ValueConversion(typeof(uint), typeof(double))]
        class ScaleConverter : IValueConverter
        {
            public ScaleConverter(uint scale, bool percent, bool signed)
            {
                this.scale = scale;
                this.percent = percent;
                this.signed = signed;
            }

            object? IValueConverter.Convert(
                object value,
                Type targetType,
                object parameter,
                System.Globalization.CultureInfo culture
            ) {
                double scaled = signed ?
                    (double)(int)value / (double)scale :
                    (double)(uint)value / (double)scale;
                scaled = percent ? scaled * 100.0f : scaled;
                return percent ?
                    String.Format(CultureInfo.InvariantCulture, "{0:F1}", scaled) :
                    String.Format(CultureInfo.InvariantCulture, "{0:G}", scaled);
            }

            object? IValueConverter.ConvertBack(
                object value,
                Type targetType,
                object parameter,
                System.Globalization.CultureInfo culture
            ) {
                try
                {
                    var format = System.Globalization.NumberFormatInfo.InvariantInfo;
                    double val = Single.Parse((string)value, format);
                    if (percent)
                    {
                        val = val / 100.0f;
                    }
                    return signed ?
                        (int)(val * (double)scale) :
                        (uint)(val * (double)scale);
                }
                catch
                {
                    return DependencyProperty.UnsetValue;
                }
            }

            uint scale;
            bool percent;
            bool signed;
        }

        public IntStat()
        {
            label = new TextBlock();
            InitializeComponent();
            InputMethod.SetPreferredImeState(entry, InputMethodState.Off);
        }

        private TextBlock label;
        public string Text
        {
            set
            {
                label.Text = value;
            }
        }

        public string FieldId
        {
            set
            {
                var tokens = value.Split('~');
                Field = ParseUint(tokens[0]);
                SubIndex = tokens.Length > 1 ? ParseUint(tokens[1]) : 0;
                UpdateBinding();
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

        public uint Scale
        {
            get => scale;
            set
            {
                scale = value;
                UpdateBinding();
            }
        }

        public bool Percent
        {
            get => percent;
            set
            {
                percent = value;
                UpdateBinding();
            }
        }

        public bool Signed
        {
            get => signed;
            set
            {
                signed = value;
                UpdateBinding();
            }
        }

        public object ToolTipText
        {
            set
            {
                label.ToolTip = value;
                this.ToolTip = value;
            }
        }

        void UpdateBinding()
        {
            var itemName = Signed ? "ItemSigned" : "Item";
            var path = $"Fields[{Field}~{SubIndex}].{itemName}";
            IValueConverter? converter = null;
            if (Scale != 1 || Percent || Signed)
            {
                converter = new ScaleConverter(Scale, Percent, Signed);
            }
            var binding = new Binding
            {
                Path = new PropertyPath(path),
                Converter = converter,
            };
            BindingOperations.SetBinding(entry, TextBox.TextProperty, binding);
        }

        uint Field = 0;
        uint SubIndex = 0;
        uint scale = 1;
        bool percent = false;
        bool signed = false;

        FrameworkElement IStatControl.LabelText
        {
            get { return label; }
        }
        FrameworkElement IStatControl.Value
        {
            get { return this; }
        }
    }
}
