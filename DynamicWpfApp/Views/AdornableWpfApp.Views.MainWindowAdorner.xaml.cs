using System.Windows;
using System.Windows.Controls;

namespace AdornableWpfApp.Views
{
    public class MainWindowAdorner
    {
        public MainWindowAdorner(FrameworkElement window)
        {
            TextBlock textBlock = window.FindName("textBlock") as TextBlock;
            textBlock.Text = "ABCDE";
        }
    }
}
