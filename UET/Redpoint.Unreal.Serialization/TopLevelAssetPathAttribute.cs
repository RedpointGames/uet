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
            PackageName = new Name(new Store<string>(packageName));
            AssetName = new Name(new Store<string>(assetName));
        }
    }
}
