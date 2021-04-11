using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace AdornableWpfApp.Views
{
    public class MainWindowAdorner
    {
        TextBlock textBlock;

        public MainWindowAdorner(ContentControl contentControl)
        {
            textBlock = contentControl.FindName("textBlock") as TextBlock;
            textBlock.Text = "ABCDE";

            (contentControl.FindName("button") as Button).Click += Button_Click;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            textBlock.Text = "Click!";
        }
    }
}
