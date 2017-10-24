namespace LiveRoku.Base.Plugin {
    public class PluginDescriptor : IPluginDescriptor {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
    }
}