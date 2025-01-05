namespace Redpoint.CloudFramework.Repository.Redis
{
    using Microsoft.Extensions.Caching.StackExchangeRedis;
    using Microsoft.Extensions.Options;
    using StackExchange.Redis;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    internal class DistributedCacheExtended : IDistributedCacheExtended, IDisposable
    {
        private const string _clearCacheLuaScript =
            "for _,k in ipairs(redis.call('KEYS', ARGV[1])) do\n" +
            "    redis.call('DEL', k)\n" +
            "end";
        private const string _getKeysLuaScript = "return redis.call('keys', ARGV[1])";
        private readonly RedisCacheOptions _options;
        private ConnectionMultiplexer? _connection;
        private IDatabase? _cache;
        private bool _isDisposed;

        public DistributedCacheExtended(IOptions<RedisCacheOptions> redisCacheOptions)
        {
            _options = redisCacheOptions.Value;
        }

        ~DistributedCacheExtended()
        {
            Dispose(false);
        }

        public async Task ClearAsync()
        {
            ThrowIfDisposed();
            await EnsureInitialized().ConfigureAwait(false);
            await _cache!.ScriptEvaluateAsync(
                _clearCacheLuaScript,
                values: new RedisValue[]
                {
                    _options.InstanceName + "*"
                }).ConfigureAwait(false);
        }

        public async Task<IEnumerable<string>> GetKeysAsync()
        {
            ThrowIfDisposed();
            await EnsureInitialized().ConfigureAwait(false);
            var result = await _cache!.ScriptEvaluateAsync(
                _getKeysLuaScript,
                values: new RedisValue[]
                {
                    _options.InstanceName + "*"
                }).ConfigureAwait(false);
            return ((RedisResult[])result!).Select(x => x.ToString()!.Substring(_options.InstanceName!.Length)).ToArray();
        }

        public async Task RemoveAsync(string[] keys)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(keys);
            await EnsureInitialized().ConfigureAwait(false);
            var keysArray = keys.Select(x => (RedisKey)(_options.InstanceName + x)).ToArray();
            await _cache!.KeyDeleteAsync(keysArray).ConfigureAwait(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected async Task EnsureInitialized()
        {
            if (_connection == null)
            {
                _connection = await ConnectionMultiplexer.ConnectAsync(_options.Configuration!).ConfigureAwait(false);
                _cache = _connection.GetDatabase();
            }
        }

        private void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing && _connection != null)
                {
                    _connection.Close();
                }

                _isDisposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
        }
    }
}
