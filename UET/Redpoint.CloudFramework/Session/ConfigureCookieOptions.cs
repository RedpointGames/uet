namespace Redpoint.CloudFramework.Session
{
    using Microsoft.AspNetCore.Authentication.Cookies;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    public class ConfigureCookieOptions : IPostConfigureOptions<CookieAuthenticationOptions>
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ConfigureCookieOptions(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }

        public void PostConfigure(string? name, CookieAuthenticationOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var provider = scope.ServiceProvider;

                options.SessionStore = provider.GetRequiredService<ITicketStore>();
            }
        }
    }
}
