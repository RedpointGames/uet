namespace Redpoint.Vfs.Layer.Folder
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Vfs.Abstractions;
    using Redpoint.Vfs.LocalIo;

    internal class FolderVfsLayerFactory : IFolderVfsLayerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public FolderVfsLayerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IVfsLayer CreateLayer(string path, IVfsLayer? nextLayer)
        {
            return new FolderVfsLayer(
                _serviceProvider.GetRequiredService<ILogger<FolderVfsLayer>>(),
                _serviceProvider.GetRequiredService<ILocalIoVfsFileFactory>(),
                path,
                nextLayer);
        }
    }
}