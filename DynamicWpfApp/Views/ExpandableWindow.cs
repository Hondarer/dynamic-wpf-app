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
    public class ExpandableWindow : Window
    {
        /// <summary>
        /// 追加のグリッドのコードビハインドを保持します。
        /// </summary>
        protected object expansionGridCodeBehind = null;

        /// <summary>
        /// 追加のグリッドを保持します。
        /// </summary>
        protected Grid ExpansionGrid { get; set; }

        public ExpandableWindow()
        {
            Initialized += DynamicWindow_Initialized;
        }

        private void DynamicWindow_Initialized(object sender, EventArgs e)
        {
            Initialized -= DynamicWindow_Initialized;

            try
            {
                Grid grid = null;
                if (File.Exists(@"Views\MainWindowEx.xaml") == true)
                {
                    using (StreamReader srImplXaml = new StreamReader(@"Views\MainWindowEx.xaml"))
                    {
                        grid = XamlReader.Load(srImplXaml.BaseStream) as Grid;

                        if (Content is Grid)
                        {
                            (Content as Grid).Children.Add(grid);
                            ExpansionGrid = grid;
                        }
                        else
                        {
                            // Content isn't Grid.
                        }
                    }
                }

                if (File.Exists(@"Views\MainWindowEx.xaml.cs") == true)
                {
                    (sender as ExpandableWindow).expansionGridCodeBehind = LoadCodeBehind(grid);
                }

                if (File.Exists(@"ViewModels\MainWindowViewModelEx.cs") == true)
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

        public object LoadCodeBehind(Grid grid)
        {
            // 例外が出る可能性がある
            return AssemblyFromCsFactory.Instance.GetNewInstance(
                @"Views\MainWindowEx.xaml.cs",
                "DynamicWpfApp.Views.MainWindowEx",
                grid);
        }

        public void LoadViewModel()
        {
            // 例外が出る可能性がある
            DataContext = AssemblyFromCsFactory.Instance.GetNewInstance(
                @"ViewModels\MainWindowViewModelEx.cs",
                "DynamicWpfApp.ViewModels.MainWindowViewModelEx");
        }
    }
}
