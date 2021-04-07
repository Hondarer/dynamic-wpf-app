using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DynamicWpfApp.Utils
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

                return instance;
            }
        }

        private AssemblyFromCsFactory()
        {
        }

        public Assembly GetAssembly(string path)
        {
            // https://stackoverflow.com/questions/826398/is-it-possible-to-dynamically-compile-and-execute-c-sharp-code-fragments

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
                    return assemblyCacheEntry.assembly;
                }

                assemblyCacheEntry.generation++;
            }
            else
            {
                assemblyCacheEntry = new AssemblyCacheEntry();
            }

            assemblyCacheEntry.lastUpdated = File.GetLastWriteTimeUtc(absolutePath);

            SyntaxTree syntaxTree;
            using (StreamReader srXamlCs = new StreamReader(absolutePath))
            {
                syntaxTree = CSharpSyntaxTree.ParseText(srXamlCs.ReadToEnd());
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
            // MEMO: 依存するクラスは先に登録しておく必要がある。対象ファイルをまとめてコンパイルする方法も考えられる。
            foreach (AssemblyCacheEntry _assemblyCacheEntry in assemblyCache.Values)
            {
                if (_assemblyCacheEntry.Equals(assemblyCacheEntry))
                {
                    continue;
                }

                MetadataReferences.Add(_assemblyCacheEntry.metadataReference);
            }

            MetadataReference[] references = MetadataReferences.ToArray();

            // analyse and generate IL code from syntax tree
            CSharpCompilationOptions cSharpCompilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
#if !DEBUG
            cSharpCompilationOptions = cSharpCompilationOptions.WithOptimizationLevel(OptimizationLevel.Release);
#endif

            CSharpCompilation compilation = CSharpCompilation.Create(
                $"{assemblyCacheEntry.guid}_{assemblyCacheEntry.generation}",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: cSharpCompilationOptions);

            using (var ms = new MemoryStream())
            {
                EmitResult result = compilation.Emit(ms);

                if (result.Success == false)
                {
                    // handle exceptions
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    StringBuilder sb = new StringBuilder();

                    sb.AppendLine("Compilation error(s) has occurred.");
                    foreach (Diagnostic diagnostic in failures)
                    {
                        sb.AppendLine(diagnostic.ToString());
                    }

                    throw new Exception(sb.ToString());
                }
                else
                {
                    ms.Seek(0, SeekOrigin.Begin);

                    // この時点で assemblyCacheEntry.assembly が非 null の場合は、可能ならば解放したいが、
                    // .NET FW では難しいので放置して入れ替えることにする。連続稼働は考慮しない。

                    assemblyCacheEntry.assembly = Assembly.Load(ms.ToArray());

                    if (assemblyCache.ContainsValue(assemblyCacheEntry) == false)
                    {
                        assemblyCache.Add(absolutePath, assemblyCacheEntry);
                    }

                    ms.Seek(0, SeekOrigin.Begin);

                    // MetadataReference の生成はイメージからしか行えないので、このタイミングで保持しておく。
                    assemblyCacheEntry.metadataReference = MetadataReference.CreateFromStream(ms);
                }
            }

            return assemblyCacheEntry.assembly;
        }

        public object GetNewInstance(string assemblyPath, string classFullName, params object[] args)
        {
            // 例外が出る可能性がある
            Assembly assembly = GetAssembly(assemblyPath);
            // 見つからないとtypeがnullになる
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
