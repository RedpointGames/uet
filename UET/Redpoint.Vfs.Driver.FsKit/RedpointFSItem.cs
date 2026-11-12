#pragma warning disable APL0002

namespace Redpoint.Vfs.Driver.FsKit
{
    using FSKit;

    public sealed class RedpointFSItem : FSItem
    {
        public FSFileName Name { get; }

        public FSItemAttributes Attributes { get; }

        public OrderedDictionary<string, RedpointFSItem> Children { get; }

        public RedpointFSItem(
            FSFileName name,
            FSItemAttributes attributes)
        {
            Name = name;
            Attributes = attributes;
            Children = new OrderedDictionary<string, RedpointFSItem>();
        }
    }
}
