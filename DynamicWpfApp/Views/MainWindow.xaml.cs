using DynamicWpfApp.Utils;
using DynamicWpfApp.ViewModels;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace DynamicWpfApp
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 追加のコードビハインドを保持します。
        /// </summary>
        private readonly object codeBehindImpl = null;

        public MainWindow()
        {
            InitializeComponent();

            FrameworkElement content = null;
            if (File.Exists(@"Views\MainWindowImpl.xaml") == true)
            {
                using (StreamReader srImplXaml = new StreamReader(@"Views\MainWindowImpl.xaml"))
                {
                    // TODO: 例外をつかまえる
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
                codeBehindImpl = LoadCodeBehind(content);
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

        public object LoadCodeBehind(FrameworkElement content)
        {
            // 例外が出る可能性がある
            Assembly assembly = AssemblyFromCsFactory.Instance.GetAssembly(@"Views\MainWindowImpl.xaml.cs");
            // 見つからないとtypeがnullになる
            Type type = assembly.GetType("DynamicWpfApp.Views.MainWindowImpl");
            // 例外が出る可能性がある
            object obj = Activator.CreateInstance(type, this, content);

            return obj;
        }

        public void LoadViewModel()
        {
            // 例外が出る可能性がある
            Assembly assembly = AssemblyFromCsFactory.Instance.GetAssembly(@"ViewModels\MainWindowViewModelImpl.cs");
            // 見つからないとtypeがnullになる
            Type type = assembly.GetType("DynamicWpfApp.ViewModels.MainWindowViewModelImpl");
            // 例外が出る可能性がある
            object obj = Activator.CreateInstance(type);

            DataContext = obj;
        }
    }
}
