namespace Redpoint.CloudFramework.DataProtection
{
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    public class StaticDataProtectionProvider : IDataProtectionProvider
    {
        private readonly IHostEnvironment _hostEnvironment;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StaticDataProtector> _logger;

        public StaticDataProtectionProvider(IHostEnvironment hostEnvironment, IConfiguration configuration, ILogger<StaticDataProtector> logger)
        {
            _hostEnvironment = hostEnvironment;
            _configuration = configuration;
            _logger = logger;
        }

        public IDataProtector CreateProtector(string purpose)
        {
            return new StaticDataProtector(_hostEnvironment, _configuration, _logger);
        }
    }
}
