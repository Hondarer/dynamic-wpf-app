using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;

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
