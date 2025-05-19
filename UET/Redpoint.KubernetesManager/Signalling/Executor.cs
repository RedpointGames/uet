namespace Redpoint.KubernetesManager.Signalling
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Components;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal class Executor
    {
        private readonly ILogger<Executor> _logger;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly RoleType _roleType;
        private readonly CancellationToken _stoppingToken;
        private readonly Dictionary<string, List<(Type, SignalDelegate)>> _signalDispatch;
        private readonly ConcurrentDictionary<string, FlagSlim> _flagDispatch;

        public Executor(
            ILogger<Executor> logger,
            IHostApplicationLifetime hostApplicationLifetime,
            RoleType roleType,
            CancellationToken stoppingToken)
        {
            _logger = logger;
            _hostApplicationLifetime = hostApplicationLifetime;
            _roleType = roleType;
            _stoppingToken = stoppingToken;
            _signalDispatch = new Dictionary<string, List<(Type, SignalDelegate)>>();
            _flagDispatch = new ConcurrentDictionary<string, FlagSlim>();
        }

        public RoleType Role => _roleType;

        private class WrappingRegistrationContext : IRegistrationContext
        {
            private readonly Executor _executor;
            private readonly Type _componentType;

            public WrappingRegistrationContext(Executor executor, Type type)
            {
                _executor = executor;
                _componentType = type;
            }

            public RoleType Role => _executor.Role;

            public void OnSignal(string signalType, SignalDelegate callback)
            {
                if (!_executor._signalDispatch.TryGetValue(signalType, out var value))
                {
                    value = new List<(Type, SignalDelegate)>();
                    _executor._signalDispatch.Add(signalType, value);
                }

                value.Add((_componentType, callback));
            }
        }

        private class WrappingContext : IContext
        {
            private readonly ILogger<Executor> _logger;
            private readonly Executor _executor;
            private readonly Type _componentType;

            public WrappingContext(
                ILogger<Executor> logger,
                Executor executor,
                Type componentType)
            {
                _logger = logger;
                _executor = executor;
                _componentType = componentType;
            }

            public RoleType Role => _executor.Role;

            public Task RaiseSignalAsync(string name, IAssociatedData? data, CancellationToken cancellationToken)
            {
                return _executor.RaiseSignalAsync(name, data, cancellationToken);
            }

            public void SetFlag(string name, IAssociatedData? data = null)
            {
                _executor.SetFlag(name, data);
            }

            public void StopOnCriticalError()
            {
                _executor.StopOnCriticalError();
            }

            public async Task<T> WaitForFlagAsync<T>(string name) where T : IAssociatedData
            {
                _logger.LogInformation($"{_componentType.Name} is now waiting for flag: {name}");
                var result = await _executor.WaitForFlagAsync(name);
                _logger.LogInformation($"{_componentType.Name} is now proceeding because flag has been set: {name}");
                return ((T)result!);
            }

            public async Task WaitForFlagAsync(string name)
            {
                _logger.LogInformation($"{_componentType.Name} is now waiting for flag: {name}");
                await _executor.WaitForFlagAsync(name);
                _logger.LogInformation($"{_componentType.Name} is now proceeding because flag has been set: {name}");
            }

            public async Task WaitForUninterruptableFlagAsync(string name)
            {
                _logger.LogInformation($"{_componentType.Name} is now waiting for uninterruptable flag: {name}");
                await _executor.WaitForUninterruptableFlagAsync(name);
                _logger.LogInformation($"{_componentType.Name} is now proceeding because uninterruptable flag has been set: {name}");
            }
        }

        public void RegisterComponents(IComponent[] components)
        {
            foreach (var component in components)
            {
                component.RegisterSignals(new WrappingRegistrationContext(this, component.GetType()));
            }
        }

        internal async Task RaiseSignalAsync(string name, IAssociatedData? data, CancellationToken cancellationToken)
        {
            if (_signalDispatch.TryGetValue(name, out var signals))
            {
                _logger.LogInformation($"Signal has been raised: {name}");
                await Task.WhenAll(signals.Select(x => Task.Run(async () =>
                {
                    try
                    {
                        await x.Item2(new WrappingContext(_logger, this, x.Item1), data, cancellationToken);
                        _logger.LogInformation($"Component {x.Item1.Name} has finished responding to signal: {name}");
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected.
                    }
                    catch (Exception ex)
                    {
                        _logger.LogCritical(ex, $"Component {x.Item1.Name} critically failed with an exception.");
                        Environment.ExitCode = 1;
                        _hostApplicationLifetime.StopApplication();
                    }
                }, cancellationToken)));
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        internal void SetFlag(string name, IAssociatedData? data = null)
        {
            try
            {
                _logger.LogInformation($"Flag has been set: {name}");
                _flagDispatch.GetOrAdd(name, new FlagSlim()).Set(data);
            }
            catch (FlagAlreadySetException)
            {
                _logger.LogCritical($"Attempted to set flag '{name}' more than once!");
                StopOnCriticalError();
            }
        }

        private async Task<IAssociatedData?> WaitForFlagAsync(string name)
        {
            return await _flagDispatch.GetOrAdd(name, new FlagSlim()).WaitAsync(_stoppingToken);
        }

        private async Task<IAssociatedData?> WaitForUninterruptableFlagAsync(string name)
        {
            return await _flagDispatch.GetOrAdd(name, new FlagSlim()).WaitAsync(CancellationToken.None);
        }

        private void StopOnCriticalError()
        {
            _logger.LogInformation("Encountered a critical error, requesting that RKM exit...");
            _hostApplicationLifetime.StopApplication();
        }
    }
}
