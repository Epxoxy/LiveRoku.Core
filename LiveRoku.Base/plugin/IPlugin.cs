namespace LiveRoku.Base.Plugin {
    public interface IPlugin {
        string Token { get; }
        IPluginDescriptor Descriptor { get; }
        void onInitialize (ISettings settings);
        void onAttach (IPluginHost host);
        void onDetach (IPluginHost host);
    }
}