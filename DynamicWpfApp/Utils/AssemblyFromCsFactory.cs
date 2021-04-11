using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace AdornableWpfApp.Utils
{
    public class AssemblyFromCsFactory
    {
        private class AssemblyCacheEntry
        {
            public Guid guid = Guid.NewGuid();
            public Assembly assembly;
            public int generation = 0;
            public DateTime lastUpdated;
            public MetadataReference metadataReference;

            public override bool Equals(object obj)
            {
                if (obj == null)
                {
                    return false;
                }

                if ((obj is AssemblyCacheEntry) != true)
                {
                    return false;
                }

                return guid == (obj as AssemblyCacheEntry).guid;
            }

            public override int GetHashCode()
            {
                return guid.GetHashCode();
            }
        }

        private Dictionary<string, AssemblyCacheEntry> assemblyCache = new Dictionary<string, AssemblyCacheEntry>();

        private static readonly object lockObject = new object();

        private static AssemblyFromCsFactory instance = null;

        public static AssemblyFromCsFactory Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (lockObject)
                    {
                        if (instance == null)
                        {
                            instance = new AssemblyFromCsFactory();
                        }
                    }
                }

                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

                return instance;
            }
        }

        private AssemblyFromCsFactory()
        {
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            // 既定のアセンブリ解決処理で動的生成されたアセンブリを Load しようとすると、
            // FileNotFoundException が発生してしまうため、カスタム解決処理を組み入れる。

            // 動的生成されたアセンブリの要求であれば、このクラスからアセンブリを返す。
            foreach (AssemblyCacheEntry assemblyCacheEntry in instance.assemblyCache.Values)
            {
                if (assemblyCacheEntry.assembly.FullName == args.Name)
                {
                    return assemblyCacheEntry.assembly;
                }
            }

            // この判定の対象外。
            return null;
        }

        public Assembly GetAssembly(string path)
        {
            if (File.Exists(path) == false)
            {
                throw new FileNotFoundException(path);
            }

            string absolutePath = Path.GetFullPath(path);

            AssemblyCacheEntry assemblyCacheEntry;

            if (assemblyCache.ContainsKey(absolutePath) == true)
            {
                assemblyCacheEntry = assemblyCache[absolutePath];

                if (assemblyCacheEntry.lastUpdated >= File.GetLastWriteTimeUtc(absolutePath))
                {
                    Debug.Print("Exists {0} updated at {1}, generation={2}", absolutePath, assemblyCacheEntry.lastUpdated.ToLocalTime(), assemblyCacheEntry.generation);
                    return assemblyCacheEntry.assembly;
                }

                assemblyCacheEntry.generation++;

                // この時点で assemblyCacheEntry.assembly が非 null の場合は、可能ならば解放したいが、
                // .NET FW では技術的に困難なため、解放はせずに入れ替える。
                assemblyCacheEntry.assembly = null;
            }
            else
            {
                assemblyCacheEntry = new AssemblyCacheEntry();
            }

            assemblyCacheEntry.lastUpdated = File.GetLastWriteTimeUtc(absolutePath);

            Debug.Print("Start compile {0} updated at {1}, generation={2}", absolutePath, assemblyCacheEntry.lastUpdated.ToLocalTime(), assemblyCacheEntry.generation);

            SourceText sourceText;
            SyntaxTree syntaxTree;
            using (FileStream sourceStream = new FileStream(absolutePath, FileMode.Open))
            {
                sourceText = SourceText.From(sourceStream, canBeEmbedded: true);
                syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
            }

            List<MetadataReference> MetadataReferences = new List<MetadataReference>();

            // 自身のアセンブリに読み込まれているアセンブリは追加コードでも対象にする
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // 動的生成のアセンブリはパスがないので、ファイルからの追加は対象外とする
                if (string.IsNullOrEmpty(assembly.Location) != true)
                {
                    MetadataReferences.Add(MetadataReference.CreateFromFile(assembly.Location));
                }

                // その他の追加方法例
                //
                // 型から
                // MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
                //
                // パスから
                // string runtimePath
                //     = @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\{0}.dll";
                // MetadataReference.CreateFromFile(string.Format(runtimePath, "WindowsBase")
            }

            // 自動生成のアセンブリの追加
            // MEMO: 依存するクラスは当然、先に生成・登録しておく必要がある。
            //       対象ファイルをまとめてコンパイルする方法も考えられるが、現状は、ソース一本一本を別アセンブリとして管理している。
            foreach (AssemblyCacheEntry _assemblyCacheEntry in assemblyCache.Values)
            {
                // 自身は対象外
                if (_assemblyCacheEntry.Equals(assemblyCacheEntry))
                {
                    continue;
                }

                MetadataReferences.Add(_assemblyCacheEntry.metadataReference);
            }

            MetadataReference[] references = MetadataReferences.ToArray();

            // analyse and generate IL code from syntax tree
            CSharpCompilationOptions cSharpCompilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
