using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Emerald.Core.News
{
    public class NewsHelper : INotifyPropertyChanged
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
        private ObservableCollection<JSON.Entry> _Entries;
        private ObservableCollection<JSON.Entry> AllEntries;
        public ObservableCollection<JSON.Entry> Entries
        {
            get => _Entries ?? new();
            set => Set(ref _Entries, value, nameof(Entries));
        }
        public ObservableCollection<JSON.Entry> Search(string key)
        {
            if(string.IsNullOrEmpty(key) || string.IsNullOrWhiteSpace(key))
            {
                Entries = AllEntries;
                return Entries;
            }

            var s = new ObservableCollection<JSON.Entry>();
            var splitText = key.Split(" ");
            foreach (var itm in AllEntries)
            {
                var found = splitText.All((key) => itm.Title.ToLower().Contains(key));
                if (found)
                {
                    s.Add(itm);
                }
            }
            Entries = s;
            return Entries;
        }
        public async Task<ObservableCollection<JSON.Entry>> LoadEntries()
        {
            try
            {
                string response;
                using (var wc = new HttpClient())
                {
                    var url = "https://launchercontent.mojang.com/news.json";
                    response = await wc.GetStringAsync(url);
                }
                var json = JsonConvert.DeserializeObject<JSON.Root>(response);
                Entries = AllEntries = json?.entries != null ? new(json.entries.ToList()) : new();
                return Entries;
            }
            catch
            {
                return new();
            }
        }
    }
}

