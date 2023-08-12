namespace Redpoint.OpenGE.Component.Worker.DriveMapping
{
    internal class UnixDirectoryDriveMapping : IDirectoryDriveMapping
    {
        public string ShortenPath(string rootPath)
        {
            return rootPath;
        }
    }
}
