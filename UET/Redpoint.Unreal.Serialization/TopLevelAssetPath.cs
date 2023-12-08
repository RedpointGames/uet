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
            ArgumentNullException.ThrowIfNull(ar);
            ArgumentNullException.ThrowIfNull(value);

            await ar.Serialize(value.V.PackageName).ConfigureAwait(false);
            await ar.Serialize(value.V.AssetName).ConfigureAwait(false);
        }

        public override string ToString()
        {
            return $"[{PackageName.V}.{AssetName.V}]";
        }

        public bool Is(string packageName, string assetName)
        {
            return (PackageName.V.StringName.V == packageName ||
                string.IsNullOrEmpty(packageName) ||
                string.IsNullOrEmpty(PackageName.V.StringName.V)) &&
                AssetName.V.StringName.V == assetName;
        }
    }
}
