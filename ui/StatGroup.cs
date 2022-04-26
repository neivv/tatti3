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
    [ContentProperty()]
    public class StatGroup : UserControl, System.Collections.IList
    {
        public StatGroup()
        {
            stats = new List<IStatControl>();
            labelCol = new ColumnDefinition()
            {
                Width = new GridLength(60.0),
            };
            boxCol = new ColumnDefinition();
            grid = new Grid();
            grid.HorizontalAlignment = HorizontalAlignment.Left;
            grid.ColumnDefinitions.Add(labelCol);
            grid.ColumnDefinitions.Add(boxCol);
            AddChild(grid);
            explicitLabelColWidth = false;
        }

        public GridLength LabelWidth
        {
            set
            {
                labelCol.Width = value;
                explicitLabelColWidth = true;
            }
        }
        /// Changes stat layout to be labels on the right w/ left align
        public bool Flags
        {
            set
            {
                if (!flagLayout && value)
                {
                    if (!explicitLabelColWidth)
                    {
                        labelCol.Width = GridLength.Auto;
                    }
                    grid.ColumnDefinitions.Clear();
                    grid.ColumnDefinitions.Add(boxCol);
                    grid.ColumnDefinitions.Add(labelCol);
                }
                if (flagLayout && !value)
                {
                    grid.ColumnDefinitions.Clear();
                    grid.ColumnDefinitions.Add(labelCol);
                    grid.ColumnDefinitions.Add(boxCol);
                }
                flagLayout = value;
            }
        }

        List<IStatControl> stats;
        ColumnDefinition labelCol;
        ColumnDefinition boxCol;
        bool flagLayout;
        bool explicitLabelColWidth;

        public bool IsFixedSize => ((IList)stats).IsFixedSize;

        public bool IsReadOnly => ((IList)stats).IsReadOnly;

        public int Count => ((IList)stats).Count;

        public bool IsSynchronized => ((IList)stats).IsSynchronized;

        public object SyncRoot => ((IList)stats).SyncRoot;

        public object? this[int index] { get => ((IList)stats)[index]; set => ((IList)stats)[index] = value; }

        public int Add(object? value)
        {
            if (value is IStatControl ctrl)
            {
                stats.Add(ctrl);
                var label = ctrl.LabelText;
                var rest = ctrl.Value;
                rest.Margin = new Thickness(5.0, 0.0, 0.0, 0.0);
                var row = stats.Count - 1;
                grid.RowDefinitions.Add(new RowDefinition {
                    Height = new GridLength(ctrl.Height()),
                });
                Grid.SetRow(label, row);
                Grid.SetRow(rest, row);
                if (flagLayout)
                {
                    Grid.SetColumn(label, 1);
                    Grid.SetColumn(rest, 0);
                    label.HorizontalAlignment = HorizontalAlignment.Left;
                }
                else
                {
                    Grid.SetColumn(label, 0);
                    Grid.SetColumn(rest, 1);
                    label.HorizontalAlignment = HorizontalAlignment.Right;
                }

                grid.Children.Add(label);
                grid.Children.Add(rest);
                return stats.Count;
            }
            else
            {
                throw new ArgumentException("Added objects must implement IStatControl");
            }
        }

        public void Clear()
        {
            ((IList)stats).Clear();
        }

        public bool Contains(object? value)
        {
            return ((IList)stats).Contains(value);
        }

        public int IndexOf(object? value)
        {
            return ((IList)stats).IndexOf(value);
        }

        public void Insert(int index, object? value)
        {
            ((IList)stats).Insert(index, value);
        }

        public void Remove(object? value)
        {
            ((IList)stats).Remove(value);
        }

        public void RemoveAt(int index)
        {
            ((IList)stats).RemoveAt(index);
        }

        public void CopyTo(Array array, int index)
        {
            ((IList)stats).CopyTo(array, index);
        }

        public IEnumerator GetEnumerator()
        {
            return ((IList)stats).GetEnumerator();
        }

        Grid grid;
    }
}
