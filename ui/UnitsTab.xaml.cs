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
    /// Interaction logic for UnitsTab.xaml
    /// </summary>
    public partial class UnitsTab : UserControl
    {
        public UnitsTab()
        {
            InitializeComponent();
            this.DataContextChanged += (o, e) => this.UpdateBinding();
        }

        void UpdateBinding()
        {
            var dat = (AppState.DatTableRef)this.DataContext;
            if (dat == null)
            {
                return;
            }
            ((ListIndexConverter)Resources["ListIndexConverter"]).List = dat.Names;
            var root = dat.Root;
            root.NamesChanged += (o, args) => {
                if (ReferenceEquals(root, o) && args.Type == GameData.ArrayFileType.Units) {
                    var dat = (AppState.DatTableRef)this.DataContext;
                    ((ListIndexConverter)Resources["ListIndexConverter"]).List = dat.Names;
                }
            };

        }
    }
}
