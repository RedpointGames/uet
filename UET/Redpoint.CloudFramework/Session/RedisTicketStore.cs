namespace Redpoint.CloudFramework.Session
{
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authentication.Cookies;
    using Microsoft.Extensions.Caching.Distributed;
    using System;
    using System.Security.Claims;
    using System.Threading.Tasks;

    public class RedisTicketStore : ITicketStore
    {
        private const string _keyPrefix = "TKT:";

        private readonly IDistributedCache _cache;

        public RedisTicketStore(IDistributedCache cache)
        {
            _cache = cache;
        }

        public async Task<string> StoreAsync(AuthenticationTicket ticket)
        {
            ArgumentNullException.ThrowIfNull(ticket);

            var key = _keyPrefix + ticket.AuthenticationScheme + ":";
            var sub = ticket.Principal.FindFirstValue("sub");
            if (sub != null)
            {
                key += sub + ":" + Guid.NewGuid();
            }
            else
            {
                var name = ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
                if (name != null)
                {
                    key += name + ":" + Guid.NewGuid();
                }
                else
                {
                    key += Guid.NewGuid();
                }
            }
            await RenewAsync(key, ticket).ConfigureAwait(false);
            return key;
        }

        public Task RenewAsync(string key, AuthenticationTicket ticket)
        {
            ArgumentNullException.ThrowIfNull(ticket);

            var options = new DistributedCacheEntryOptions();
            var expiresUtc = ticket.Properties.ExpiresUtc;
            if (expiresUtc.HasValue)
            {
                options.SetAbsoluteExpiration(expiresUtc.Value);
            }
            byte[] val = SerializeToBytes(ticket);
            _cache.Set(key, val, options);
            return Task.FromResult(0);
        }

        public Task<AuthenticationTicket?> RetrieveAsync(string key)
        {
            return Task.FromResult(DeserializeFromBytes(_cache.Get(key)));
        }

        public Task RemoveAsync(string key)
        {
            _cache.Remove(key);
            return Task.FromResult(0);
        }

        private static byte[] SerializeToBytes(AuthenticationTicket source)
        {
            return TicketSerializer.Default.Serialize(source);
        }

        private static AuthenticationTicket? DeserializeFromBytes(byte[]? source)
        {
            return source == null ? null : TicketSerializer.Default.Deserialize(source);
        }
    }
}
