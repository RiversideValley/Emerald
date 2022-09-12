using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emerald.WinUI.Models
{
    public class Model : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Set<T>(ref T obj,T value)
        {
            obj = value;
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }
}
