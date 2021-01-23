using DynamicWpfApp.Utils;
using DynamicWpfApp.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public ExpandableWindow()
        {
            Loaded += ExpandableWindow_Loaded;
        }

        private void ExpandableWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ExpandableWindow_Loaded;

            try
            {
                Grid grid = null;
                if (File.Exists(@"Views\MainWindowEx.xaml") == true)
                {
                    using (StreamReader srImplXaml = new StreamReader(@"Views\MainWindowEx.xaml"))
                    {
                        grid = XamlReader.Load(srImplXaml.BaseStream) as Grid;
                        grid.Name = "expansionGrid";

                        if (Content is Grid)
                        {
                            (Content as Grid).Children.Add(grid);
                            RegisterName(grid.Name, grid);

                            // XamlReader で読み込むと、読み込んだ xaml のルート要素に NameScope が構築される。
                            // この NameScope を親に詰め替える。

                            INameScopeDictionary gridNameScope = NameScope.GetNameScope(grid) as INameScopeDictionary;

                            foreach (KeyValuePair<string, object> nameScopeEntry in gridNameScope.AsEnumerable())
                            {
                                RegisterName(nameScopeEntry.Key, nameScopeEntry.Value);
                            }

                            gridNameScope.Clear();
                        }
                        else
                        {
                            // Content isn't Grid.
                        }
                    }
                }

                if (File.Exists(@"Views\MainWindowEx.xaml.cs") == true)
                {
                    (sender as ExpandableWindow).expansionGridCodeBehind = LoadCodeBehind();
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

        public object LoadCodeBehind()
        {
            // 例外が出る可能性がある
            return AssemblyFromCsFactory.Instance.GetNewInstance(
                @"Views\MainWindowEx.xaml.cs",
                "DynamicWpfApp.Views.MainWindowEx",
                this);
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
