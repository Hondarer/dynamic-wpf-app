using AdornableWpfLib.ViewModels;

namespace AdornableWpfApp.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        private string _title = "MainWindow";

        public string Title
        { 
            get
            {
                return _title;
            }
            set
            {
                SetProperty(ref _title, value);
            }
        }
    }
}
