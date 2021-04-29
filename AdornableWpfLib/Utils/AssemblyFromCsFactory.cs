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
    /// <summary>
    /// ソースファイルからアセンブリを返す機能を提供します。
    /// </summary>
    public class AssemblyFromCsFactory
    {
        /// <summary>
        /// アセンブリのキャッシュ情報を表します。
        /// </summary>
        private class AssemblyCacheEntry
        {
            /// <summary>
            /// このキャッシュ情報の ID を保持します。このフィールドは読み取り専用です。
            /// </summary>
            public readonly Guid guid = Guid.NewGuid();

            /// <summary>
            /// アセンブリを保持します。
            /// </summary>
            public Assembly assembly;

            /// <summary>
            /// 世代を保持します。
            /// </summary>
            public int generation = 0;

            /// <summary>
            /// 各ソースファイルの最終更新日時を保持します。
            /// </summary>
            public Dictionary<string, DateTime> lastUpdatedDictionary = new Dictionary<string, DateTime>();

            /// <summary>
            /// 最終ビルド日時を保持します。
            /// </summary>
            public DateTime lastBuilt;

            /// <summary>
            /// <see cref="MetadataReference"/> を保持します。
            /// </summary>
            public MetadataReference metadataReference;

            /// <inheritdoc/>
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

            /// <inheritdoc/>
            public override int GetHashCode()
            {
                return guid.GetHashCode();
            }
        }

        #region シングルトン デザイン パターン

        /// <summary>
        /// シングルトン デザイン パターンのためのロックオブジェクトを保持します。
        /// </summary>
        private static readonly object lockObject = new object();

        /// <summary>
        /// <see cref="AssemblyFromCsFactory"/> のシングルトンインスタンスを保持します。
        /// </summary>
        private static AssemblyFromCsFactory instance = null;

        /// <summary>
        /// <see cref="AssemblyFromCsFactory"/> のシングルトンインスタンスを取得します。
        /// </summary>
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

        #endregion

        /// <summary>
        /// アセンブリのキャッシュを保持します。このフィールドは読み取り専用です。
        /// </summary>
        private readonly Dictionary<string, AssemblyCacheEntry> assemblyCache = new Dictionary<string, AssemblyCacheEntry>();

        /// <summary>
        /// <see cref="AssemblyFromCsFactory"/> の新しいインスタンスを初期化します。
        /// </summary>
        private AssemblyFromCsFactory()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }

        /// <summary>
        /// アセンブリを解決します。
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The event data.</param>
        /// <returns>The assembly that resolves the type, assembly, or resource; or null if the assembly cannot be resolved.</returns>
        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            // 既定のアセンブリ解決処理で
            // 動的生成されたアセンブリを Load しようとすると
            // FileNotFoundException が発生してしまうため、カスタム解決処理を組み入れる。

            // 動的生成されたアセンブリの要求であれば、このクラスからアセンブリを返す。
            lock (Instance)
            {
                foreach (AssemblyCacheEntry assemblyCacheEntry in instance.assemblyCache.Values)
                {
                    // アセンブリ生成中に #r で アセンブリを Load しようとすると、ここに到達する。
                    if (assemblyCacheEntry.assembly == null)
                    {
                        continue;
                    }

                    if (assemblyCacheEntry.assembly.FullName == args.Name)
                    {
                        return assemblyCacheEntry.assembly;
                    }
                }
            }

            // この判定の対象外。
            return null;
        }

        /// <summary>
        /// アセンブリを生成、またはキャッシュから返します。
        /// </summary>
        /// <param name="path">ソースファイルのパス。ファイル名にはワイルドカードが利用できます。</param>
        /// <returns>アセンブリ。</returns>
        /// <exception cref="Exception">アセンブリ生成に失敗した場合にスローされます。</exception>
        public Assembly CreateOrGetAssembly(string path)
        {
            string directoryName;
            string fileName;

            // ? や * が含まれている場合には Path クラスの処理ができないので、自前で分解する。
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

            // 対象のソースファイル群に展開
            List<string> absolutePaths = Directory.GetFiles(directoryName, fileName).ToList();
            if (absolutePaths.Count == 0)
            {
                throw new FileNotFoundException(path);
            }

            AssemblyCacheEntry assemblyCacheEntry;

            lock (this)
            {
                // 初回か、2 回目以降かを判定する。
                if (assemblyCache.ContainsKey(path) == true)
                {
                    assemblyCacheEntry = assemblyCache[path];

                    // 対象ソースファイルのうち、いずれか 1 つでも新しくなっていた場合は、アセンブリ再生成対象とする。
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

                    // 更新が無かった場合は、生成性しない。
                    // MEMO: このクラス内での生成順を見て、他のアセンブリを再生成したほうがよい場合もあるが、現在のところは行っていない。
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

                // 各ソースファイルの最終更新日時を保持する。
                foreach (string absolutePath in absolutePaths)
                {
                    if (assemblyCacheEntry.lastUpdatedDictionary.ContainsKey(absolutePath) == true)
                    {
                        assemblyCacheEntry.lastUpdatedDictionary[absolutePath] = File.GetLastWriteTimeUtc(absolutePath);
                    }
                    else
                    {
                        assemblyCacheEntry.lastUpdatedDictionary.Add(absolutePath, File.GetLastWriteTimeUtc(absolutePath));
                    }
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
                    using (StreamReader sr = new StreamReader(absolutePath))
                    {
                        // #r を解決する。このあと読み込み済みアセンブリを探索するので Load しておけばよい。

                        string text = sr.ReadToEnd();

                        Regex regex = new Regex("^\\s*?#r\\s+\"(?<assemblyString>.*?)\"\\s*?$", RegexOptions.Multiline);
                        MatchCollection matchCollection = regex.Matches(text);
                        if (matchCollection.Count > 0)
                        {
                            text = regex.Replace(text, "");

                            foreach (Match match in matchCollection)
                            {
                                try
                                {
                                    Assembly.Load(match.Groups["assemblyString"].Value);
                                }
                                catch (Exception ex)
                                {
                                    StringBuilder sb = new StringBuilder();

                                    sb.AppendLine($"Unable load assembly '{match.Groups["assemblyString"].Value}' in {absolutePath}.");
                                    sb.AppendLine(ex.ToString());

                                    throw new Exception(sb.ToString());
                                }
                            }
                        }

                        // SourceText と CSharpSyntaxTree を得る。
                        byte[] buffer = Encoding.GetEncoding("utf-8").GetBytes(text);
                        SourceText sourceText = SourceText.From(buffer, buffer.Count(), canBeEmbedded: true);
                        syntaxTrees.Add(CSharpSyntaxTree.ParseText(sourceText, parseOptions));

                        sourceTextDictionary.Add(absolutePath, sourceText);
                    }
                }

                // アセンブリ参照の処理
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
            }

            return assemblyCacheEntry.assembly;
        }

        /// <summary>
        /// 新しいインスタンスを取得します。
        /// </summary>
        /// <param name="assemblyPath">アセンブリのパス。</param>
        /// <param name="classFullName">クラスの名称。</param>
        /// <param name="args">コンストラクターの引数。</param>
        /// <returns>新しいインスタンス。</returns>
        /// <exception cref="Exception">インスタンス生成に失敗した場合にスローされます。</exception>
        public object GetNewInstance(string assemblyPath, string classFullName, params object[] args)
        {
            Assembly assembly = CreateOrGetAssembly(assemblyPath);

            // 見つからないと type が null になる
            Type type = assembly.GetType(classFullName);
            if (type == null)
            {
                throw new Exception($"Class not found: {classFullName}");
            }

            object obj = Activator.CreateInstance(type, args);

            return obj;
        }
    }
}
