using Microsoft.UI.Xaml.Markup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emerald.WinUI.Helpers
{
    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public sealed class LocalizeString : MarkupExtension
    {
        public Core.Localized Name { get; set; }

        protected override object ProvideValue() => Name.ToLocalizedString();
    }
}
