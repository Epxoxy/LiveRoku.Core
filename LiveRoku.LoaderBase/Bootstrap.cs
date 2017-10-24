using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using LiveRoku.Base;
using LiveRoku.Base.Plugin;

namespace LiveRoku.LoaderBase {

    public class Bootstrap : IDisposable {
        public string BaseDirectory => baseDir;
        private readonly string baseDir;
        private readonly string dataDir;
        private readonly string pluginDir;
        private readonly string coreDir;
        private readonly string pathOfConfig;
        private readonly string pathOfExtra;
        private IDictionary<string, Assembly> assemblies;
        private LoadContextBase baseCtx;

        public Bootstrap (string baseDir) {
            if (string.IsNullOrEmpty(baseDir) || !Directory.Exists(baseDir))
                throw new ArgumentException ($"parameter {nameof(baseDir)} must be a readable directory.");
            this.baseDir = baseDir;
            this.dataDir = Path.Combine (baseDir + "data");
            this.pluginDir = Path.Combine (baseDir + "plugins");
            this.coreDir = Path.Combine (baseDir + "core");
            this.pathOfConfig = Path.Combine (dataDir, "application.plugins.config");
            this.pathOfExtra = Path.Combine(dataDir, "application.extra.config");
            makeDirectoryExist (pluginDir);
            if (!makeDirectoryExist (coreDir))
                throw new ArgumentException ($"parameter {nameof(coreDir)} must be a readable directory.");
            assemblies = new Dictionary<string, Assembly> ();
            AppDomain.CurrentDomain.AssemblyResolve += findCache;
            AppDomain.CurrentDomain.AssemblyLoad += cacheAssembly;
        }

        public void Dispose () {
            AppDomain.CurrentDomain.AssemblyResolve -= findCache;
            AppDomain.CurrentDomain.AssemblyLoad -= cacheAssembly;
        }

        private LoadContextBase reloadCtxBase () {
            var ctx = new LoadContextBase (baseDir, dataDir, pathOfConfig, pathOfExtra);
            //Load core part
            Type core = null;
            if ((core = findCoreImpl (coreDir)) == null)
                throw new Exception ("Core.dll cannot be load.");
            ctx.CoreType = core;
            try {
                ctx.PluginImpls = loadTypesImplFromDirectory<IPlugin> (pluginDir, "*.dll") ? .ToList ();
                ctx.AllSettings = PluginHelper.unwrapAllSettings(dataDir, "*.txt");
                var text = FileHelper.readText (Path.Combine (dataDir, pathOfConfig));
                ctx.InitConfigs = FileHelper.deserializeFromJson<Dictionary<string, PluginConfiguration>> (text);
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine (e.ToString ());
            }
            ctx.AllSettings = ctx.AllSettings ?? new Dictionary<string, SettingItemCollection> ();
            ctx.PluginImpls = ctx.PluginImpls ?? new List<Type> ();
            ctx.InitConfigs = ctx.InitConfigs ?? new Dictionary<string, PluginConfiguration> ();
            foreach (var impl in ctx.PluginImpls) {
                if (!ctx.InitConfigs.ContainsKey (impl.FullName)) {
                    ctx.InitConfigs.Add (impl.FullName, new PluginConfiguration {
                        HostType = impl,
                            Priority = ctx.InitConfigs.Count,
                    });
                }
            }
            ctx.LoadOk = true;
            return ctx;
        }

