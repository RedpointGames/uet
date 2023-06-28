namespace Redpoint.Unreal.Serialization
{
    public record class TopLevelAssetPath : ISerializable<TopLevelAssetPath>
    {
        public Store<Name> PackageName;
        public Store<Name> AssetName;

        public TopLevelAssetPath()
        {
            PackageName = new Store<Name>(Name.Empty);
            AssetName = new Store<Name>(Name.Empty);
        }

        public TopLevelAssetPath(Name packageName, Name assetName)
        {
            PackageName = new Store<Name>(packageName);
            AssetName = new Store<Name>(assetName);
        }

        public static async Task Serialize(Archive ar, Store<TopLevelAssetPath> value)
        {
            await ar.Serialize(value.V.PackageName);
            await ar.Serialize(value.V.AssetName);
        }

        public override string ToString()
        {
            return $"[{PackageName.V}.{AssetName.V}]";
        }

        public bool Is(string packageName, string assetName)
        {
            return (PackageName.V.StringName.V == packageName ||
                packageName == string.Empty ||
                PackageName.V.StringName.V == string.Empty) &&
                AssetName.V.StringName.V == assetName;
        }
    }
}
