namespace Io
{
    using GitLabApiClient;
    using GitLabApiClient.Internal.Paths;
    using GitLabApiClient.Models.Webhooks.Requests;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;

    public class GitLabWebhookConfigurationService : IHostedService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GitLabWebhookConfigurationService> _logger;
        private readonly IHostEnvironment _hostEnvironment;

        public GitLabWebhookConfigurationService(IConfiguration configuration, ILogger<GitLabWebhookConfigurationService> logger, IHostEnvironment hostEnvironment)
        {
            _configuration = configuration;
            _logger = logger;
            _hostEnvironment = hostEnvironment;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_hostEnvironment.IsProduction())
            {
                return Task.CompletedTask;
            }

            // Intentionally run on a background task; don't block startup.
            _ = Task.Run(async () =>
            {
                try
                {
                    var accessToken = _configuration.GetValue<string>("GitLab:AccessToken");
                    if (string.IsNullOrWhiteSpace(accessToken))
                    {
                        _logger.LogWarning("Skipping webhook provisioning because GitLab access token is not set.");
                        return;
                    }

                    var gitlabDomain = _configuration.GetValue<string>("GitLab:Domain");
                    var webhookBaseUrl = _configuration.GetValue<string>("GitLab:WebhookBaseUrl");
                    if (!string.IsNullOrWhiteSpace(gitlabDomain) && !string.IsNullOrWhiteSpace(webhookBaseUrl))
                    {
                        var webhookUrl = webhookBaseUrl + "/webhook/gitlab";
                        var gitlabClient = new GitLabClient("https://" + gitlabDomain, accessToken);
                        var projects = await gitlabClient.Groups.GetProjectsAsync(_configuration.GetValue<int>("GitLab:GroupId"));
                        foreach (var project in projects)
                        {
                            var webhooks = await gitlabClient.Webhooks.GetAsync(project.Id);
                            var existingWebhook = webhooks.FirstOrDefault(x => x.Url.Contains(webhookUrl, StringComparison.Ordinal));
                            if (existingWebhook == null)
                            {
                                _logger.LogInformation($"Creating webhook for project {project.PathWithNamespace} ...");
                                await gitlabClient.Webhooks.CreateAsync(project.Id, new CreateWebhookRequest(webhookUrl)
                                {
                                    EnableSslVerification = true,
                                    PipelineEvents = true,
                                    JobEvents = true,
                                });
                            }
                            else
                            {
                                _logger.LogInformation($"Updating webhook for project {project.PathWithNamespace} ...");
                                await gitlabClient.Webhooks.UpdateAsync(project.Id, existingWebhook.Id, new CreateWebhookRequest(webhookUrl)
                                {
                                    EnableSslVerification = true,
                                    PipelineEvents = true,
                                    JobEvents = true,
                                });
                            }
                        }
                        _logger.LogInformation($"Webhook synchronisation finished.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);
                }
            }, cancellationToken);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
