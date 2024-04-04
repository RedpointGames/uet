namespace Redpoint.Uet.Workspace
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal class DefaultWorkspaceReservationParameterGenerator : IWorkspaceReservationParameterGenerator
    {
        public DefaultWorkspaceReservationParameterGenerator(ILogger<DefaultWorkspaceReservationParameterGenerator> logger)
        {
            _logger = logger;
        }

        private static readonly Lazy<string> _machineNameDisambiguator = new Lazy<string>(() => Environment.MachineName);
        private readonly ILogger<DefaultWorkspaceReservationParameterGenerator> _logger;

        public string[] ConstructReservationParameters(params string[] parameters)
        {
            return ConstructReservationParameters((IEnumerable<string>)parameters);
        }

        public string[] ConstructReservationParameters(IEnumerable<string> parameters)
        {
            var machineName = _machineNameDisambiguator.Value;
            if (string.IsNullOrWhiteSpace(machineName))
            {
                _logger.LogWarning($"Environment.MachineName evaluted to '{machineName}'. Reservation folder names will not be unique to this machine.");
                return parameters.ToArray();
            }
            return new[] { _machineNameDisambiguator.Value }.Concat(parameters).ToArray();
        }
    }
}
