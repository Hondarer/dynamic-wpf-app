using System.Reflection;
using System.Windows;

namespace AdornableWpfApp
{
    /// <summary>
    /// <see cref="App"/> の相互作用ロジックを提供します。
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // TODO:追加処理だけが利用しているアセンブリに関して、何らかの方法でアセンブリ参照をする必要がある
            // ここでは、読み込み済みのアセンブリを参照追加している処理を活用する。
            // #r "System.Text.Json" などの宣言を解釈するのが良い
            Assembly.Load("System.Text.Json");

            base.OnStartup(e);
        }
    }
}