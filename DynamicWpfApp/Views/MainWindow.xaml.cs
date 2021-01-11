using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Resources;
using System.Windows.Shapes;

namespace DynamicWpfApp
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            FrameworkElement rootObject;
            using (StreamReader sr = new StreamReader(@"Views\MainWindowImpl.xaml"))
            {
                rootObject = XamlReader.Load(sr.BaseStream) as FrameworkElement;
                if (Content is Grid)
                {
                    (Content as Grid).Children.Add(rootObject);
                }
                else
                {
                    // Content isn't Grid.
                }
            }

            var textBlock = rootObject.FindName("textBlock") as TextBlock;
            textBlock.Text = "ABCDE";
        }
    }
}
