using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Markup;

namespace AdornableWpfLib.Utils
{
    /// <summary>
    /// XAML から <see cref="FrameworkElement"/> を返す機能を提供します。
    /// </summary>
    public class FrameworkElementFromXamlFactory
    {
        /// <summary>
        /// <see cref="FrameworkElement"/> のキャッシュ情報を表します。
        /// </summary>
        private class FrameworkElementCacheEntry
        {
            /// <summary>
            /// <see cref="FrameworkElement"/> を保持します。
            /// </summary>
            public FrameworkElement frameworkElement;

            /// <summary>
            /// 世代を保持します。
            /// </summary>
            public int generation = 0;

            /// <summary>
            /// XAML の最終更新日時を保持します。
            /// </summary>
            public DateTime lastUpdated;
        }

        #region シングルトン デザイン パターン

        /// <summary>
        /// <see cref="FrameworkElementFromXamlFactory"/> のシングルトンインスタンスを保持します。
        /// </summary>
        private static FrameworkElementFromXamlFactory instance = null;

        /// <summary>
        /// <see cref="FrameworkElementFromXamlFactory"/> のシングルトンインスタンスを取得します。
        /// </summary>
        public static FrameworkElementFromXamlFactory Instance
        {
            get
            {
                if (instance == null)
                {
                    // MEMO: 本クラスは UI スレッド向けのため、マルチスレッドの考慮は不要。
                    instance = new FrameworkElementFromXamlFactory();
                }

                return instance;
            }
        }

        /// <summary>
        /// <see cref="FrameworkElementFromXamlFactory"/> の新しいインスタンスを初期化します。
        /// </summary>
        private FrameworkElementFromXamlFactory()
        {
        }

        #endregion

        /// <summary>
        /// <see cref="FrameworkElement"/> のキャッシュを保持します。このフィールドは読み取り専用です。
        /// </summary>
        private readonly Dictionary<string, FrameworkElementCacheEntry> frameworkElementCache = new Dictionary<string, FrameworkElementCacheEntry>();

        /// <summary>
        /// <see cref="FrameworkElement"/> を生成、またはキャッシュから返します。
        /// </summary>
        /// <param name="path">XAML のパス。</param>
        /// <returns><see cref="FrameworkElement"/>。</returns>
        /// <exception cref="Exception"><see cref="FrameworkElement"/> 生成に失敗した場合にスローされます。</exception>
        public FrameworkElement CreateOrGetFrameworkElement(string path)
        {
            if (File.Exists(path) == false)
            {
                throw new FileNotFoundException(path);
            }

            string absolutePath = Path.GetFullPath(path);

            FrameworkElementCacheEntry frameworkElementCacheEntry;

            // MEMO: 本クラスは UI スレッド向けのため、マルチスレッドの考慮は不要。

            if (frameworkElementCache.ContainsKey(absolutePath) == true)
            {
                frameworkElementCacheEntry = frameworkElementCache[absolutePath];

                if (frameworkElementCacheEntry.lastUpdated >= File.GetLastWriteTimeUtc(absolutePath))
                {
                    Debug.Print("Latest {0} updated at {1}, generation={2}", absolutePath, frameworkElementCacheEntry.lastUpdated.ToLocalTime(), frameworkElementCacheEntry.generation);
                    return frameworkElementCacheEntry.frameworkElement;
                }

                frameworkElementCacheEntry.generation++;
                frameworkElementCacheEntry.frameworkElement = null;
            }
            else
            {
                frameworkElementCacheEntry = new FrameworkElementCacheEntry();
            }

            frameworkElementCacheEntry.lastUpdated = File.GetLastWriteTimeUtc(absolutePath);

            Debug.Print("Start parse {0} updated at {1}, generation={2}", absolutePath, frameworkElementCacheEntry.lastUpdated.ToLocalTime(), frameworkElementCacheEntry.generation);

            using (StreamReader srImplXaml = new StreamReader(absolutePath))
            {
                try
                {
                    frameworkElementCacheEntry.frameworkElement = XamlReader.Load(srImplXaml.BaseStream) as FrameworkElement;
                }
                catch (Exception ex)
                {
                    throw new Exception($"XAML parse error(s) has occurred in {absolutePath}.", ex);
                }
            }

            if (frameworkElementCache.ContainsValue(frameworkElementCacheEntry) == false)
            {
                frameworkElementCache.Add(absolutePath, frameworkElementCacheEntry);
            }

            Debug.Print("Done parse {0} updated at {1}, generation={2}", absolutePath, frameworkElementCacheEntry.lastUpdated.ToLocalTime(), frameworkElementCacheEntry.generation);
            return frameworkElementCacheEntry.frameworkElement;
        }
    }
}