        public LoadContext makeFor (IFetchArgsHost argsHost, bool reload = false) {
            if (argsHost == null) {
                throw new ArgumentNullException (nameof (argsHost));
            }
            if (baseCtx == null || reload) {
                baseCtx = reloadCtxBase ();
            }
            var instance = Activator.CreateInstance (baseCtx.CoreType, argsHost);
            if (instance == null) {
                throw new Exception ("Core implement cannot be create.");
            }
            //make plugins
            var plugins = new List<IPlugin> ();
            if (baseCtx.PluginImpls.Count > 0) {
                var orderedList = baseCtx.InitConfigs.Values.ToList ().OrderBy (config => config.Priority);
                var invalidFileNameChars = new string (Path.GetInvalidFileNameChars ());
                var regFileName = new Regex (string.Format ("[{0}]", Regex.Escape (invalidFileNameChars)));
                foreach (var config in orderedList) {
                    //make instance
                    if (!config.IsEnable) continue;
                    try {
                        var plugin = (IPlugin) Activator.CreateInstance (config.HostType);
                        plugins.Add (plugin);
                        var key = plugin.Token ?? plugin.GetType ().FullName;
                        //set configuration
                        config.AccessToken = key;
                        if (string.IsNullOrEmpty (config.ConfigName))
                            config.ConfigName = regFileName.Replace (key, "").ToLower ()+".txt";
                        //restore setting
                        if (baseCtx.AllSettings.TryGetValue (key, out SettingItemCollection my)) {
                            PluginHelper.applySettings (plugin, my.UnwarppedSettings);
                        }
                    } catch (Exception e) {
                        System.Diagnostics.Debug.WriteLine (e.StackTrace);
                    }
                }
            }
            return new LoadContext (baseDir, dataDir, pathOfConfig, pathOfExtra) {
                Fetcher = instance as ILiveFetcher,
                    Plugins = plugins,
                    AllSettings = baseCtx.AllSettings,
                    InitConfigs = baseCtx.InitConfigs,
                    PluginImpls = baseCtx.PluginImpls
            };
        }

        //Assembly help
        private void cacheAssembly (object sender, AssemblyLoadEventArgs args) {
            var assembly = args.LoadedAssembly;
            System.Diagnostics.Debug.WriteLine ($"Loading Assembly {args.LoadedAssembly.Location}");
            if (assemblies.ContainsKey (assembly.FullName))
                assemblies[assembly.FullName] = assembly;
            else assemblies.Add (assembly.FullName, assembly);
        }

        private Assembly findCache (object sender, ResolveEventArgs args) {
            /*if (assemblyCache.TryGetValue (args.Name, out Assembly assembly))
                return assembly;
            return null;*/
            // you may not want to use First() here, consider FirstOrDefault() as well
            return (from a in AppDomain.CurrentDomain.GetAssemblies () where a.GetName ().FullName == args.Name select a).FirstOrDefault ();
        }

        //Directory check
        private bool makeDirectoryExist (string path) {
            try {
                return (!string.IsNullOrEmpty (path) && Directory.Exists (path)) 
                    || Directory.CreateDirectory(path).Exists;
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLineIf (e != null, e.ToString ());
            }
            return false;
        }

        //Implement check
        private Type findCoreImpl (string dir) {
            var target = default (Type);
            try {
                var files = Directory.GetFiles (dir);
                if (files.Length < 1) return target;
                foreach (var file in files) {
                    target = loadTypesImplFromFile<ILiveFetcher> (files.First ()).FirstOrDefault ();
                    if (target == null) continue;
                    return target;
                }
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLineIf (e != null, e.ToString ());
            }
            return target;
        }

        private IEnumerable<Type> loadTypesImplFromDirectory<T> (string dir, string searchPattern) {
            if (!Directory.Exists (dir)) return null;
            var result = new List<Type> ();
            foreach (var file in Directory.EnumerateFiles (dir, searchPattern)) {
                IEnumerable<Type> types = null;
                try {
                    types = loadTypesImplFromFile<T> (file);
                } catch (Exception e) {
                    System.Diagnostics.Debug.WriteLine (e.StackTrace);
                }
                if (types == null) continue;
                result.AddRange (types);
            }
            return result;
        }

        private IEnumerable<Type> loadTypesImplFromFile<T> (string path) {
            if (File.Exists (path) && path.EndsWith (".dll", true, null)) {
                var name = AssemblyName.GetAssemblyName (path);
                if (!assemblies.TryGetValue (name.FullName, out Assembly assembly)) {
                    assembly = Assembly.LoadFrom (path);
                }
                var types = assembly?.GetTypes ();
                if (types != null && types.Length > 0) {
                    return types.Where (type => type.IsClass && !type.IsAbstract && typeof (T).IsAssignableFrom (type));
                }
            }
            return null;
        }

    }
}