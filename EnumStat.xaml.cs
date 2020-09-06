using System;
using System.Collections;
using System.Collections.Generic;
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
            public DropdownFilterInvalid(List<T> entries)
            {
                this.entries = entries;
            }

            object? IValueConverter.Convert(
                object value,
                Type targetType,
                object parameter,
                System.Globalization.CultureInfo culture
            ) {
                int val = (int)(uint)value;
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
                        return val;
                    }
                }
                catch
                {
                    return DependencyProperty.UnsetValue;
                }
            }

            List<T> entries;
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

        public uint FieldId
        {
            get => field;
            set
            {
                field = value;
                UpdateBinding();
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
            var path = $"Fields[{FieldId}].Item";
            var binding = new Binding
            {
                Path = new PropertyPath(path),
            };
            var binding2 = new Binding
            {
                Path = new PropertyPath(path),
                Converter = new DropdownFilterInvalid<string>(this.enumNames),
            };

            BindingOperations.SetBinding(numeric, TextBox.TextProperty, binding);
            BindingOperations.SetBinding(dropdown, ComboBox.SelectedIndexProperty, binding2);
        }

        uint field = 0;

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
