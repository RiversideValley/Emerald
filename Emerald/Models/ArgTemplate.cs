using CommunityToolkit.Mvvm.ComponentModel;

namespace Emerald.Models;

//Copied from Emerald.UWP
[ObservableObject]
public partial class ArgTemplate
{
    [ObservableProperty]
    private string arg;

    public int Count { get; set; }
}
