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
        [ValueConversion(typeof(uint), typeof(float))]
        class ScaleConverter : IValueConverter
        {
            public ScaleConverter(uint scale)
            {
                this.scale = scale;
            }

            object? IValueConverter.Convert(
                object value,
                Type targetType,
                object parameter,
                System.Globalization.CultureInfo culture
            ) {
                return (float)(uint)value / (float)scale;
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
                    return (uint)(Single.Parse((string)value, format) * (float)scale);
                }
                catch
                {
                    return DependencyProperty.UnsetValue;
                }
            }

            uint scale;
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

        void UpdateBinding()
        {
            var path = $"Fields[{Field}~{SubIndex}].Item";
            IValueConverter? converter = null;
            if (Scale != 1)
            {
                converter = new ScaleConverter(Scale);
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
