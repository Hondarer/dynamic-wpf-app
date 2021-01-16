using DynamicWpfApp.Utils;
using DynamicWpfApp.ViewModels;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace DynamicWpfApp.Views
{
    public class DynamicWindow : Window
    {
        /// <summary>
        /// 追加のコードビハインドを保持します。
        /// </summary>
        protected object codeBehindImpl = null;

        public DynamicWindow()
        {
            Initialized += DynamicWindow_Initialized;
        }

        private void DynamicWindow_Initialized(object sender, EventArgs e)
        {
            Initialized -= DynamicWindow_Initialized;

            try
            {
                FrameworkElement content = null;
                if (File.Exists(@"Views\MainWindowImpl.xaml") == true)
                {
                    using (StreamReader srImplXaml = new StreamReader(@"Views\MainWindowImpl.xaml"))
                    {
                        content = XamlReader.Load(srImplXaml.BaseStream) as FrameworkElement;

                        if (Content is Grid)
                        {
                            (Content as Grid).Children.Add(content);
                        }
                        else
                        {
                            // Content isn't Grid.
                        }
                    }
                }

                if (File.Exists(@"Views\MainWindowImpl.xaml.cs") == true)
                {
                    (sender as DynamicWindow).codeBehindImpl = LoadCodeBehind(content);
                }

                if (File.Exists(@"ViewModels\MainWindowViewModelImpl.cs") == true)
                {
                    LoadViewModel();
                }
                else
                {
                    DataContext = new MainWindowViewModel();
                }
            }
            catch (Exception ex)
            {
                if (Content is Grid)
                {
                    (Content as Grid).Children.Add(new TextBox()
                    {
                        Text = ex.ToString(),
                        IsReadOnly = true,
                        Foreground = Brushes.Red,
                        Background = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                    });
                }
            }
        }

        public object LoadCodeBehind(FrameworkElement content)
        {
            // 例外が出る可能性がある
            return AssemblyFromCsFactory.Instance.GetNewInstance(
                @"Views\MainWindowImpl.xaml.cs",
                "DynamicWpfApp.Views.MainWindowImpl",
                this, content);
        }

        public void LoadViewModel()
        {
            // 例外が出る可能性がある
            DataContext = AssemblyFromCsFactory.Instance.GetNewInstance(
                @"ViewModels\MainWindowViewModelImpl.cs",
                "DynamicWpfApp.ViewModels.MainWindowViewModelImpl");
        }
    }
}
