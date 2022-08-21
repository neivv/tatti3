using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Tatti3
{
    /// <summary>
    /// Interaction logic for EnumStat.xaml
    /// </summary>
    [ContentProperty()]
    public partial class EnumStat : UserControl, IStatControl, System.Collections.IList
    {
        [ValueConversion(typeof(int), typeof(int))]
        public class DropdownFilterInvalid<T> : IValueConverter
        {
            public DropdownFilterInvalid(List<T> entries, uint mask)
            {
                this.entries = entries;
                if (mask == UInt32.MaxValue)
                {
                    this.maskConverter = null;
                }
                else
                {
                    this.maskConverter = new MaskIntConverter(mask);
                }
            }

            object? IValueConverter.Convert(
                object value,
                Type targetType,
                object parameter,
                System.Globalization.CultureInfo culture
            ) {
                int val = 0;
                if (this.maskConverter != null)
                {
                    val = (int)(uint)(maskConverter.Convert(value, targetType, parameter, culture)!);
                }
                else
                {
                    val = (int)(uint)value;
                }
                if (val < 0 || val >= entries.Count)
                {
                    return -1;
                }
                else
                {
                    return val;
                }
            }

            object? IValueConverter.ConvertBack(
                object value,
                Type targetType,
                object parameter,
                System.Globalization.CultureInfo culture
            ) {
                try
                {
                    int val = (int)value;
                    if (val < 0 || val >= entries.Count)
                    {
                        return Binding.DoNothing;
                    }
                    else
                    {
                        if (this.maskConverter != null)
                        {
                            return maskConverter.ConvertBack((uint)val, targetType, parameter, culture);
                        }
                        else
                        {
                            return (uint)val;
                        }
                    }
                }
                catch
                {
                    return DependencyProperty.UnsetValue;
                }
            }

            List<T> entries;
            MaskIntConverter? maskConverter;
        }

        [ValueConversion(typeof((uint, uint)), typeof(UInt32))]
        public class MaskIntConverter : IValueConverter
        {
            public MaskIntConverter(UInt32 mask)
            {
                this.mask = mask;
            }

            public object? Convert(
                object value,
                Type targetType,
                object parameter,
                System.Globalization.CultureInfo culture
            ) {
                var (val, _) = ((UInt32, uint))value;
                if (mask != UInt32.MaxValue)
                {
                    int shift = BitOperations.TrailingZeroCount(mask);
                    val = (val & mask) >> shift;
                }
                return val;
            }

            public object? ConvertBack(
                object value,
                Type targetType,
                object parameter,
                System.Globalization.CultureInfo culture
            ) {
                try
                {
                    UInt32 val = (UInt32)value;
                    if (mask != UInt32.MaxValue)
                    {
                        int shift = BitOperations.TrailingZeroCount(mask);
                        val = (val << shift) & mask;
                    }
                    return (mask, val);
                }
                catch
                {
                    return DependencyProperty.UnsetValue;
                }
            }

            UInt32 mask;
        }

        public EnumStat()
        {
            label = new TextBlock();
            InitializeComponent();
            InputMethod.SetPreferredImeState(numeric, InputMethodState.Off);
            enumNames = new List<string>();
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
                if (tokens.Length > 1)
                {
                    mask = ParseUint(tokens[1]);
                }
                else
                {
                    mask = uint.MaxValue;
                }
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

        public double DropdownWidth
        {
            set
            {
                dropdown.Width = value;
            }
        }

        void UpdateBinding()
        {
            if (mask == uint.MaxValue)
            {
                var path = $"Fields[{this.field}].Item";
                var binding = new Binding
                {
                    Path = new PropertyPath(path),
                };
                var binding2 = new Binding
                {
                    Path = new PropertyPath(path),
                    Converter = new DropdownFilterInvalid<string>(this.enumNames, this.mask),
                };

                BindingOperations.SetBinding(numeric, TextBox.TextProperty, binding);
                BindingOperations.SetBinding(dropdown, ComboBox.SelectedIndexProperty, binding2);
            }
            else
            {
                var path = $"Fields[{this.field}].ItemBits";
                var binding = new Binding
                {
                    Path = new PropertyPath(path),
                    Converter = new MaskIntConverter(this.mask),
                };
                var binding2 = new Binding
                {
                    Path = new PropertyPath(path),
                    Converter = new DropdownFilterInvalid<string>(this.enumNames, this.mask),
                };

                BindingOperations.SetBinding(numeric, TextBox.TextProperty, binding);
                BindingOperations.SetBinding(dropdown, ComboBox.SelectedIndexProperty, binding2);
            }
        }

        uint field = 0;
        uint mask = 0;

        FrameworkElement IStatControl.LabelText
        {
            get { return label; }
        }
        FrameworkElement IStatControl.Value
        {
            get { return this; }
        }

        // ---- Enum names
        public List<string> EnumNames
        {
            get => enumNames;
        }


        List<string> enumNames;

        public bool IsFixedSize => ((IList)enumNames).IsFixedSize;

        public bool IsReadOnly => ((IList)enumNames).IsReadOnly;

        public int Count => ((IList)enumNames).Count;

        public bool IsSynchronized => ((IList)enumNames).IsSynchronized;

        public object SyncRoot => ((IList)enumNames).SyncRoot;

        public object? this[int index] { get => ((IList)enumNames)[index]; set => ((IList)enumNames)[index] = value; }

        public int Add(object? value)
        {
            if (value is string str)
            {
                enumNames.Add(str);
                return enumNames.Count;
            }
            else
            {
                throw new ArgumentException("Added objects must be strings");
            }
        }

        public void Clear()
        {
            ((IList)enumNames).Clear();
        }

        public bool Contains(object? value)
        {
            return ((IList)enumNames).Contains(value);
        }

        public int IndexOf(object? value)
        {
            return ((IList)enumNames).IndexOf(value);
        }

        public void Insert(int index, object? value)
        {
            ((IList)enumNames).Insert(index, value);
        }

        public void Remove(object? value)
        {
            ((IList)enumNames).Remove(value);
        }

        public void RemoveAt(int index)
        {
            ((IList)enumNames).RemoveAt(index);
        }

        public void CopyTo(Array array, int index)
        {
            ((IList)enumNames).CopyTo(array, index);
        }

        public IEnumerator GetEnumerator()
        {
            return ((IList)enumNames).GetEnumerator();
        }
    }
}
