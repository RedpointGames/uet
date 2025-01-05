namespace Redpoint.CloudFramework.Startup
{
    using Microsoft.Extensions.Logging;
    using StackExchange.Redis;
    using StackExchange.Redis.Maintenance;
    using StackExchange.Redis.Profiling;
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

#pragma warning disable CS8766
    internal class ConnectionMultiplexerProxy : IConnectionMultiplexer
#pragma warning restore CS8766
    {
        private Lazy<ConnectionMultiplexer> _connectionMultiplexerLazy;

        public ConnectionMultiplexerProxy(string redisServer, ILogger<ConnectionMultiplexerProxy> logger)
        {
            _connectionMultiplexerLazy = new Lazy<ConnectionMultiplexer>(() => ConnectToRedis(redisServer, logger)!);
        }

        private ConnectionMultiplexer ConnectionMultiplexer
        {
            get
            {
                return _connectionMultiplexerLazy.Value;
            }
        }

        internal static string? GetRedisConnectionString(string redisServer)
        {
            if (string.IsNullOrWhiteSpace(redisServer))
            {
                return null;
            }

            var serverName = redisServer;
            var portSuffix = string.Empty;
            if (serverName.Contains(':', StringComparison.InvariantCultureIgnoreCase))
            {
                var s = serverName.Split(_hostPortSeparator, 2);
                serverName = s[0];
                portSuffix = ":" + s[1];
            }

            string redisConnect;
            if (IPAddress.TryParse(serverName, out var address))
            {
                // Server component is a direct network address; use
                // original Redis server string as-is.
                redisConnect = redisServer;
            }
            else
            {
                // Lookup based on DNS.
                var dnsTask = Dns.GetHostAddressesAsync(serverName);
                var addresses = new List<string>();
                foreach (var dnsEntry in dnsTask.Result)
                {
                    if (dnsEntry.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        addresses.Add(dnsEntry.MapToIPv4().ToString() + portSuffix);
                    }
                    else if (dnsEntry.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    {
                        addresses.Add($"[{dnsEntry.MapToIPv6().ToString()}]{portSuffix}");
                    }
                }
                redisConnect = string.Join(",", addresses);
            }

            return redisConnect + ",allowAdmin=true";
        }

        private static ConnectionMultiplexer? ConnectToRedis(
            string redisServer,
            ILogger<ConnectionMultiplexerProxy> logger)
        {
            var redisConnect = GetRedisConnectionString(redisServer);

            if (redisConnect == null)
            {
                return null;
            }

            ConnectionMultiplexer? connectionMultiplexer = null;
            var now = DateTime.Now;
            for (var i = 0; i < 30; i++)
            {
                try
                {
                    logger?.LogInformation($"Attempting to connect to Redis {redisConnect} (attempt #{i + 1})...");
                    connectionMultiplexer = ConnectionMultiplexer.Connect(redisConnect);
                    break;
                }
                catch (RedisConnectionException)
                {
                    logger?.LogWarning($"Failed to connect to Redis (attempt #{i + 1}), waiting {i} seconds before trying again...");
                    Thread.Sleep(i * 1000);
                    continue;
                }
            }

            if (connectionMultiplexer == null)
            {
                logger?.LogCritical($"Unable to connect to Redis after 30 attempts and {Math.Round((DateTime.Now - now).TotalMinutes, 0)} minutes... something is drastically wrong!");
                throw new InvalidOperationException("Unable to connect to Redis!");
            }

            return connectionMultiplexer;
        }

        #region Proxied Methods

        public string ClientName => ConnectionMultiplexer.ClientName;

        public string Configuration => ConnectionMultiplexer.Configuration;

        public int TimeoutMilliseconds => ConnectionMultiplexer.TimeoutMilliseconds;

        public long OperationCount => ConnectionMultiplexer.OperationCount;

#pragma warning disable CS0618
        public bool PreserveAsyncOrder { get => ConnectionMultiplexer.PreserveAsyncOrder; set => ConnectionMultiplexer.PreserveAsyncOrder = value; }
#pragma warning restore CS0618

        public bool IsConnected => ConnectionMultiplexer.IsConnected;

        public bool IsConnecting => ConnectionMultiplexer.IsConnecting;

        [Obsolete]
        public bool IncludeDetailInExceptions { get => ConnectionMultiplexer.IncludeDetailInExceptions; set => ConnectionMultiplexer.IncludeDetailInExceptions = value; }

        public int StormLogThreshold { get => ConnectionMultiplexer.StormLogThreshold; set => ConnectionMultiplexer.StormLogThreshold = value; }

        internal static readonly char[] _hostPortSeparator = new[] { ':' };

        public event EventHandler<RedisErrorEventArgs> ErrorMessage
        {
            add
            {
                ConnectionMultiplexer.ErrorMessage += value;
            }

            remove
            {
                ConnectionMultiplexer.ErrorMessage -= value;
            }
        }

        public event EventHandler<ConnectionFailedEventArgs> ConnectionFailed
        {
            add
            {
                ConnectionMultiplexer.ConnectionFailed += value;
            }

            remove
            {
                ConnectionMultiplexer.ConnectionFailed -= value;
            }
        }

        public event EventHandler<InternalErrorEventArgs> InternalError
        {
            add
            {
                ConnectionMultiplexer.InternalError += value;
            }

            remove
            {
                ConnectionMultiplexer.InternalError -= value;
            }
        }

        public event EventHandler<ConnectionFailedEventArgs> ConnectionRestored
        {
            add
            {
                ConnectionMultiplexer.ConnectionRestored += value;
            }

            remove
            {
                ConnectionMultiplexer.ConnectionRestored -= value;
            }
        }

        public event EventHandler<EndPointEventArgs> ConfigurationChanged
        {
            add
            {
                ConnectionMultiplexer.ConfigurationChanged += value;
            }

            remove
            {
                ConnectionMultiplexer.ConfigurationChanged -= value;
            }
        }

        public event EventHandler<EndPointEventArgs> ConfigurationChangedBroadcast
        {
            add
            {
                ConnectionMultiplexer.ConfigurationChangedBroadcast += value;
            }

            remove
            {
                ConnectionMultiplexer.ConfigurationChangedBroadcast -= value;
            }
        }

        public event EventHandler<HashSlotMovedEventArgs> HashSlotMoved
        {
            add
            {
                ConnectionMultiplexer.HashSlotMoved += value;
            }

            remove
            {
                ConnectionMultiplexer.HashSlotMoved -= value;
            }
        }

        public event EventHandler<ServerMaintenanceEvent> ServerMaintenanceEvent
        {
            add
            {
                ConnectionMultiplexer.ServerMaintenanceEvent += value;
            }

            remove
            {
                ConnectionMultiplexer.ServerMaintenanceEvent -= value;
            }
        }

        public void RegisterProfiler(Func<ProfilingSession?> profilingSessionProvider)
        {
            ConnectionMultiplexer.RegisterProfiler(profilingSessionProvider);
        }

        public ServerCounters GetCounters()
        {
            return ConnectionMultiplexer.GetCounters();
        }

        public EndPoint[] GetEndPoints(bool configuredOnly = false)
        {
            return ConnectionMultiplexer.GetEndPoints(configuredOnly);
        }

        public void Wait(Task task)
        {
            ConnectionMultiplexer.Wait(task);
        }

        public T Wait<T>(Task<T> task)
        {
            return ((IConnectionMultiplexer)ConnectionMultiplexer).Wait(task);
        }

        public void WaitAll(params Task[] tasks)
        {
            ConnectionMultiplexer.WaitAll(tasks);
        }

        public int HashSlot(RedisKey key)
        {
            return ConnectionMultiplexer.HashSlot(key);
        }

        public ISubscriber GetSubscriber(object? asyncState = null)
        {
            return ConnectionMultiplexer.GetSubscriber(asyncState);
        }

        public IDatabase GetDatabase(int db = -1, object? asyncState = null)
        {
            return ConnectionMultiplexer.GetDatabase(db, asyncState);
        }

        public IServer GetServer(string host, int port, object? asyncState = null)
        {
            return ConnectionMultiplexer.GetServer(host, port, asyncState);
        }

        public IServer GetServer(string hostAndPort, object? asyncState = null)
        {
            return ConnectionMultiplexer.GetServer(hostAndPort, asyncState);
        }

        public IServer GetServer(IPAddress host, int port)
        {
            return ConnectionMultiplexer.GetServer(host, port);
        }

        public IServer GetServer(EndPoint endpoint, object? asyncState = null)
        {
            return ConnectionMultiplexer.GetServer(endpoint, asyncState);
        }

        public Task<bool> ConfigureAsync(TextWriter? log = null)
        {
            return ConnectionMultiplexer.ConfigureAsync(log);
        }

        public bool Configure(TextWriter? log = null)
        {
            return ConnectionMultiplexer.Configure(log);
        }

        public string GetStatus()
        {
            return ConnectionMultiplexer.GetStatus();
        }

        public void GetStatus(TextWriter log)
        {
            ConnectionMultiplexer.GetStatus(log);
        }

        public void Close(bool allowCommandsToComplete = true)
        {
            ConnectionMultiplexer.Close(allowCommandsToComplete);
        }

        public Task CloseAsync(bool allowCommandsToComplete = true)
        {
            return ConnectionMultiplexer.CloseAsync(allowCommandsToComplete);
        }

        public string? GetStormLog()
        {
            return ConnectionMultiplexer.GetStormLog();
        }

        public void ResetStormLog()
        {
            ConnectionMultiplexer.ResetStormLog();
        }

        public long PublishReconfigure(CommandFlags flags = CommandFlags.None)
        {
            return ConnectionMultiplexer.PublishReconfigure(flags);
        }

        public Task<long> PublishReconfigureAsync(CommandFlags flags = CommandFlags.None)
        {
            return ConnectionMultiplexer.PublishReconfigureAsync(flags);
        }

        public int GetHashSlot(RedisKey key)
        {
            return ConnectionMultiplexer.GetHashSlot(key);
        }

        public void ExportConfiguration(Stream destination, ExportOptions options = (ExportOptions)(-1))
        {
            ConnectionMultiplexer.ExportConfiguration(destination, options);
        }

        public void Dispose()
        {
            ConnectionMultiplexer.Dispose();
        }

        public IServer[] GetServers()
        {
            return ConnectionMultiplexer.GetServers();
        }

        public void AddLibraryNameSuffix(string suffix)
        {
            ConnectionMultiplexer.AddLibraryNameSuffix(suffix);
        }

        public ValueTask DisposeAsync()
        {
            return ConnectionMultiplexer.DisposeAsync();
        }

        #endregion
    }
}
