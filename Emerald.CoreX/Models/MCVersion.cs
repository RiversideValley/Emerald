using CmlLib.Core.Version;
using CmlLib.Core.VersionMetadata;
using CommunityToolkit.Mvvm.ComponentModel;
using Emerald.CoreX.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emerald.CoreX.Models
{
    public partial class MCVersion
    {
        public MCVersionType Type { get; private set; }
        public MVersionType ReleaseType { get; private set; }

        public string Name { get; private set; }

        public string DisplayName { get; set; }

        public IVersionMetadata Metadata { get; private set; }

        public bool Local { get; private set; }

        public MCVersion[] Subversions { get; private set; }

    }
}
