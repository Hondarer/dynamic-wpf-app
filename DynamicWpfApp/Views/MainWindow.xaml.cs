using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        public MainWindow()
        {
            InitializeComponent();

            FrameworkElement content;
            using (StreamReader srXaml = new StreamReader(@"Views\MainWindowImpl.xaml"))
            {
                content = XamlReader.Load(srXaml.BaseStream) as FrameworkElement;

                if (Content is Grid)
                {
                    (Content as Grid).Children.Add(content);

                    LoadCodeBehind(content);

                    LoadViewModel();
                }
                else
                {
                    // Content isn't Grid.
                }
            }
        }

        // 以下はリファクタリング未

        public object LoadCodeBehind(FrameworkElement content)
        {
            // https://stackoverflow.com/questions/826398/is-it-possible-to-dynamically-compile-and-execute-c-sharp-code-fragments

            // define source code, then parse it (to the type used for compilation)
            SyntaxTree syntaxTree;
            using (StreamReader srXamlCs = new StreamReader(@"Views\MainWindowImpl.xaml.cs"))
            {
                syntaxTree = CSharpSyntaxTree.ParseText(srXamlCs.ReadToEnd());
            }

            // define other necessary objects for compilation

            //// このプロジェクトとしては参照しているはずなので、相対パスでもよいはず
            //string runtimePath
            //    = @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\{0}.dll";

            string assemblyName = System.IO.Path.GetRandomFileName();
            //MetadataReference[] references = new MetadataReference[]
            //{
            //    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            //    MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            //    MetadataReference.CreateFromFile(GetType().Assembly.Location),
            //    MetadataReference.CreateFromFile(string.Format(runtimePath, "mscorlib")),
            //    MetadataReference.CreateFromFile(string.Format(runtimePath, "System")),
            //    MetadataReference.CreateFromFile(string.Format(runtimePath, "System.Core")),
            //    MetadataReference.CreateFromFile(string.Format(runtimePath, "PresentationCore")),
            //    MetadataReference.CreateFromFile(string.Format(runtimePath, "PresentationFramework")),
            //    MetadataReference.CreateFromFile(string.Format(runtimePath, "WindowsBase"))
            //};

            List<MetadataReference> MetadataReferences = new List<MetadataReference>();
            foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // 自動生成のアセンブリは対象外とする
                if (string.IsNullOrEmpty(assembly.Location) != true)
                {
                    MetadataReferences.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
            }
            MetadataReference[] references = MetadataReferences.ToArray();

            // analyse and generate IL code from syntax tree
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var ms = new MemoryStream())
            {
                // write IL code into memory
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    // handle exceptions
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (Diagnostic diagnostic in failures)
                    {
                        Debug.WriteLine("{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
                    }
                }
                else
                {
                    // load this 'virtual' DLL so that we can use
                    ms.Seek(0, SeekOrigin.Begin);
                    Assembly assembly = Assembly.Load(ms.ToArray());

                    // create instance of the desired class and call the desired function
                    Type type = assembly.GetType("DynamicWpfApp.Views.MainWindowImpl");
                    object obj = Activator.CreateInstance(type, this, content);
                    //type.InvokeMember("Write",
                    //    BindingFlags.Default | BindingFlags.InvokeMethod,
                    //    null,
                    //    obj,
                    //    new object[] { "Hello World" });

                    return obj;
                }
            }

            return null;
        }

        public void LoadViewModel()
        {
            // https://stackoverflow.com/questions/826398/is-it-possible-to-dynamically-compile-and-execute-c-sharp-code-fragments

            // define source code, then parse it (to the type used for compilation)
            SyntaxTree syntaxTree;
            using (StreamReader srXamlCs = new StreamReader(@"ViewModels\MainWindowViewModelImpl.cs"))
            {
                syntaxTree = CSharpSyntaxTree.ParseText(srXamlCs.ReadToEnd());
            }

            // define other necessary objects for compilation

            //// このプロジェクトとしては参照しているはずなので、相対パスでもよいはず
            //string runtimePath
            //    = @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\{0}.dll";

            string assemblyName = System.IO.Path.GetRandomFileName();
            //MetadataReference[] references = new MetadataReference[]
            //{
            //    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            //    MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            //    MetadataReference.CreateFromFile(GetType().Assembly.Location),
            //    MetadataReference.CreateFromFile(string.Format(runtimePath, "mscorlib")),
            //    MetadataReference.CreateFromFile(string.Format(runtimePath, "System")),
            //    MetadataReference.CreateFromFile(string.Format(runtimePath, "System.Core")),
            //    MetadataReference.CreateFromFile(string.Format(runtimePath, "PresentationCore")),
            //    MetadataReference.CreateFromFile(string.Format(runtimePath, "PresentationFramework")),
            //    MetadataReference.CreateFromFile(string.Format(runtimePath, "WindowsBase"))
            //};

            List<MetadataReference> MetadataReferences = new List<MetadataReference>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // 自動生成のアセンブリは対象外とする
                if (string.IsNullOrEmpty(assembly.Location) != true)
                {
                    MetadataReferences.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
            }
            MetadataReference[] references = MetadataReferences.ToArray();

            // analyse and generate IL code from syntax tree
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var ms = new MemoryStream())
            {
                // write IL code into memory
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    // handle exceptions
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (Diagnostic diagnostic in failures)
                    {
                        Debug.WriteLine("{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
                    }
                }
                else
                {
                    // load this 'virtual' DLL so that we can use
                    ms.Seek(0, SeekOrigin.Begin);
                    Assembly assembly = Assembly.Load(ms.ToArray());

                    // create instance of the desired class and call the desired function
                    Type type = assembly.GetType("DynamicWpfApp.ViewModels.MainWindowViewModelImpl");
                    object obj = Activator.CreateInstance(type);
                    //type.InvokeMember("Write",
                    //    BindingFlags.Default | BindingFlags.InvokeMethod,
                    //    null,
                    //    obj,
                    //    new object[] { "Hello World" });

                    DataContext = obj;
                }
            }
        }
    }
}
