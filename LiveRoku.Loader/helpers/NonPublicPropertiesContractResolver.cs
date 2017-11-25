namespace LiveRoku.Loader.Helpers {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public class NonPublicPropertiesContractResolver : DefaultContractResolver {

        protected override IList<JsonProperty> CreateProperties (Type type, MemberSerialization memberSerialization) {
            var props = type.GetProperties (BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Select (p => CreateProperty (p, memberSerialization))
                .Union (type.GetFields (BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Select (f => CreateProperty (f, memberSerialization)))
                .ToList ();
            return props;
        }

        protected override JsonProperty CreateProperty (MemberInfo member, MemberSerialization memberSerialization) {
            var prop = base.CreateProperty (member, memberSerialization);
            var pi = member as PropertyInfo;
            if (pi != null) {
                prop.Readable = (pi.GetMethod != null);
                prop.Writable = (pi.SetMethod != null);
            }
            return prop;
        }
    }
}