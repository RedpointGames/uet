namespace Redpoint.Uefs.Daemon.Service.Mounting
{
    using Redpoint.Uefs.Protocol;

    internal class DefaultWriteScratchPath : IWriteScratchPath
    {
        public string ComputeWriteScratchPath(MountRequest request, string targetPath)
        {
            if (request.WriteScratchPersistence == WriteScratchPersistence.Keep)
            {
                if (!string.IsNullOrWhiteSpace(request.WriteScratchPath))
                {
                    return request.WriteScratchPath;
                }
            }

            return PathUtils.GetTemporaryWriteLayerPath(targetPath);
        }
    }
}
