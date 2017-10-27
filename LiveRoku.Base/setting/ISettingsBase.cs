using System.Collections.Generic;
namespace LiveRoku.Base{
    public interface ISettingsBase {
        IReadOnlyDictionary<string, object> Settings { get; }
        T get<T> (string key, T defaultValue = default (T));
        void put<T> (string key, T value);
        bool delete (string key);
    }
}
