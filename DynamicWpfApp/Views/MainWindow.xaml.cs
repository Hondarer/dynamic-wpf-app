using AdornableWpfLib.Behaviors;
using System.Windows;
using System.Windows.Interactivity;

namespace AdornableWpfApp.Views
{
    /// <summary>
    /// <see cref="MainWindow"/> の相互作用ロジックを提供します。
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// <see cref="MainWindow"/> の新しいインスタンスを生成します。
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // コードビハインドでビヘイビアを追加する場合
            // (継承されるようなクラスで、xaml が書けない際に利用する)
            //Interaction.GetBehaviors(this).Add(new AdornableBehavior());
        }
    }
}
