using System.Windows;
using System.Windows.Controls;

namespace DynamicWpfApp.Views
{
    public class MainWindowEx
    {
        public MainWindowEx(FrameworkElement window)
        {
            TextBlock textBlock = window.FindName("textBlock") as TextBlock;
            textBlock.Text = "ABCDE";
        }
    }
}
