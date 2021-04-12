using AdornableWpfLib.Commands;
using AdornableWpfLib.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interactivity;
using System.Windows.Markup;
using System.Windows.Media;

namespace AdornableWpfLib.Behaviors
{
    /// <summary>
    /// 追加可能なビヘイビアを提供します。
    /// </summary>
    public class AdornableBehavior : Behavior<ContentControl>
    {
        /// <summary>
        /// 追加のコンテントの更新コマンドの <see cref="InputBinding"/> を保持します。
        /// </summary>
        private InputBinding refreshAdornerBinding = null;

        /// <summary>
        /// 追加のコンテントの名前を表します。
        /// </summary>
        public const string ADORN_CONTENT_NAME = "adornContent";

        /// <summary>
        /// 追加のコンテントのコードビハインドを保持します。
        /// </summary>
        private object adornContentCodeBehind = null;

        /// <summary>
        /// 追加のコンテントを保持します。
        /// </summary>
        private FrameworkElement adornContent = null;

        /// <summary>
        /// 追加の際に発生したエラーを表示するコンテントを保持します。
        /// </summary>
        private FrameworkElement adornErrorContent = null;

        /// <summary>
        /// 追加のコンテントで管理されている名前のリストを保持します。
        /// </summary>
        private readonly List<string> adornNames = new List<string>();

        /// <summary>
        /// 追加のコンテントの ViewModel を保持します。
        /// </summary>
        private object adornContentViewModel = null;

        /// <summary>
        /// 追加のコンテントの更新コマンドを保持します。
        /// </summary>
        public DelegateCommand RefreshAdornerCommand { get; }

        /// <summary>
        /// <see cref="AdornableBehavior"/> の新しいインスタンスを生成します。
        /// </summary>
        public AdornableBehavior()
        {
            RefreshAdornerCommand = new DelegateCommand(RefreshAdorner);
        }

