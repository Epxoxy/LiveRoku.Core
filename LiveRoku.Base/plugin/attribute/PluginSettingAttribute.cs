namespace LiveRoku.Base.Plugin {
    using System.Collections.Generic;
    using System.Linq;
    using System;

    [AttributeUsage (AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class PluginSettingAttribute : Attribute {
        public bool Required { get; set; } = false;
        public string Key { get; set; }
        
    }

}