namespace Redpoint.Vfs.Layer.Scratch
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Vfs.Abstractions;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Vfs.LocalIo;

    internal class ScratchVfsLayerFactory : IScratchVfsLayerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public ScratchVfsLayerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IScratchVfsLayer CreateLayer(string path, IVfsLayer? nextLayer, bool enableCorrectnessChecks = false)
        {
            return new ScratchVfsLayer(
                _serviceProvider.GetRequiredService<ILogger<ScratchVfsLayer>>(),
                _serviceProvider.GetRequiredService<ILogger<FilesystemScratchIndex>>(),
                _serviceProvider.GetRequiredService<ILocalIoVfsFileFactory>(),
                path,
                nextLayer,
                enableCorrectnessChecks);
        }
    }
}
