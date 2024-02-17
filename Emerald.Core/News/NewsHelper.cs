using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;

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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private ObservableCollection<JSON.Entry> _Entries = new();

        private ObservableCollection<JSON.Entry> AllEntries;

        public ObservableCollection<JSON.Entry> Entries
        {
            get => _Entries ?? new();
            set => Set(ref _Entries, value, nameof(Entries));
        }

        public ObservableCollection<JSON.Entry> Search(string key, string[] filter = null)
        {
            if (AllEntries == null)
                return new();


            if (string.IsNullOrEmpty(key) || string.IsNullOrWhiteSpace(key))
            {
                Entries.Clear();

                foreach (var item in AllEntries.Where(x => filter == null || filter.Contains(x.Category)))
                    Entries.Add(item);

                return Entries;
            }

            var s = new ObservableCollection<JSON.Entry>();
            var splitText = key.Split(" ");

            foreach (var itm in AllEntries ?? new())
            {
                var found = splitText.All((key) => itm.Title.ToLower().Contains(key));
                if (found)
                {
                    s.Add(itm);
                }
            }

            Entries.Clear();

            foreach (var item in s.Where(x => filter == null || filter.Contains(x.Category)))
                Entries.Add(item);

            return Entries;
        }

        public async Task<ObservableCollection<JSON.Entry>> LoadEntries(string[] filter = null)
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
                AllEntries = json?.entries != null ? new(json.entries.ToList()) : new();

                Entries.Clear();

                foreach (var item in AllEntries.Where(x => filter == null || filter.Contains(x.Category)))
                    Entries.Add(item);

            }
            catch
            {
                Entries.Clear();
            }
            return Entries;
        }
    }
}
