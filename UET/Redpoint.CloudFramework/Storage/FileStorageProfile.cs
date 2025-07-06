namespace Redpoint.CloudFramework.Storage
{
    public class FileStorageProfile
    {
        public FileStorageProfile(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public static FileStorageProfile Default { get; } = new FileStorageProfile("Default");
    }
}
