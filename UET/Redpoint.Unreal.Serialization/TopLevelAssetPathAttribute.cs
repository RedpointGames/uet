namespace Redpoint.Unreal.Serialization
{
    using System;

    [AttributeUsage(AttributeTargets.Class)]
    public class TopLevelAssetPathAttribute : Attribute
    {
        public Name PackageName { get; }
        public Name AssetName { get; }

        public TopLevelAssetPathAttribute(string packageName, string assetName)
        {
            PackageName = packageName;
            AssetName = assetName;
        }
    }
}
