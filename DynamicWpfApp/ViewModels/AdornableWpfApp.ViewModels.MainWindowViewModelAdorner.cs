#r "System.Text.Json"

using AdornableWpfLib.ViewModels;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace AdornableWpfApp.ViewModels
{
    public class RecordData1 : BindableBase
    {
        public string Text { get; set; }
    }

    public class RecordData2 : BindableBase
    {
        public int IntData { get; set; }

        public string StringData { get; set; }
    }

    public class MainWindowViewModelAdorner : MainWindowViewModel
    {
        public RecordData1 RecordData1 { get; }
        public ObservableCollection<RecordData2> RecordsData2 { get; }

        public MainWindowViewModelAdorner()
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                // 大文字と小文字を区別しないプロパティ名の照合を有効にする
                PropertyNameCaseInsensitive = true
            };

            using (StreamReader sr = new StreamReader(@"Data\RecordData1.json"))
            {
                RecordData1 = JsonSerializer.Deserialize<RecordData1>(sr.ReadToEnd(), options);
            }

            using (StreamReader sr = new StreamReader(@"Data\RecordsData2.json"))
            {
                RecordsData2 = JsonSerializer.Deserialize<ObservableCollection<RecordData2>>(sr.ReadToEnd(), options);
            }
        }
    }
}
