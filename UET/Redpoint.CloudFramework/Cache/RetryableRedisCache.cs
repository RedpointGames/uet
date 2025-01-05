namespace Redpoint.CloudFramework.Cache
{
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.Extensions.Caching.StackExchangeRedis;
    using Microsoft.Extensions.Options;
    using StackExchange.Redis;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class RetryableRedisCache : IDistributedCache, IDisposable
    {
        private readonly RedisCache _cache;
        private const int _retryCount = 50;

        public RetryableRedisCache(IOptions<RedisCacheOptions> redisOptions)
        {
            _cache = new RedisCache(redisOptions);
        }

        public void Dispose()
        {
            _cache.Dispose();
        }

        public byte[]? Get(string key)
        {
            return RetryableRedisCache.Retry(() => _cache.Get(key));
        }

        public Task<byte[]?> GetAsync(string key)
        {
            return RetryableRedisCache.RetryAsync(() => _cache.GetAsync(key));
        }

        public void Refresh(string key)
        {
            RetryableRedisCache.Retry(() => _cache.Refresh(key));
        }

        public Task RefreshAsync(string key)
        {
            return RetryableRedisCache.RetryAsync(() => _cache.RefreshAsync(key));
        }

        public void Remove(string key)
        {
            RetryableRedisCache.Retry(() => _cache.Remove(key));
        }

        public Task RemoveAsync(string key)
        {
            return RetryableRedisCache.RetryAsync(() => _cache.RemoveAsync(key));
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            RetryableRedisCache.Retry(() => _cache.Set(key, value, options));
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            return RetryableRedisCache.RetryAsync(() => _cache.SetAsync(key, value, options));
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default(CancellationToken))
        {
            return RetryableRedisCache.RetryAsync(() => _cache.GetAsync(key, token));
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default(CancellationToken))
        {
            return RetryableRedisCache.RetryAsync(() => _cache.SetAsync(key, value, options, token));
        }

        public Task RefreshAsync(string key, CancellationToken token = default(CancellationToken))
        {
            return RetryableRedisCache.RetryAsync(() => _cache.RefreshAsync(key, token));
        }

        public Task RemoveAsync(string key, CancellationToken token = default(CancellationToken))
        {
            return RetryableRedisCache.RetryAsync(() => _cache.RemoveAsync(key, token));
        }

        #region Retry Logic

        private static void Retry(Action func)
        {
            for (var i = 0; i < _retryCount; i++)
            {
                try
                {
                    func();
                    return;
                }
                catch (NullReferenceException)
                {
                    // Bug in Microsoft.Extensions.Caching.Redis! See https://github.com/aspnet/Caching/issues/270.
                    if (i == _retryCount - 1)
                    {
                        throw;
                    }

                    Thread.Sleep(i * 100);
                    continue;
                }
                catch (TimeoutException)
                {
                    if (i == _retryCount - 1)
                    {
                        throw;
                    }

                    Thread.Sleep(i * 100);
                    continue;
                }
                catch (RedisConnectionException ex) when (ex.Message.Contains("BUSY", StringComparison.Ordinal))
                {
                    if (i == _retryCount - 1)
                    {
                        throw;
                    }

                    Thread.Sleep(i * 100);
                    continue;
                }
            }

            throw new InvalidOperationException("Should not be able to escape for loop in Retry() of RetryableRedisCache");
        }

        private static T Retry<T>(Func<T> func)
        {
            for (var i = 0; i < _retryCount; i++)
            {
                try
                {
                    return func();
                }
                catch (NullReferenceException)
                {
                    // Bug in Microsoft.Extensions.Caching.Redis! See https://github.com/aspnet/Caching/issues/270.
                    if (i == _retryCount - 1)
                    {
                        throw;
                    }

                    Thread.Sleep(i * 100);
                    continue;
                }
                catch (TimeoutException)
                {
                    if (i == _retryCount - 1)
                    {
                        throw;
                    }

                    Thread.Sleep(i * 100);
                    continue;
                }
                catch (RedisConnectionException ex) when (ex.Message.Contains("BUSY", StringComparison.Ordinal))
                {
                    if (i == _retryCount - 1)
                    {
                        throw;
                    }

                    Thread.Sleep(i * 100);
                    continue;
                }
            }

            throw new InvalidOperationException("Should not be able to escape for loop in Retry<T>() of RetryableRedisCache");
        }

        private static async Task RetryAsync(Func<Task> func)
        {
            for (var i = 0; i < _retryCount; i++)
            {
                try
                {
                    await func().ConfigureAwait(false);
                    return;
                }
                catch (NullReferenceException)
                {
                    // Bug in Microsoft.Extensions.Caching.Redis! See https://github.com/aspnet/Caching/issues/270.
                    if (i == _retryCount - 1)
                    {
                        throw;
                    }

                    await Task.Delay(i * 100).ConfigureAwait(false);
                    continue;
                }
                catch (TimeoutException)
                {
                    if (i == _retryCount - 1)
                    {
                        throw;
                    }

                    await Task.Delay(i * 100).ConfigureAwait(false);
                    continue;
                }
                catch (RedisConnectionException ex) when (ex.Message.Contains("BUSY", StringComparison.Ordinal))
                {
                    if (i == _retryCount - 1)
                    {
                        throw;
                    }

                    await Task.Delay(i * 100).ConfigureAwait(false);
                    continue;
                }
            }

            throw new InvalidOperationException("Should not be able to escape for loop in RetryAsync() of RetryableRedisCache");
        }

        private static async Task<T> RetryAsync<T>(Func<Task<T>> func)
        {
            for (var i = 0; i < _retryCount; i++)
            {
                try
                {
                    return await func().ConfigureAwait(false);
                }
                catch (NullReferenceException)
                {
                    // Bug in Microsoft.Extensions.Caching.Redis! See https://github.com/aspnet/Caching/issues/270.
                    if (i == _retryCount - 1)
                    {
                        throw;
                    }

                    await Task.Delay(i * 100).ConfigureAwait(false);
                    continue;
                }
                catch (TimeoutException)
                {
                    if (i == _retryCount - 1)
                    {
                        throw;
                    }

                    await Task.Delay(i * 100).ConfigureAwait(false);
                    continue;
                }
                catch (RedisConnectionException ex) when (ex.Message.Contains("BUSY", StringComparison.Ordinal))
                {
                    if (i == _retryCount - 1)
                    {
                        throw;
                    }

                    await Task.Delay(i * 100).ConfigureAwait(false);
                    continue;
                }
            }

            throw new InvalidOperationException("Should not be able to escape for loop in RetryAsync<T>() of RetryableRedisCache");
        }

        #endregion
    }
}
