namespace LiveRoku.Loader {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using LiveRoku.Base;
    using LiveRoku.Base.Plugin;
    using LiveRoku.Loader.Helper;
    using LiveRoku.Loader.Base;

    public class LoadManager : IDisposable {
        public string BaseDirectory => baseDir;
        private readonly string baseDir;
        private readonly string dataDir;
        private readonly string pluginDir;
        private readonly string coreDir;
        private const string appDataFileName = "app.data";
        private IDictionary<string, Assembly> assemblies;
        private ModuleContextBase globalBaseCtx;

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
        
        public ModuleContextLoader generateLoader(bool reload = false) {
            if (globalBaseCtx == null || reload) {
                globalBaseCtx = reloadCtxBase();
            }
            if (globalBaseCtx != null) {
                return new ModuleContextLoader(globalBaseCtx, dataDir, appDataFileName);
            }
            return null;
        }

        private ModuleContextBase reloadCtxBase () {
            //Load core part
            Type coreType = null;
            IEnumerable<Type> pluginTypes = null;
            AppLocalData appLocalData = null;
            var extraSettings = new Dictionary<string, SettingSection> ();
            //Load core.dll
            if ((coreType = findCoreImpl (coreDir)) == null)
                throw new Exception ("Core.dll cannot be load.");
            //Get plugin types
            Utils.runSafely (() => {
                pluginTypes = getTypesImplFromDirectory<IPlugin> (pluginDir, "*.dll");
            });
            //Get app local data
            Utils.runSafely (() => {
                appLocalData = FileHelper.deserializeFromPath<AppLocalData> (Path.Combine (dataDir, appDataFileName));
            });
            //Get extra settings
            foreach (var file in FileHelper.safelyGetFiles (dataDir, "*.txt")) {
                Utils.runSafely (() => {
                    var collection = FileHelper.deserializeFromPath<SettingSection> (file);
                    if (collection != null)
                        extraSettings.Add (collection.AccessKey, collection);
                });
            }
            //set context config
            appLocalData = appLocalData ?? new AppLocalData ();
            appLocalData.ExtraSettings = extraSettings;
            //set context
            var ctx = new ModuleContextBase (dataDir, appDataFileName) {
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
            return ctx;
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
            var files = Directory.GetFiles(dir);
            var target = default(Type);
            if (files.Length < 1)
                return target;
            foreach (var file in files) {
                target = getTypesImplFromDll<ILiveFetcher>(files.First()).FirstOrDefault();
                if (target == null)
                    continue;
                return target;
            }
            return target;
        }

        private IEnumerable<Type> getTypesImplFromDirectory<T> (string dir, string searchPattern) {
            if (!Directory.Exists (dir)) return Enumerable.Empty<Type> ();
            var result = new List<Type> ();
            foreach (var file in Directory.EnumerateFiles (dir, searchPattern)) {
                IEnumerable<Type> types = null;
                Utils.runSafely (() => {
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

    }

    public class Utils {
        public static T runSafely<T>(Func<T> doWhat) {
            try {
                return doWhat.Invoke();
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine(e.ToString());
            }
            return default(T);
        }

        public static void runSafely(Action doWhat) {
            try {
                doWhat.Invoke();
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine(e.ToString());
            }
        }
    }

    public class ModuleContextLoader {
        public ModuleContextBase BaseContext => baseCtx;
        private readonly ModuleContextBase baseCtx;
        private readonly string dataDir;
        private readonly string appDataFileName;

        public ModuleContextLoader(ModuleContextBase baseCtx, string dataDir, string appDataFileName) {
            this.baseCtx = baseCtx;
            this.dataDir = dataDir;
            this.appDataFileName = appDataFileName;
        }

        public ModuleContext create(IPreferences pref) {
            if (pref == null) {
                throw new ArgumentNullException(nameof(pref));
            }
            if (baseCtx == null) {
                throw new InvalidOperationException("Cannot load without valid " + nameof(baseCtx));
            }
            //setupCtxBase(reload);
            var instance = Activator.CreateInstance(baseCtx.CoreType, pref);
            if (instance == null) {
                throw new Exception("Core implement cannot be create.");
            }
            //make plugins
            var plugins = new List<IPlugin>();
            if (baseCtx.PluginTypes.Count > 0) {
                var orderedList = baseCtx.AppLocalData.AppConfigs.Values.ToList().OrderBy(config => config.Priority);
                var invalidFileNameChars = new string(Path.GetInvalidFileNameChars());
                var regFileName = new Regex(string.Format("[{0}]", Regex.Escape(invalidFileNameChars)));
                foreach (var config in orderedList) {
                    //make instance
                    if (!config.IsEnable)
                        continue;
                    Utils.runSafely(() => {
                        var plugin = (IPlugin)Activator.CreateInstance(config.HostType);
                        plugins.Add(plugin);
                        var key = plugin.Token ?? plugin.GetType().FullName;
                        //set configuration
                        config.AccessToken = key;
                        if (string.IsNullOrEmpty(config.ConfigName))
                            config.ConfigName = regFileName.Replace(key, "").ToLower() + ".txt";
                        //restore setting
                        if (baseCtx.AppLocalData.ExtraSettings.TryGetValue(key, out SettingSection settings)) {
                            PluginHelper.applySettings(plugin, settings.Items);
                        }
                    });
                }
            }
            return new ModuleContext(dataDir, appDataFileName) {
                Fetcher = instance as ILiveFetcher,
                Plugins = plugins,
                AppLocalData = baseCtx.AppLocalData,
                CoreType = baseCtx.CoreType,
                PluginTypes = baseCtx.PluginTypes
            };
        }

    }

}