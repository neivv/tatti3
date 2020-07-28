using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Data;

using ArrayFileType = Tatti3.GameData.ArrayFileType;

namespace Tatti3
{
    // "Used by:" references between dats (backwards compared to direction that the
    // dat files specify)
    public class BackRef
    {
        public SortedSet<(ArrayFileType, uint)> Set
        {
            get => set;
        }
        SortedSet<(ArrayFileType, uint)> set = new SortedSet<(ArrayFileType, uint)>();
    }

    public class BackRefConverter : IMultiValueConverter
    {
        object? IMultiValueConverter.Convert(
            object[] values,
            Type targetType,
            object parameter,
            System.Globalization.CultureInfo culture
        ) {
            var root = (AppState)values[0];
            (var datType, var entry) = ((ArrayFileType, uint))values[1];
            var names = root.ArrayFileNames(datType);
            string name = names.Count > (int)entry ? names[(int)entry]: "";
            string prefix = datType switch
            {
                ArrayFileType.Units => "U",
                ArrayFileType.Weapons => "W",
                ArrayFileType.Flingy => "F",
                ArrayFileType.Sprites => "S",
                ArrayFileType.Images => "I",
                ArrayFileType.Upgrades => "Upg",
                ArrayFileType.TechData => "T",
                ArrayFileType.Orders => "O",
                _ => "",
            };
            return $"[{prefix}{entry}] {name}";
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
