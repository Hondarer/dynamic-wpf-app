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
using System.Text.RegularExpressions;

namespace AdornableWpfLib.Utils
{
    public class AssemblyFromCsFactory
    {
        private class AssemblyCacheEntry
        {
            public Guid guid = Guid.NewGuid();
            public Assembly assembly;
            public int generation = 0;
            public Dictionary<string, DateTime> lastUpdatedDictionary = new Dictionary<string, DateTime>();
            public DateTime lastBuilt;
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

        private readonly Dictionary<string, AssemblyCacheEntry> assemblyCache = new Dictionary<string, AssemblyCacheEntry>();

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
            // 既定のアセンブリ解決処理で
            // 動的生成されたアセンブリを Load しようとすると
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

        public Assembly CreateOrGetAssembly(string path)
        {
            string directoryName;
            string fileName;

            if (path.Contains(@"\") == true)
            {
                int lastIndex = path.LastIndexOf(@"\");
                directoryName = Path.GetFullPath(path.Substring(0, lastIndex + 1));
                fileName = path.Substring(lastIndex + 1);
            }
            else
            {
                directoryName = Directory.GetCurrentDirectory();
                fileName = path;
            }

            List<string> absolutePaths = Directory.GetFiles(directoryName, fileName).ToList();
            if (absolutePaths.Count == 0)
            {
                throw new FileNotFoundException(path);
            }

            AssemblyCacheEntry assemblyCacheEntry;

            if (assemblyCache.ContainsKey(path) == true)
            {
                assemblyCacheEntry = assemblyCache[path];

                bool containUpdated = false;
                foreach (string absolutePath in absolutePaths)
                {
                    DateTime lastWriteTime = File.GetLastWriteTimeUtc(absolutePath);
                    if (assemblyCacheEntry.lastUpdatedDictionary[absolutePath] < lastWriteTime)
                    {
                        containUpdated = true;
                        break;
                    }
                }

                if (containUpdated == false)
                {
                    Debug.Print("Latest {0} updated at {1}, generation={2}", path, assemblyCacheEntry.lastBuilt.ToLocalTime(), assemblyCacheEntry.generation);
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

            foreach (string absolutePath in absolutePaths)
            {
                assemblyCacheEntry.lastUpdatedDictionary.Add(absolutePath, File.GetLastWriteTimeUtc(absolutePath));
            }

            Debug.Print("Start compile {0} updated at {1}, generation={2}", path, assemblyCacheEntry.lastBuilt.ToLocalTime(), assemblyCacheEntry.generation);

            Dictionary<string, SourceText> sourceTextDictionary = new Dictionary<string, SourceText>();
            List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();

            CSharpParseOptions parseOptions = new CSharpParseOptions();
#if DEBUG
            parseOptions = parseOptions.WithPreprocessorSymbols(CSharpCommandLineParser.ParseConditionalCompilationSymbols("DEBUG;TRACE", out IEnumerable<Diagnostic> diagnostics));
#endif

            foreach (string absolutePath in absolutePaths)
            {
                using (FileStream sourceStream = new FileStream(absolutePath, FileMode.Open))
                {
                    SourceText sourceText = SourceText.From(sourceStream, canBeEmbedded: true);
                    syntaxTrees.Add(CSharpSyntaxTree.ParseText(sourceText, parseOptions));

                    sourceTextDictionary.Add(absolutePath, sourceText);
                }
            }

            List<MetadataReference> MetadataReferences = new List<MetadataReference>();

            // 自身のアセンブリに読み込まれているアセンブリを参照可能にする
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // 動的生成のアセンブリはパスがないので本処理では追加できない。
                // 後で追加するのでスキップする。
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

            // 動的生成のアセンブリを参照可能にする
            // MEMO: 依存するクラスは、先に生成・登録しておく必要がある。
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

            string assemblyName = $"{Regex.Replace(path, @"[\\:\.\*\?]", "-")}_{assemblyCacheEntry.guid}_{assemblyCacheEntry.generation}";

            CSharpCompilation compilation = CSharpCompilation.Create(
                $"{assemblyName}.dll",
                syntaxTrees: syntaxTrees,
                references: references,
                options: cSharpCompilationOptions);

            using (MemoryStream assemblyStream = new MemoryStream())
            using (MemoryStream symbolsStream = new MemoryStream())
            {
                EmitOptions emitOptions = new EmitOptions(
                    debugInformationFormat: DebugInformationFormat.PortablePdb,
                    pdbFilePath: $"{assemblyName}.pdb");

                List<EmbeddedText> embeddedTexts = new List<EmbeddedText>();
                foreach (string absolutePath in absolutePaths)
                {
                    embeddedTexts.Add(EmbeddedText.FromSource(absolutePath, sourceTextDictionary[absolutePath]));
                }

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

                    sb.AppendLine($"Compilation error(s) has occurred in {path}.");
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
                        assemblyCache.Add(path, assemblyCacheEntry);
                    }

                    assemblyStream.Seek(0, SeekOrigin.Begin);

                    // MetadataReference の生成はイメージから行うべきなので、このタイミングで保持しておく。
                    // (Assembly から生成する方法は Obsolete になっている。)
                    assemblyCacheEntry.metadataReference = MetadataReference.CreateFromStream(assemblyStream);
                }
            }

            assemblyCacheEntry.lastBuilt = DateTime.UtcNow;
            Debug.Print("Done compile {0} updated at {1}, generation={2}", path, assemblyCacheEntry.lastBuilt.ToLocalTime(), assemblyCacheEntry.generation);

            return assemblyCacheEntry.assembly;
        }

        public object GetNewInstance(string assemblyPath, string classFullName, params object[] args)
        {
            // 例外が出る可能性がある
            Assembly assembly = CreateOrGetAssembly(assemblyPath);

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
