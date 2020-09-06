using System;
using System.Collections;
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

    public class ArrayIndexConverter : IValueConverter
    {
        public ArrayIndexConverter()
        {
        }

        object? IValueConverter.Convert(
            object value,
            Type targetType,
            object parameter,
            System.Globalization.CultureInfo culture
        ) {
            int index = (int)parameter;
            if (value is IList array)
            {
                return array[index];
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

    public class MultiBindingToArrayConverter : IMultiValueConverter
    {
        object? IMultiValueConverter.Convert(
            object[] values,
            Type targetType,
            object parameter,
            System.Globalization.CultureInfo culture
        ) {
            object[] ret = new object[values.Length];
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = values[i];
            }
            return ret;
        }

        object[] IMultiValueConverter.ConvertBack(
            object value,
            Type[] targetTypes,
            object parameter,
            System.Globalization.CultureInfo culture
        ) {
            throw new NotSupportedException();
        }
    }
}
