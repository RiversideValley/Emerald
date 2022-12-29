using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emerald.WinUI.Helpers
{
    public class ConditionString
    {
        public string TrueString { get; set; }
        public string FalseString { get; set; }
        public bool Condition { get; set; }
        public string Result => Condition ? TrueString : FalseString;
        public override string ToString() => Result;
    }
}