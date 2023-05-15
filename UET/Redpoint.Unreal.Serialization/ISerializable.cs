namespace Redpoint.Unreal.Serialization
{
    public interface ISerializable<T> where T : new()
    {
        static abstract void Serialize(Archive ar, ref T value);
    }
}