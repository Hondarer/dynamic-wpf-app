using AdornableWpfApp.Commands;
using AdornableWpfApp.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

                if (DataContext != null)
                {
                    if (DataContext is IDisposable)
                    {
                        (DataContext as IDisposable).Dispose();
                    }
                    DataContext = null;
                }

                adornContentViewModel = null;

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

                string xamlPath = GetAdornerXamlPath(this);

                if (File.Exists(xamlPath) == true)
                {
                    // 例外が出る可能性がある
                    adornContent = FrameworkElementFromXamlFactory.Instance.GetFrameworkElement(xamlPath);
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

                string codeBehindPath = GetCodeBehindPath(this);

                if (File.Exists(codeBehindPath) == true)
                {
                    // 例外が出る可能性がある
                    adornContentCodeBehind = AssemblyFromCsFactory.Instance.GetNewInstance(
                        codeBehindPath,
                        GetCodeBehindClassName(this),
                        this);
                }

                string viewModelPath = GetViewModelPath(this);

                if (File.Exists(viewModelPath) == true)
                {
                    // 例外が出る可能性がある
                    adornContentViewModel = AssemblyFromCsFactory.Instance.GetNewInstance(
                        viewModelPath,
                        GetAdornerViewModelClassName(this));

                    DataContext = adornContentViewModel;
                }
                else
                {
                    Type type = GetType().Assembly.GetType(GetViewModelClassName(this));
                    if (type != null)
                    {
                        DataContext = Activator.CreateInstance(type);
                    }
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

        private string GetAdornerXamlPath(AdornableWindow target)
        {
            string targetFullName = target.GetType().FullName; // AdornableWpfApp.Views.MainWindow

            Match match = Regex.Match(targetFullName, @"^(?<head>.*?)\.(?<folder>[^\.]+)\.(?<class>[^\.]+)$");

            return $@"{match.Groups["folder"].Value}\{match.Groups["head"].Value}.{match.Groups["folder"].Value}.{match.Groups["class"].Value}Adorner.xaml"; // Views\AdornableWpfApp.Views.MainWindowAdorner.xaml
        }

        private string GetCodeBehindPath(AdornableWindow target)
        {
            return $@"{GetAdornerXamlPath(target)}.cs"; // Views\AdornableWpfApp.Views.MainWindowAdorner.xaml.cs
        }

        private string GetCodeBehindClassName(AdornableWindow target)
        {
            return $@"{target.GetType().FullName}Adorner"; // AdornableWpfApp.Views.MainWindowAdorner
        }

        private string GetViewModelPath(AdornableWindow target)
        {
            string targetFullName = target.GetType().FullName; // AdornableWpfApp.Views.MainWindow

            Match match = Regex.Match(targetFullName, @"^(?<head>.*?)\.(?<folder>[^\.]+)\.(?<class>[^\.]+)$");

            return $@"ViewModels\{match.Groups["head"].Value}.ViewModels.{match.Groups["class"].Value}ViewModelAdorner.cs"; // ViewModels\AdornableWpfApp.ViewModels.MainWindowViewModelAdorner.cs
        }

        private string GetViewModelClassName(AdornableWindow target)
        {
            string targetFullName = target.GetType().FullName; // AdornableWpfApp.Views.MainWindow

            Match match = Regex.Match(targetFullName, @"^(?<head>.*?)\.(?<folder>[^\.]+)\.(?<class>[^\.]+)$");

            return $@"{match.Groups["head"].Value}.ViewModels.{match.Groups["class"].Value}ViewModel"; // AdornableWpfApp.ViewModels.MainWindowViewModel
        }

        private string GetAdornerViewModelClassName(AdornableWindow target)
        {
            return $@"{GetViewModelClassName(target)}Adorner"; // AdornableWpfApp.ViewModels.MainWindowViewModelAdorner
        }
    }
}
