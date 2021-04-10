using DynamicWpfApp.Commands;
using DynamicWpfApp.Utils;
using DynamicWpfApp.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace DynamicWpfApp.Views
{
    public class ExpandableWindow : Window
    {
        /// <summary>
        /// 追加のコンテントのコードビハインドを保持します。
        /// </summary>
        private object additionalContentCodeBehind = null;

        private FrameworkElement additionalContent = null;

        private FrameworkElement additionalErrorContent = null;

        private List<string> additionalNames = new List<string>();

        private object additionalContentViewModel = null;

        public DelegateCommand RefreshAdditionalCommand { get; }

        public ExpandableWindow()
        {
            Loaded += ExpandableWindow_Loaded;

            RefreshAdditionalCommand = new DelegateCommand(RefreshAdditional);

            InputBindings.Add(new KeyBinding() { Gesture = new KeyGesture(Key.F5, ModifierKeys.Shift, "Shift+F5"), Command = RefreshAdditionalCommand });
        }

        private void ExpandableWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ExpandableWindow_Loaded;

            RefreshAdditional();
        }

        public void RefreshAdditional(object parameter=null)
        {
            if (!(Content is Panel))
            {
                return;
            }

            try
            {
                if (additionalErrorContent != null)
                {
                    (Content as Panel).Children.Remove(additionalErrorContent);

                    additionalErrorContent = null;
                }

                if (additionalContentViewModel != null)
                {
                    DataContext = null;

                    if (additionalContentViewModel is IDisposable)
                    {
                        (additionalContentViewModel as IDisposable).Dispose();
                    }
                    additionalContentViewModel = null;
                }

                if (additionalContentCodeBehind != null)
                {
                    if (additionalContentCodeBehind is IDisposable)
                    {
                        (additionalContentCodeBehind as IDisposable).Dispose();
                    }
                    additionalContentCodeBehind = null;
                }

                if (additionalContent != null)
                {
                    foreach (string additionalName in additionalNames)
                    {
                        UnregisterName(additionalName);
                    }
                    additionalNames.Clear();

                    (Content as Panel).Children.Remove(additionalContent);

                    additionalContent = null;
                }

                if (File.Exists(@"Views\MainWindowEx.xaml") == true)
                {
                    // 例外が出る可能性がある
                    additionalContent = FrameworkElementFromXamlFactory.Instance.GetFrameworkElement(@"Views\MainWindowEx.xaml");
                    additionalContent.Name = "additionalContent";

                    (Content as Panel).Children.Add(additionalContent);
                    RegisterName(additionalContent.Name, additionalContent);
                    additionalNames.Add(additionalContent.Name);

                    // XamlReader で読み込むと、読み込んだ xaml のルート要素に NameScope が構築される。
                    // この NameScope を親にも詰める。

                    INameScopeDictionary gridNameScope = NameScope.GetNameScope(additionalContent) as INameScopeDictionary;

                    foreach (KeyValuePair<string, object> nameScopeEntry in gridNameScope.AsEnumerable())
                    {
                        RegisterName(nameScopeEntry.Key, nameScopeEntry.Value);
                        additionalNames.Add(nameScopeEntry.Key);
                    }
                }

                if (File.Exists(@"Views\MainWindowEx.xaml.cs") == true)
                {
                    additionalContentCodeBehind = LoadCodeBehind();
                }

                if (File.Exists(@"ViewModels\MainWindowViewModelEx.cs") == true)
                {
                    additionalContentViewModel = LoadViewModel();
                    DataContext = additionalContentViewModel;
                }
                else
                {
                    DataContext = new MainWindowViewModel();
                }
            }
            catch (Exception ex)
            {
                additionalErrorContent = new TextBox()
                {
                    Text = ex.ToString(),
                    IsReadOnly = true,
                    Foreground = Brushes.DarkRed,
                    Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                (Content as Panel).Children.Add(additionalErrorContent);
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

        public object LoadViewModel()
        {
            // 例外が出る可能性がある
            return AssemblyFromCsFactory.Instance.GetNewInstance(
                @"ViewModels\MainWindowViewModelEx.cs",
                "DynamicWpfApp.ViewModels.MainWindowViewModelEx");
        }
    }
}
