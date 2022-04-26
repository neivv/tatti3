using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace Tatti3
{
    public interface IStatControl
    {
        FrameworkElement LabelText { get; }
        FrameworkElement Value { get; }
        double Height() { return 20.0; }
    }
}
