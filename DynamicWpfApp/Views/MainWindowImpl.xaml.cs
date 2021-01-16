using System.Windows;
using System.Windows.Controls;

namespace DynamicWpfApp.Views
{
    public class MainWindowImpl
    {
        public MainWindowImpl(MainWindow mainWindow, FrameworkElement content)
        {
            TextBlock textBlock = content.FindName("textBlock") as TextBlock;
            textBlock.Text = "ABCDE";
        }
    }
}
