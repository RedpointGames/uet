namespace Redpoint.OpenGE.Component.PreprocessorCache.Filesystem
{
    internal interface IFilesystemExistenceProvider
    {
        ValueTask InitAsync()
        {
            return ValueTask.CompletedTask;
        }

        bool FileExists(string path, long buildStartTicks);
    }
}
