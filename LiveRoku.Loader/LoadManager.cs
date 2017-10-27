using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using LiveRoku.Base;
using LiveRoku.Base.Plugin;

namespace LiveRoku.Loader {

    public class LoadManager : IDisposable {
        public string BaseDirectory => baseDir;
        private readonly string baseDir;
        private readonly string dataDir;
        private readonly string pluginDir;
        private readonly string coreDir;
        private const string appDataFileName = "app.data";
        private IDictionary<string, Assembly> assemblies;
        private LoadContextBase baseCtx;

        public LoadManager (string baseDir) {
            if (string.IsNullOrEmpty (baseDir) || !Directory.Exists (baseDir))
                throw new ArgumentException ($"parameter {nameof(baseDir)} must be a readable directory.");
            this.baseDir = baseDir;
            this.dataDir = Path.Combine (baseDir + "data");
            this.pluginDir = Path.Combine (baseDir + "plugins");
            this.coreDir = Path.Combine (baseDir + "core");
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
            //Load core part
            Type coreType = null;
            IEnumerable<Type> pluginTypes = null;
            ContextLoadConfig appLocalData = null;
            var extraSettings = new Dictionary<string, SettingsSection> ();
            if ((coreType = findCoreImpl (coreDir)) == null)
                throw new Exception ("Core.dll cannot be load.");
            //Get plugin types
            runSafely (() => {
                pluginTypes = getTypesImplFromDirectory<IPlugin> (pluginDir, "*.dll");
            });
            //Get app local data
            runSafely (() => {
                appLocalData = FileHelper.deserializeFromPath<ContextLoadConfig> (Path.Combine (dataDir, appDataFileName));
            });
            //Get extra settings
            foreach (var file in FileHelper.safelyGetFiles (dataDir, "*.txt")) {
                runSafely (() => {
                    var collection = FileHelper.deserializeFromPath<SettingsSection> (file);
                    if (collection != null)
                        extraSettings.Add (collection.AccessKey, collection);
                });
            }
            //set context config
            appLocalData = appLocalData ?? new ContextLoadConfig ();
            appLocalData.ExtraSettings = extraSettings;
            //set context
            var ctx = new LoadContextBase (dataDir, appDataFileName) {
                CoreType = coreType,
                AppLocalData = appLocalData,
                PluginTypes = pluginTypes.ToList ()
            };
            foreach (var impl in ctx.PluginTypes) {
                if (!ctx.AppLocalData.AppConfigs.ContainsKey (impl.FullName)) {
                    ctx.AppLocalData.AppConfigs.Add (impl.FullName, new PluginConfig {
                        HostType = impl,
                            Priority = ctx.AppLocalData.AppConfigs.Count,
                    });
                }
            }
            ctx.LoadOk = true;
            return ctx;
        }

        public LoadContextBase initCtxBase (bool reload = false) {
            if (baseCtx == null || reload) {
                baseCtx = reloadCtxBase ();
            }
            return baseCtx;
        }

        public LoadContext create (IFetchArgsHost argsHost, bool reload = false) {
            if (argsHost == null) {
                throw new ArgumentNullException (nameof (argsHost));
            }
            initCtxBase (reload);
            var instance = Activator.CreateInstance (baseCtx.CoreType, argsHost);
            if (instance == null) {
                throw new Exception ("Core implement cannot be create.");
            }
            //make plugins
            var plugins = new List<IPlugin> ();
            if (baseCtx.PluginTypes.Count > 0) {
                var orderedList = baseCtx.AppLocalData.AppConfigs.Values.ToList ().OrderBy (config => config.Priority);
                var invalidFileNameChars = new string (Path.GetInvalidFileNameChars ());
                var regFileName = new Regex (string.Format ("[{0}]", Regex.Escape (invalidFileNameChars)));
                foreach (var config in orderedList) {
                    //make instance
                    if (!config.IsEnable) continue;
                    runSafely (() => {
                        var plugin = (IPlugin) Activator.CreateInstance (config.HostType);
                        plugins.Add (plugin);
                        var key = plugin.Token ?? plugin.GetType ().FullName;
                        //set configuration
                        config.AccessToken = key;
                        if (string.IsNullOrEmpty (config.ConfigName))
                            config.ConfigName = regFileName.Replace (key, "").ToLower () + ".txt";
                        //restore setting
                        if (baseCtx.AppLocalData.ExtraSettings.TryGetValue (key, out SettingsSection settings)) {
                            PluginHelper.applySettings (plugin, settings.Items);
                        }
                    });
                }
            }
            return new LoadContext (dataDir, appDataFileName) {
                Fetcher = instance as ILiveFetcher,
                    Plugins = plugins,
                    AppLocalData = baseCtx.AppLocalData,
                    CoreType = baseCtx.CoreType,
                    PluginTypes = baseCtx.PluginTypes,
                    LoadOk = true
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
                return (!string.IsNullOrEmpty (path) && Directory.Exists (path)) ||
                    Directory.CreateDirectory (path).Exists;
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLineIf (e != null, e.ToString ());
            }
            return false;
        }

        //Implement check
        private Type findCoreImpl (string dir) {
            return runSafely (() => {
                var files = Directory.GetFiles (dir);
                var target = default (Type);
                if (files.Length < 1) return target;
                foreach (var file in files) {
                    target = getTypesImplFromDll<ILiveFetcher> (files.First ()).FirstOrDefault ();
                    if (target == null) continue;
                    return target;
                }
                return target;
            });
        }

        private IEnumerable<Type> getTypesImplFromDirectory<T> (string dir, string searchPattern) {
            if (!Directory.Exists (dir)) return Enumerable.Empty<Type> ();
            var result = new List<Type> ();
            foreach (var file in Directory.EnumerateFiles (dir, searchPattern)) {
                IEnumerable<Type> types = null;
                runSafely (() => {
                    types = getTypesImplFromDll<T> (file);
                });
                if (types == null) continue;
                result.AddRange (types);
            }
            return result;
        }

        private IEnumerable<Type> getTypesImplFromDll<T> (string path) {
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

        static T runSafely<T> (Func<T> doWhat) {
            try {
                return doWhat.Invoke ();
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine (e.ToString ());
            }
            return default (T);
        }

        static void runSafely (Action doWhat) {
            try {
                doWhat.Invoke ();
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine (e.ToString ());
            }
        }
    }
}