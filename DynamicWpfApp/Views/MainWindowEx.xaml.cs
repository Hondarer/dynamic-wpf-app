using System.Windows;
using System.Windows.Controls;

namespace DynamicWpfApp.Views
{
    public class MainWindowEx
    {
        public MainWindowEx(Grid grid)
        {
            TextBlock textBlock = grid.FindName("textBlock") as TextBlock;
            textBlock.Text = "ABCDE";
        }
    }
}