#if DEBUG
            cSharpCompilationOptions = cSharpCompilationOptions.WithOptimizationLevel(OptimizationLevel.Debug);
#else
            cSharpCompilationOptions = cSharpCompilationOptions.WithOptimizationLevel(OptimizationLevel.Release);
#endif

            string assemblyName = $"{Path.GetFileNameWithoutExtension(absolutePath)}_{assemblyCacheEntry.guid}_{assemblyCacheEntry.generation}";

            CSharpCompilation compilation = CSharpCompilation.Create(
                $"{assemblyName}.dll",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: cSharpCompilationOptions);

            using (MemoryStream assemblyStream = new MemoryStream())
            using (MemoryStream symbolsStream = new MemoryStream())
            {
                EmitOptions emitOptions = new EmitOptions(
                    debugInformationFormat: DebugInformationFormat.PortablePdb,
                    pdbFilePath: $"{assemblyName}.pdb");

                List<EmbeddedText> embeddedTexts = new List<EmbeddedText>()
                    {
                        EmbeddedText.FromSource(absolutePath, sourceText),
                    };

                EmitResult result = compilation.Emit(
                    peStream: assemblyStream,
                    pdbStream: symbolsStream,
                    embeddedTexts: embeddedTexts,
                    options: emitOptions);

                if (result.Success == false)
                {
                    // handle exceptions
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    StringBuilder sb = new StringBuilder();

                    sb.AppendLine($"Compilation error(s) has occurred in {absolutePath}.");
                    foreach (Diagnostic diagnostic in failures)
                    {
                        sb.AppendLine(diagnostic.ToString());
                    }

                    throw new Exception(sb.ToString());
                }
                else
                {
                    assemblyStream.Seek(0, SeekOrigin.Begin);
                    symbolsStream.Seek(0, SeekOrigin.Begin);

                    assemblyCacheEntry.assembly = Assembly.Load(assemblyStream.ToArray(), symbolsStream.ToArray());

                    if (assemblyCache.ContainsValue(assemblyCacheEntry) == false)
                    {
                        assemblyCache.Add(absolutePath, assemblyCacheEntry);
                    }

                    assemblyStream.Seek(0, SeekOrigin.Begin);

                    // MetadataReference の生成はイメージからしか行うべきなので、このタイミングで保持しておく。
                    // (Assembly から生成する方法は Obsolete になっている。)
                    assemblyCacheEntry.metadataReference = MetadataReference.CreateFromStream(assemblyStream);
                }
            }

            Debug.Print("Done compile {0} updated at {1}, generation={2}", absolutePath, assemblyCacheEntry.lastUpdated.ToLocalTime(), assemblyCacheEntry.generation);
            return assemblyCacheEntry.assembly;
        }

        public object GetNewInstance(string assemblyPath, string classFullName, params object[] args)
        {
            // 例外が出る可能性がある
            Assembly assembly = GetAssembly(assemblyPath);

            // 見つからないと type が null になる
            Type type = assembly.GetType(classFullName);
            if (type == null)
            {
                throw new Exception($"Class not found: {classFullName}");
            }

            // 例外が出る可能性がある
            object obj = Activator.CreateInstance(type, args);

            return obj;
        }
    }
}
