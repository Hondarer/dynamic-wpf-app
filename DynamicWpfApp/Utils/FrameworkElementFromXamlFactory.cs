using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Markup;

namespace DynamicWpfApp.Utils
{
    public class FrameworkElementFromXamlFactory
    {
        private class FrameworkElementCacheEntry
        {
            public Guid guid = Guid.NewGuid();
            public FrameworkElement frameworkElement;
            public int generation = 0;
            public DateTime lastUpdated;
        }

        private Dictionary<string, FrameworkElementCacheEntry> frameworkElementCache = new Dictionary<string, FrameworkElementCacheEntry>();

        private static readonly object lockObject = new object();

        private static FrameworkElementFromXamlFactory instance = null;

        public static FrameworkElementFromXamlFactory Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (lockObject)
                    {
                        if (instance == null)
                        {
                            instance = new FrameworkElementFromXamlFactory();
                        }
                    }
                }

                return instance;
            }
        }

        private FrameworkElementFromXamlFactory()
        {
        }

        public FrameworkElement GetFrameworkElement(string path)
        {
            if (File.Exists(path) == false)
            {
                throw new FileNotFoundException(path);
            }

            string absolutePath = Path.GetFullPath(path);

            FrameworkElementCacheEntry frameworkElementCacheEntry;

            if (frameworkElementCache.ContainsKey(absolutePath) == true)
            {
                frameworkElementCacheEntry = frameworkElementCache[absolutePath];

                if (frameworkElementCacheEntry.lastUpdated >= File.GetLastWriteTimeUtc(absolutePath))
                {
                    Debug.Print("Exists {0} updated at {1}, generation={2}", absolutePath, frameworkElementCacheEntry.lastUpdated.ToLocalTime(), frameworkElementCacheEntry.generation);
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

            Debug.Print("Load {0} updated at {1}, generation={2}", absolutePath, frameworkElementCacheEntry.lastUpdated.ToLocalTime(), frameworkElementCacheEntry.generation);

            using (StreamReader srImplXaml = new StreamReader(absolutePath))
            {
                try
                {
                    frameworkElementCacheEntry.frameworkElement = XamlReader.Load(srImplXaml.BaseStream) as FrameworkElement;
                }
                catch(Exception ex)
                {
                    throw new Exception($"Xaml parse error(s) has occurred in {absolutePath}.", ex);
                }
            }

            if (frameworkElementCache.ContainsValue(frameworkElementCacheEntry) == false)
            {
                frameworkElementCache.Add(absolutePath, frameworkElementCacheEntry);
            }

            return frameworkElementCacheEntry.frameworkElement;
        }
    }
}
