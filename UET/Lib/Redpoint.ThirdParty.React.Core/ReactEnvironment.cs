/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using JavaScriptEngineSwitcher.Core;
using JSPool;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace React
{
    /// <summary>
    /// Request-specific ReactJS.NET environment. This is unique to the individual request and is
    /// not shared.
    /// </summary>
    public class ReactEnvironment : IReactEnvironment, IDisposable
    {
        /// <summary>
        /// JavaScript variable set when user-provided scripts have been loaded
        /// </summary>
        protected const string USER_SCRIPTS_LOADED_KEY = "_ReactNET_UserScripts_Loaded";
        /// <summary>
        /// Stack size to use for JSXTransformer if the default stack is insufficient
        /// </summary>
        protected const int LARGE_STACK_SIZE = 2 * 1024 * 1024;

        /// <summary>
        /// Factory to create JavaScript engines
        /// </summary>
        protected readonly IJavaScriptEngineFactory _engineFactory;
        /// <summary>
        /// Site-wide configuration
        /// </summary>
        protected readonly IReactSiteConfiguration _config;
        /// <summary>
        /// Cache used for storing compiled JSX
        /// </summary>
        protected readonly ICache _cache;
        /// <summary>
        /// File system wrapper
        /// </summary>
        protected readonly IFileSystem _fileSystem;
        /// <summary>
        /// Hash algorithm for file-based cache
        /// </summary>
        protected readonly IFileCacheHash _fileCacheHash;
        /// <summary>
        /// React Id generator
        /// </summary>
        private readonly IReactIdGenerator _reactIdGenerator;

        /// <summary>
        /// Version number of ReactJS.NET
        /// </summary>
        protected readonly Lazy<string> _version = new Lazy<string>(GetVersion);

        /// <summary>
        /// Contains an engine acquired from a pool of engines. Only used if
        /// <see cref="IReactSiteConfiguration.ReuseJavaScriptEngines"/> is enabled.
        /// </summary>
        protected Lazy<PooledJsEngine> _engineFromPool;

        /// <summary>
        /// List of all components instantiated in this environment
        /// </summary>
        protected readonly IList<IReactComponent> _components = new List<IReactComponent>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ReactEnvironment"/> class.
        /// </summary>
        /// <param name="engineFactory">The JavaScript engine factory</param>
        /// <param name="config">The site-wide configuration</param>
        /// <param name="cache">The cache to use for JSX compilation</param>
        /// <param name="fileSystem">File system wrapper</param>
        /// <param name="fileCacheHash">Hash algorithm for file-based cache</param>
        /// <param name="reactIdGenerator">React ID generator</param>
        public ReactEnvironment(
            IJavaScriptEngineFactory engineFactory,
            IReactSiteConfiguration config,
            ICache cache,
            IFileSystem fileSystem,
            IFileCacheHash fileCacheHash,
            IReactIdGenerator reactIdGenerator)
        {
            _engineFactory = engineFactory;
            _config = config;
            _cache = cache;
            _fileSystem = fileSystem;
            _fileCacheHash = fileCacheHash;
            _reactIdGenerator = reactIdGenerator;
            _engineFromPool = new Lazy<PooledJsEngine>(() => _engineFactory.GetEngine());
        }

        /// <summary>
        /// Gets the JavaScript engine to use for this environment.
        /// </summary>
        protected virtual IJsEngine Engine
        {
            get
            {
                return _config.ReuseJavaScriptEngines
                    ? _engineFromPool.Value
                    : _engineFactory.GetEngineForCurrentThread();
            }
        }

        /// <summary>
        /// Gets the version of the JavaScript engine in use by ReactJS.NET
        /// </summary>
        public virtual string EngineVersion
        {
            get { return Engine.Name + " " + Engine.Version; }
        }

        /// <summary>
        /// Gets the version number of ReactJS.NET
        /// </summary>
        public virtual string Version
        {
            get { return _version.Value; }
        }

        /// <summary>
        /// Ensures any user-provided scripts have been loaded. This only loads JSX files; files
        /// that need no transformation are loaded in JavaScriptEngineFactory.
        /// </summary>
        protected virtual void EnsureUserScriptsLoaded()
        {
            // We no longer do Babel transpilation.
            Engine.SetVariableValue(USER_SCRIPTS_LOADED_KEY, true);
            return;
        }

        /// <summary>
        /// Executes the provided JavaScript code.
        /// </summary>
        /// <param name="code">JavaScript to execute</param>
        public virtual void Execute(string code)
        {
            Engine.Execute(code);
        }

        /// <summary>
        /// Executes the provided JavaScript code, returning a result of the specified type.
        /// </summary>
        /// <typeparam name="T">Type to return</typeparam>
        /// <param name="code">Code to execute</param>
        /// <returns>Result of the JavaScript code</returns>
        public virtual T Execute<T>(string code)
        {
            return Engine.Evaluate<T>(code);
        }

        /// <summary>
        /// Executes the provided JavaScript function, returning a result of the specified type.
        /// </summary>
        /// <typeparam name="T">Type to return</typeparam>
        /// <param name="function">JavaScript function to execute</param>
        /// <param name="args">Arguments to pass to function</param>
        /// <returns>Result of the JavaScript code</returns>
        [RequiresDynamicCode("Uses JsonSerializer.Deserialize<T> without type info.")]
        [RequiresUnreferencedCode("Uses JsonSerializer.Deserialize<T> without type info.")]
        public virtual T Execute<T>(string function, params object[] args)
        {
            return Engine.CallFunctionReturningJson<T>(function, args);
        }

        /// <summary>
        /// Determines if the specified variable exists in the JavaScript engine
        /// </summary>
        /// <param name="name">Name of the variable</param>
        /// <returns><c>true</c> if the variable exists; <c>false</c> otherwise</returns>
        public virtual bool HasVariable(string name)
        {
            return Engine.HasVariable(name);
        }

        /// <summary>
        /// Creates an instance of the specified React JavaScript component.
        /// </summary>
        /// <typeparam name="T">Type of the props</typeparam>
        /// <param name="componentName">Name of the component</param>
        /// <param name="props">Props to use</param>
        /// <param name="containerId">ID to use for the container HTML tag. Defaults to an auto-generated ID</param>
        /// <param name="clientOnly">True if server-side rendering will be bypassed. Defaults to false.</param>
        /// <param name="serverOnly">True if this component only should be rendered server-side. Defaults to false.</param>
        /// <param name="skipLazyInit">Skip adding to components list, which is used during GetInitJavascript</param>
        /// <returns>The component</returns>
        [RequiresDynamicCode("Props serialization uses JsonSerializer without type info.")]
        [RequiresUnreferencedCode("Props serialization uses JsonSerializer without type info.")]
        public virtual IReactComponent CreateComponent<T>(string componentName, T props, string containerId = null, bool clientOnly = false, bool serverOnly = false, bool skipLazyInit = false)
        {
            if (!clientOnly)
            {
                EnsureUserScriptsLoaded();
            }

            var component = new ReactComponent(this, _config, _reactIdGenerator, componentName, containerId)
            {
                ClientOnly = clientOnly,
                Props = props,
                ServerOnly = serverOnly
            };

            if (!skipLazyInit)
            {
                _components.Add(component);
            }
            return component;
        }

        /// <summary>
        /// Adds the provided <see cref="IReactComponent"/> to the list of components to render client side.
        /// </summary>
        /// <param name="component">Component to add to client side render list</param>
        /// <param name="clientOnly">True if server-side rendering will be bypassed. Defaults to false.</param>
        /// <returns>The component</returns>
        public virtual IReactComponent CreateComponent(IReactComponent component, bool clientOnly = false)
        {
            if (!clientOnly)
            {
                EnsureUserScriptsLoaded();
            }

            _components.Add(component);
            return component;
        }

        /// <summary>
        /// Renders the JavaScript required to initialise all components client-side. This will
        /// attach event handlers to the server-rendered HTML.
        /// </summary>
        /// <param name="clientOnly">True if server-side rendering will be bypassed. Defaults to false.</param>
        /// <returns>JavaScript for all components</returns>
        public virtual string GetInitJavaScript(bool clientOnly = false)
        {
            using (var writer = new StringWriter())
            {
                GetInitJavaScript(writer, clientOnly);
                return writer.ToString();
            }
        }

        /// <summary>
        /// Renders the JavaScript required to initialise all components client-side. This will
        /// attach event handlers to the server-rendered HTML.
        /// </summary>
        /// <param name="writer">The <see cref="T:System.IO.TextWriter" /> to which the content is written</param>
        /// <param name="clientOnly">True if server-side rendering will be bypassed. Defaults to false.</param>
        /// <returns>JavaScript for all components</returns>
        public virtual void GetInitJavaScript(TextWriter writer, bool clientOnly = false)
        {
            // Propagate any server-side console.log calls to corresponding client-side calls.
            if (!clientOnly && _components.Count != 0)
            {
                var consoleCalls = Execute<string>("console.getCalls()");
                writer.Write(consoleCalls);
            }

            foreach (var component in _components)
            {
                if (!component.ServerOnly)
                {
                    component.RenderJavaScript(writer, waitForDOMContentLoad: false);
                    writer.WriteLine(';');
                }
            }
        }

        private ReactAppAssetManifest GetAppManifest() => ReactAppAssetManifest.LoadManifest(_config, _fileSystem, _cache, useCacheRead: true);

        /// <summary>
        /// Returns a list of paths to scripts generated by the React app
        /// </summary>
        public virtual IEnumerable<string> GetScriptPaths()
        {
            return GetAppManifest().Entrypoints
                .Where(path => path.EndsWith(".js"))
                .Where(path => _config.FilterResource == null || _config.FilterResource(path));
        }

        /// <summary>
        /// Returns a list of paths to stylesheets generated by the React app
        /// </summary>
        public virtual IEnumerable<string> GetStylePaths()
        {
            return GetAppManifest().Entrypoints
                .Where(path => path.EndsWith(".css"))
                .Where(path => _config.FilterResource == null || _config.FilterResource(path));
        }

        /// <summary>
        /// Gets the ReactJS.NET version number. Use <see cref="Version" /> instead.
        /// </summary>
        private static string GetVersion()
        {
            var assembly = typeof(ReactEnvironment).GetTypeInfo().Assembly;
            var rawVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
            var lastDot = rawVersion.LastIndexOf('.');
            var version = rawVersion.Substring(0, lastDot);
            var build = rawVersion.Substring(lastDot + 1);
            return string.Format("{0} (build {1})", version, build);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public virtual void Dispose()
        {
            _engineFactory.DisposeEngineForCurrentThread();
            ReturnEngineToPool();
        }

        /// <summary>
        /// Returns the currently held JS engine to the pool. (no-op if engine pooling is disabled)
        /// </summary>
        public void ReturnEngineToPool()
        {
            if (_engineFromPool.IsValueCreated)
            {
                _engineFromPool.Value.Dispose();
                _engineFromPool = new Lazy<PooledJsEngine>(() => _engineFactory.GetEngine());
            }
        }

        /// <summary>
        /// Gets the site-wide configuration.
        /// </summary>
        public virtual IReactSiteConfiguration Configuration
        {
            get { return _config; }
        }
    }
}
