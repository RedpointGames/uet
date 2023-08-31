namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories
{
    using Redpoint.OpenGE.Protocol;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Redpoint.OpenGE.Component.Dispatcher.Graph;

    internal abstract class RemoteTaskDescriptorFactory : ITaskDescriptorFactory
    {
        protected static readonly IReadOnlySet<string> _knownMachineSpecificEnvironmentVariables = new HashSet<string>
        {
            "ALLUSERSPROFILE",
            "APPDATA",
            "CommonProgramFiles",
            "CommonProgramFiles(x86)",
            "CommonProgramW6432",
            "COMPUTERNAME",
            "ComSpec",
            "DriverData",
            "HOMEDRIVE",
            "HOMEPATH",
            "LOCALAPPDATA",
            "LOGONSERVER",
            "NUMBER_OF_PROCESSORS",
            "OneDrive",
            "OS",
            "Path",                                 // @note: Should this really be excluded?
            "PATHEXT",
            "POWERSHELL_DISTRIBUTION_CHANNEL",
            "PROCESSOR_ARCHITECTURE",
            "PROCESSOR_IDENTIFIER",
            "PROCESSOR_LEVEL",
            "PROCESSOR_REVISION",
            "ProgramData",
            "ProgramFiles",
            "ProgramFiles(x86)",
            "ProgramW6432",
            "PROMPT",
            "PSModulePath",                         // @note: Should this really be excluded?
            "PUBLIC",
            "SESSIONNAME",
            "SystemDrive",                          // @note: Should this really be excluded?
            "SystemRoot",                           // @note: Should this really be excluded?
            "TEMP",
            "TMP",
            "USERDOMAIN",
            "USERDOMAIN_ROAMINGPROFILE",
            "USERNAME",
            "USERPROFILE",
            "windir",
            "WSLENV",
            "WT_PROFILE_ID",
            "WT_SESSION",
        };

        public abstract string PreparationOperationDescription { get; }

        public abstract string PreparationOperationCompletedDescription { get; }

        public abstract int ScoreTaskSpec(GraphTaskSpec spec);

        public abstract ValueTask<TaskDescriptor> CreateDescriptorForTaskSpecAsync(
            GraphTaskSpec spec,
            bool guaranteedToExecuteLocally,
            CancellationToken cancellationToken);
    }
}
