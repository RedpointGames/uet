namespace DiscUtils
{
    internal static class DynamicTypes
    {
        public static DynamicTypeRecord[] GetDynamicTypes()
        {
            return new[]
            {
                new DynamicTypeRecord(typeof(Raw.DiskFactory)),
                new DynamicTypeRecord(typeof(Vhd.DiskFactory)),
                new DynamicTypeRecord(typeof(Ntfs.FileSystemFactory)),
                new DynamicTypeRecord(typeof(ApplePartitionMap.PartitionMapFactory)),
                new DynamicTypeRecord(typeof(Partitions.DefaultPartitionTableFactory)),
                new DynamicTypeRecord(typeof(FileTransport)),
                new DynamicTypeRecord(typeof(LogicalDiskManager.DynamicDiskManagerFactory)),
            };
        }
    }
}
