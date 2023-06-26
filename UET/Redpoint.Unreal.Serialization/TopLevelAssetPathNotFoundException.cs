namespace Redpoint.Unreal.Serialization
{
    public class TopLevelAssetPathNotFoundException : Exception
    {
        public TopLevelAssetPathNotFoundException(TopLevelAssetPath assetPath) : base($"No class mapping for [TopLevelAssetPath] PackageName={assetPath.PackageName} AssetName={assetPath.AssetName}")
        {
            AssetPath = assetPath;
        }

        public TopLevelAssetPathNotFoundException(string message) : base(message)
        {
            AssetPath = new TopLevelAssetPath();
        }

        public TopLevelAssetPath AssetPath { get; }
    }
}