        /// <summary>
        /// Called after the behavior is attached to an AssociatedObject.
        /// </summary>
        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, (Action)(() => AssociatedObjectLoaded()));
        }

        /// <summary>
        /// Called when the behavior is being detached from its AssociatedObject.
        /// </summary>
        protected override void OnDetaching()
        {
            if (refreshAdornerBinding != null)
            {
                AssociatedObject.InputBindings.Remove(refreshAdornerBinding);
                refreshAdornerBinding = null;
            }

            base.OnDetaching();
        }

        /// <summary>
        /// 関連付けされたオブジェクトの読み込みが完了されたときの処理をします。
        /// </summary>
        private void AssociatedObjectLoaded()
        {
            RefreshAdorner();

            refreshAdornerBinding = new KeyBinding() { Gesture = new KeyGesture(Key.F5, ModifierKeys.Shift, "Shift+F5"), Command = RefreshAdornerCommand };
            AssociatedObject.InputBindings.Add(refreshAdornerBinding);
        }

        /// <summary>
        /// 追加のコンテントを更新します。
        /// </summary>
        /// <param name="parameter">コマンドのパラメーター。使用しません。省略可能です。規定値は <c>null</c> です。</param>
        public void RefreshAdorner(object parameter = null)
        {
            if ((AssociatedObject.Content is Panel) != true)
            {
                return;
            }

            try
            {
                if (adornErrorContent != null)
                {
                    (AssociatedObject.Content as Panel).Children.Remove(adornErrorContent);
                    adornErrorContent = null;
                }

                if (AssociatedObject.DataContext != null)
                {
                    if (AssociatedObject.DataContext is IDisposable)
                    {
                        (AssociatedObject.DataContext as IDisposable).Dispose();
                    }
                    AssociatedObject.DataContext = null;
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
                        AssociatedObject.UnregisterName(additionalName);
                    }
                    adornNames.Clear();
                    (AssociatedObject.Content as Panel).Children.Remove(adornContent);
                    adornContent = null;
                }

                string xamlPath = GetAdornerXamlPath(AssociatedObject);

                if (File.Exists(xamlPath) == true)
                {
                    adornContent = FrameworkElementFromXamlFactory.Instance.GetFrameworkElement(xamlPath);
                    adornContent.Name = ADORN_CONTENT_NAME;
                    (AssociatedObject.Content as Panel).Children.Add(adornContent);
                    AssociatedObject.RegisterName(adornContent.Name, adornContent);
                    adornNames.Add(adornContent.Name);

                    // XamlReader で読み込むと、読み込んだ xaml のルート要素に NameScope が構築される。
                    // この NameScope を親にも詰める。

                    INameScopeDictionary gridNameScope = NameScope.GetNameScope(adornContent) as INameScopeDictionary;

                    foreach (KeyValuePair<string, object> nameScopeEntry in gridNameScope.AsEnumerable())
                    {
                        AssociatedObject.RegisterName(nameScopeEntry.Key, nameScopeEntry.Value);
                        adornNames.Add(nameScopeEntry.Key);
                    }
                }

                string codeBehindPath = GetCodeBehindPath(AssociatedObject);

                if (File.Exists(codeBehindPath) == true)
                {
                    adornContentCodeBehind = AssemblyFromCsFactory.Instance.GetNewInstance(
                        codeBehindPath,
                        GetCodeBehindClassName(AssociatedObject),
                        AssociatedObject);
                }

                string viewModelPath = GetViewModelPath(AssociatedObject);

                if (File.Exists(viewModelPath) == true)
                {
                    adornContentViewModel = AssemblyFromCsFactory.Instance.GetNewInstance(
                        viewModelPath,
                        GetAdornerViewModelClassName(AssociatedObject));

                    AssociatedObject.DataContext = adornContentViewModel;
                }
                else
                {
                    Type type = GetType().Assembly.GetType(GetViewModelClassName(AssociatedObject));
                    if (type != null)
                    {
                        AssociatedObject.DataContext = Activator.CreateInstance(type);
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

                (AssociatedObject.Content as Panel).Children.Add(adornErrorContent);
            }
        }

        /// <summary>
        /// 追加のコンテントの xaml ファイルのパスを返します。
        /// </summary>
        /// <param name="target">対象の <see cref="ContentControl"/>。</param>
        /// <returns>追加のコンテントの xaml ファイルのパス。</returns>
        private string GetAdornerXamlPath(ContentControl target)
        {
            string targetFullName = target.GetType().FullName; // AdornableWpfApp.Views.MainWindow

            Match match = Regex.Match(targetFullName, @"^(?<head>.*?)\.(?<folder>[^\.]+)\.(?<class>[^\.]+)$");

            return $@"{match.Groups["folder"].Value}\{match.Groups["head"].Value}.{match.Groups["folder"].Value}.{match.Groups["class"].Value}Adorner.xaml"; // Views\AdornableWpfApp.Views.MainWindowAdorner.xaml
        }

        private string GetCodeBehindPath(ContentControl target)
        {
            return $@"{GetAdornerXamlPath(target)}.cs"; // Views\AdornableWpfApp.Views.MainWindowAdorner.xaml.cs
        }

        private string GetCodeBehindClassName(ContentControl target)
        {
            return $@"{target.GetType().FullName}Adorner"; // AdornableWpfApp.Views.MainWindowAdorner
        }

        private string GetViewModelPath(ContentControl target)
        {
            string targetFullName = target.GetType().FullName; // AdornableWpfApp.Views.MainWindow

            Match match = Regex.Match(targetFullName, @"^(?<head>.*?)\.(?<folder>[^\.]+)\.(?<class>[^\.]+)$");

            return $@"ViewModels\{match.Groups["head"].Value}.ViewModels.{match.Groups["class"].Value}ViewModelAdorner.cs"; // ViewModels\AdornableWpfApp.ViewModels.MainWindowViewModelAdorner.cs
        }

        private string GetViewModelClassName(ContentControl target)
        {
            string targetFullName = target.GetType().FullName; // AdornableWpfApp.Views.MainWindow

            Match match = Regex.Match(targetFullName, @"^(?<head>.*?)\.(?<folder>[^\.]+)\.(?<class>[^\.]+)$");

            return $@"{match.Groups["head"].Value}.ViewModels.{match.Groups["class"].Value}ViewModel"; // AdornableWpfApp.ViewModels.MainWindowViewModel
        }

        private string GetAdornerViewModelClassName(ContentControl target)
        {
            return $@"{GetViewModelClassName(target)}Adorner"; // AdornableWpfApp.ViewModels.MainWindowViewModelAdorner
        }
    }
}
