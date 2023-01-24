using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace Emerald.WinUI.Models
{
    [ObservableObject]
    public partial class Model
    {
        internal void Set<T>(ref T obj, T value, string name = null)
        {
            SetProperty(ref obj, value, name);
        }

        public void InvokePropertyChanged(string name = null)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(name));
        }
    }
}
