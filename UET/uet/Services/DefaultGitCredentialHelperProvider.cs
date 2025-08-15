namespace UET.Services
{
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.Workspace.PhysicalGit;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class DefaultGitCredentialHelperProvider : IGitCredentialHelperProvider
    {
        private readonly ISelfLocation _selfLocation;

        public DefaultGitCredentialHelperProvider(
            ISelfLocation selfLocation)
        {
            _selfLocation = selfLocation;
        }

        public string FilePath => _selfLocation.GetUetLocalLocation(false);

        public IEnumerable<LogicalProcessArgument> Arguments => ["internal", "git-credential-helper"];
    }
}
