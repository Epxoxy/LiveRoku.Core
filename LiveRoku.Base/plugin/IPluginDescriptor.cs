namespace LiveRoku.Base.Plugin {
    public interface IPluginDescriptor {
        string Name { get; }
        string Version { get; }
        string Author { get; }
        string Description { get; }
    }
}