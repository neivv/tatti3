using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;

namespace Tatti3 {
    public class TupleIndexConverter : IValueConverter
    {
        public TupleIndexConverter()
        {
        }

        object? IValueConverter.Convert(
            object value,
            Type targetType,
            object parameter,
            System.Globalization.CultureInfo culture
        ) {
            int index = (int)parameter;
            if (value is ITuple tuple)
            {
                return tuple[index];
            }
            else
            {
                return null;
            }
        }

        object? IValueConverter.ConvertBack(
            object value,
            Type targetType,
            object parameter,
            System.Globalization.CultureInfo culture
        ) {
            throw new NotSupportedException();
        }
    }
}
