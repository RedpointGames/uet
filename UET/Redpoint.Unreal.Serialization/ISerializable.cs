namespace Redpoint.Unreal.Serialization
{
    public interface ISerializable<T> where T : new()
    {
        static abstract Task Serialize(Archive ar, Store<T> value);
    }
}