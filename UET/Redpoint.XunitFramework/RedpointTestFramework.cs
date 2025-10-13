namespace Redpoint.XunitFramework
{
    using System.Reflection;
    using Xunit.Internal;
    using Xunit.Sdk;
    using Xunit.v3;

    /// <inheritdoc/>
    public class RedpointTestFramework : TestFramework
    {
        readonly string? _configFileName;

        /// <summary>
        /// Initializes a new instance of the <see cref="XunitTestFramework"/> class.
        /// </summary>
        public RedpointTestFramework()
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="XunitTestFramework"/> class.
        /// </summary>
        /// <param name="configFileName">The optional test configuration file.</param>
        public RedpointTestFramework(string? configFileName) =>
            this._configFileName = configFileName;

        /// <inheritdoc/>
        public override string TestFrameworkDisplayName => "xUnit.net v3 with Redpoint";

        /// <inheritdoc/>
        protected override ITestFrameworkDiscoverer CreateDiscoverer(Assembly assembly) =>
            new XunitTestFrameworkDiscoverer(new XunitTestAssembly(Guard.ArgumentNotNull(assembly), _configFileName, assembly.GetName().Version));

        /// <inheritdoc/>
        protected override ITestFrameworkExecutor CreateExecutor(Assembly assembly) =>
            new RedpointTestFrameworkExecutor(new XunitTestAssembly(Guard.ArgumentNotNull(assembly), _configFileName, assembly.GetName().Version));
    }
}