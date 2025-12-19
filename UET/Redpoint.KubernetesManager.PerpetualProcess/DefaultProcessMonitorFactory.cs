namespace Redpoint.KubernetesManager.PerpetualProcess
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Abstractions;

    internal class DefaultProcessMonitorFactory : IProcessMonitorFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IPathProvider? _pathProvider;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IWslDistro? _wslDistro;

        public DefaultProcessMonitorFactory(
            ILoggerFactory loggerFactory,
            IHostApplicationLifetime hostApplicationLifetime,
            IWslDistro? wslDistro = null,
            IPathProvider? pathProvider = null)
        {
            _loggerFactory = loggerFactory;
            _pathProvider = pathProvider;
            _hostApplicationLifetime = hostApplicationLifetime;
            _wslDistro = wslDistro;
        }

        public IProcessMonitor CreatePerpetualProcess(string filename, string[] arguments, Dictionary<string, string>? environment, Func<CancellationToken, Task>? beforeStart = null, Func<CancellationToken, Task>? afterStart = null)
        {
            return CreatePerpetualProcess(new PerpetualProcessSpecification(
                filename: filename,
                arguments: arguments,
                environment: environment,
                beforeStart: beforeStart,
                afterStart: afterStart));
        }

        public IProcessMonitor CreatePerpetualProcess(PerpetualProcessSpecification processSpecification)
        {
            return new SingleProcessMonitor(
                _loggerFactory.CreateLogger(Path.GetFileName(processSpecification.Filename)),
                _pathProvider,
                _hostApplicationLifetime,
                _wslDistro,
                processSpecification,
                perpetual: true);
        }

        public IProcessMonitor CreateTerminatingProcess(string filename, string[] arguments, Dictionary<string, string>? environment = null, bool silent = false)
        {
            return CreatePerpetualProcess(new PerpetualProcessSpecification(
                filename: filename,
                arguments: arguments,
                environment: environment,
                silent: silent));
        }

        public IProcessMonitor CreateTerminatingProcess(PerpetualProcessSpecification processSpecification)
        {
            return new SingleProcessMonitor(
                _loggerFactory.CreateLogger(Path.GetFileName(processSpecification.Filename)),
                _pathProvider,
                _hostApplicationLifetime,
                _wslDistro,
                processSpecification,
                perpetual: false);
        }
    }
}
