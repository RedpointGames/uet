namespace Redpoint.Unreal.Serialization
{
    using System;
    using System.Text.Json;

    public interface ISerializerRegistry
    {
        bool CanHandleStoreType(Type type);

        Task SerializeStoreType<T>(Archive ar, Store<T> value);

        bool CanHandleTopLevelAssetPath(TopLevelAssetPath topLevelAssetPath);

        object? DeserializeTopLevelAssetPath(TopLevelAssetPath topLevelAssetPath, string json, JsonSerializerOptions jsonOptions);

        string SerializeTopLevelAssetPath(TopLevelAssetPath topLevelAssetPath, object? value, JsonSerializerOptions jsonOptions);
    }
}
