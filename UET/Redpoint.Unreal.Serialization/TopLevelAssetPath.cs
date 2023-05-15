namespace Redpoint.Unreal.Serialization
{
    public record class TopLevelAssetPath : ISerializable<TopLevelAssetPath>
    {
        public Name PackageName;
        public Name AssetName;

        public TopLevelAssetPath()
        {
            PackageName = Name.Empty;
            AssetName = Name.Empty;
        }

        public TopLevelAssetPath(Name packageName, Name assetName)
        {
            PackageName = packageName;
            AssetName = assetName;
        }

        public static void Serialize(Archive ar, ref TopLevelAssetPath value)
        {
            ar.Serialize(ref value.PackageName);
            ar.Serialize(ref value.AssetName);
        }

        public override string ToString()
        {
            return $"[{PackageName}.{AssetName}]";
        }
    }
}
