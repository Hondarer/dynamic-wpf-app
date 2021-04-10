using AdornableWpfApp.Commands;
using AdornableWpfApp.Utils;
using AdornableWpfApp.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace AdornableWpfApp.Views
{
    public class AdornableWindow : Window
    {
        /// <summary>
        /// 追加のコンテントのコードビハインドを保持します。
        /// </summary>
        private object adornContentCodeBehind = null;

        private FrameworkElement adornContent = null;

        private FrameworkElement adornErrorContent = null;

        private List<string> adornNames = new List<string>();

        private object adornContentViewModel = null;

        public DelegateCommand RefreshAdditionalCommand { get; }

        public AdornableWindow()
        {
            Loaded += AdornableWindow_Loaded;

            RefreshAdditionalCommand = new DelegateCommand(RefreshAdorner);

            InputBindings.Add(new KeyBinding() { Gesture = new KeyGesture(Key.F5, ModifierKeys.Shift, "Shift+F5"), Command = RefreshAdditionalCommand });
        }

        private void AdornableWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= AdornableWindow_Loaded;

            RefreshAdorner();
        }

        public void RefreshAdorner(object parameter=null)
        {
            if ((Content is Panel) != true)
            {
                return;
            }

            try
            {
                if (adornErrorContent != null)
                {
                    (Content as Panel).Children.Remove(adornErrorContent);

                    adornErrorContent = null;
                }

                if (adornContentViewModel != null)
                {
                    DataContext = null;

                    if (adornContentViewModel is IDisposable)
                    {
                        (adornContentViewModel as IDisposable).Dispose();
                    }
                    adornContentViewModel = null;
                }

                if (adornContentCodeBehind != null)
                {
                    if (adornContentCodeBehind is IDisposable)
                    {
                        (adornContentCodeBehind as IDisposable).Dispose();
                    }
                    adornContentCodeBehind = null;
                }

                if (adornContent != null)
                {
                    foreach (string additionalName in adornNames)
                    {
                        UnregisterName(additionalName);
                    }
                    adornNames.Clear();

                    (Content as Panel).Children.Remove(adornContent);

                    adornContent = null;
                }

                if (File.Exists(@"Views\MainWindowAdorner.xaml") == true)
                {
                    // 例外が出る可能性がある
                    adornContent = FrameworkElementFromXamlFactory.Instance.GetFrameworkElement(@"Views\MainWindowAdorner.xaml");
                    adornContent.Name = "adornContent";
                    (Content as Panel).Children.Add(adornContent);
                    RegisterName(adornContent.Name, adornContent);
                    adornNames.Add(adornContent.Name);

                    // XamlReader で読み込むと、読み込んだ xaml のルート要素に NameScope が構築される。
                    // この NameScope を親にも詰める。

                    INameScopeDictionary gridNameScope = NameScope.GetNameScope(adornContent) as INameScopeDictionary;

                    foreach (KeyValuePair<string, object> nameScopeEntry in gridNameScope.AsEnumerable())
                    {
                        RegisterName(nameScopeEntry.Key, nameScopeEntry.Value);
                        adornNames.Add(nameScopeEntry.Key);
                    }
                }

                if (File.Exists(@"Views\MainWindowAdorner.xaml.cs") == true)
                {
                    // 例外が出る可能性がある
                    adornContentCodeBehind = AssemblyFromCsFactory.Instance.GetNewInstance(
                        @"Views\MainWindowAdorner.xaml.cs",
                        "AdornableWpfApp.Views.MainWindowAdorner",
                        this);
                }

                if (File.Exists(@"ViewModels\MainWindowViewModelAdorner.cs") == true)
                {
                    // 例外が出る可能性がある
                    adornContentViewModel = AssemblyFromCsFactory.Instance.GetNewInstance(
                        @"ViewModels\MainWindowViewModelAdorner.cs",
                        "AdornableWpfApp.ViewModels.MainWindowViewModelAdorner");

                    DataContext = adornContentViewModel;
                }
                else
                {
                    DataContext = new MainWindowViewModel();
                }
            }
            catch (Exception ex)
            {
                adornErrorContent = new TextBox()
                {
                    Text = ex.ToString(),
                    IsReadOnly = true,
                    Foreground = Brushes.DarkRed,
                    Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                (Content as Panel).Children.Add(adornErrorContent);
            }
        }
    }
}
