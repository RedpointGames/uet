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
    }
}
