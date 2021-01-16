﻿using Microsoft.CodeAnalysis;
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
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // 自動生成のアセンブリは対象外とする
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
            MetadataReference[] references = MetadataReferences.ToArray();

            // analyse and generate IL code from syntax tree
            CSharpCompilation compilation = CSharpCompilation.Create(
                $"{assemblyCacheEntry.guid}_{assemblyCacheEntry.generation}",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

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

                    foreach (Diagnostic diagnostic in failures)
                    {
                        sb.AppendLine($"{diagnostic.Id}: {diagnostic.GetMessage()}");
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
                }
            }

            return assemblyCacheEntry.assembly;
        }
    }
}
