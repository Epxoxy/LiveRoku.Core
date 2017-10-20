namespace LiveRoku.Base {
    public interface IStorage {
        bool tryGet (string name, out object obj);
        bool tryGet<T> (string name, out T obj);
        bool add (string name, object value);
        bool save ();
    }
}