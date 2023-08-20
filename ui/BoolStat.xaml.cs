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
    /// Interaction logic for BoolStat.xaml
    /// </summary>
    public partial class BoolStat : UserControl, IStatControl
    {
        [ValueConversion(typeof((uint, uint)), typeof(bool))]
        class BitConverter : IValueConverter
        {
            public BitConverter(uint mask)
            {
                this.mask = mask;
            }

            object? IValueConverter.Convert(
                object value,
                Type targetType,
                object parameter,
                System.Globalization.CultureInfo culture
            ) {
                var (bits, _) = ((uint, uint))value;
                return (bits & mask) == mask;
            }

            object? IValueConverter.ConvertBack(
                object value,
                Type targetType,
                object parameter,
                System.Globalization.CultureInfo culture
            ) {
                bool set = (bool)value;
                if (set)
                {
                    return (mask, mask);
                }
                else
                {
                    return (mask, 0U);
                }
            }

            uint mask;
        }

        public BoolStat()
        {
            label = new TextBlock();
            label.MouseUp += (e, args) => {
                entry.IsChecked = !entry.IsChecked;
            };
            InitializeComponent();
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
                var tokens = value.Split(':');
                field = ParseUint(tokens[0]);
                bit = ParseUint(tokens[1]);
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

        void UpdateBinding()
        {
            var path = $"Fields[{field}].ItemBits";
            IValueConverter converter = new BitConverter(bit);
            var binding = new Binding
            {
                Path = new PropertyPath(path),
                Converter = converter,
            };
            BindingOperations.SetBinding(entry, CheckBox.IsCheckedProperty, binding);
        }

        uint field = 0;
        uint bit = 0;

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
