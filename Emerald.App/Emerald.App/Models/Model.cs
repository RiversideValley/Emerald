using System.ComponentModel;

namespace Emerald.WinUI.Models
{
    public class Model : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        internal void Set<T>(ref T obj, T value, string name = null)
        {
            obj = value;
            InvokePropertyChanged(name);
        }

        public void InvokePropertyChanged(string name = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
