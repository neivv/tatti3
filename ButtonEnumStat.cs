using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows;

using Requirement = Tatti3.GameData.Requirement;
using ArrayFileType = Tatti3.GameData.ArrayFileType;

namespace Tatti3
{
    public class ButtonEnumStat : EnumStat
    {
        public void SetList(List<(string, ArrayFileType?)> list)
        {
            foreach ((string name, _) in list)
            {
                this.Add(name);
            }
        }
    }
}
