namespace Redpoint.Uba
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.Uba.Native;
    using System;

    /// <remarks>
    /// This class can't import library functions directly, since it might be constructed by dependency injection prior to user code getting a chance to call <see cref="UbaNative.Init(string)"/>.
    /// </remarks>
    internal class DefaultUbaServerFactory : IUbaServerFactory
    {
        private readonly ILogger<DefaultUbaServer> _defaultUbaServerLogger;
        private readonly ILogger<UbaLoggerForwarder> _ubaLoggerForwarderLogger;
        private readonly IProcessArgumentParser _processArgumentParser;
        private readonly IProcessExecutor _localProcessExecutor;

        public DefaultUbaServerFactory(
            ILogger<DefaultUbaServer> defaultUbaServerLogger,
            ILogger<UbaLoggerForwarder> ubaLoggerForwarderLogger,
            IProcessArgumentParser processArgumentParser,
            IProcessExecutor localProcessExecutor)
        {
            _defaultUbaServerLogger = defaultUbaServerLogger;
            _ubaLoggerForwarderLogger = ubaLoggerForwarderLogger;
            _processArgumentParser = processArgumentParser;
            _localProcessExecutor = localProcessExecutor;
        }

        public IUbaServer CreateServer(
            string rootStorageDirectoryPath,
            string ubaTraceFilePath,
            int maxWorkers,
            int sendSize,
            int receiveTimeoutSeconds,
            bool useQuic)
        {
            if (!Path.IsPathFullyQualified(rootStorageDirectoryPath))
            {
                throw new ArgumentException("The storage path (where UBA will store/cache data) must be fully qualified.", nameof(rootStorageDirectoryPath));
            }
            if (!Path.IsPathFullyQualified(ubaTraceFilePath))
            {
                throw new ArgumentException("The UBA trace file path must be fully qualified.", nameof(ubaTraceFilePath));
            }
            Directory.CreateDirectory(rootStorageDirectoryPath);
            Directory.CreateDirectory(Path.GetDirectoryName(ubaTraceFilePath)!);

            // @note: UbaLoggerForwarder must be static and hold a singleton log. Otherwise we can run into a crash if
            // DefaultUbaServer is finalized after UbaLoggerForwarder.
            var ubaLogger = UbaLoggerForwarder.GetUbaLogger(_ubaLoggerForwarderLogger);
            var server = UbaServerDelayedImports.CreateServer(
                ubaLogger,
                maxWorkers,
                sendSize,
                receiveTimeoutSeconds,
                useQuic);
            return new DefaultUbaServer(
                _defaultUbaServerLogger,
                _processArgumentParser,
                _localProcessExecutor,
                ubaLogger,
                server,
                rootStorageDirectoryPath,
                ubaTraceFilePath);
        }
    }
}
