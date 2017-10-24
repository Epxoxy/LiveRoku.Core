namespace LiveRoku.Base{
    public interface ILowList<T> where T : class {
        void add(T value);
        void remove(T value);
    }
}
