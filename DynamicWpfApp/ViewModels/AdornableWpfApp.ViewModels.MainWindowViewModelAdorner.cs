#r "System.Text.Json"

using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace AdornableWpfApp.ViewModels
{
    public class MainWindowViewModelAdorner : MainWindowViewModel
    {
        public string TextContent { get; set; } = "TEXTCONTENT";

        public class RecordViewModel : BindableBase
        {
            public int IntValue { get; set; }

            public string StringValue { get; set; }
        }

        public ObservableCollection<RecordViewModel> GridViewModel { get; }

        public MainWindowViewModelAdorner()
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                // 大文字と小文字を区別しないプロパティ名の照合を有効にする
                PropertyNameCaseInsensitive = true,
            };

            using (StreamReader sr = new StreamReader(@"Data\GridViewModel.json"))
            {
                GridViewModel = JsonSerializer.Deserialize<ObservableCollection<RecordViewModel>>(sr.ReadToEnd(), options);
            }
        }
    }
}
