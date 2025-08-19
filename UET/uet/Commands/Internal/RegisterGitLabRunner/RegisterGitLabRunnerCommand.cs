namespace UET.Commands.Internal.RegisterGitLabRunner
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http.Json;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal sealed class RegisterGitLabRunnerCommand
    {
        internal sealed class Options
        {
        }

        public static Command CreateRegisterGitLabRunnerCommand()
        {
            var options = new Options();
            var command = new Command(
                "register-gitlab-runner",
                "Register a GitLab runner with the GitLab API using the new authentication flow.");
            command.AddAllOptions(options);
            command.AddCommonHandler<RegisterGitLabRunnerCommandInstance>(options);
            return command;
        }

        private sealed class RegisterGitLabRunnerCommandInstance : ICommandInstance
        {
            private readonly ILogger<RegisterGitLabRunnerCommandInstance> _logger;

            public RegisterGitLabRunnerCommandInstance(
                ILogger<RegisterGitLabRunnerCommandInstance> logger)
            {
                _logger = logger;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var baseUrl = Environment.GetEnvironmentVariable("UET_GITLAB_BASE_URL");
                var personalAccessToken = Environment.GetEnvironmentVariable("UET_GITLAB_PERSONAL_ACCESS_TOKEN");
                var registrationSpecRaw = Environment.GetEnvironmentVariable("UET_GITLAB_REGISTER_SPEC");
                GitLabRunnerRegistrationSpec[] registrationSpec;
                try
                {
                    registrationSpec = JsonSerializer.Deserialize(registrationSpecRaw!, RegisterGitLabRunnerJsonSerializerContext.Default.GitLabRunnerRegistrationSpecArray)!;
                    ArgumentNullException.ThrowIfNull(registrationSpec);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Environment variable 'UET_GITLAB_REGISTER_SPEC' was invalid.");
                    return 1;
                }
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    _logger.LogError("Environment variable 'UET_GITLAB_BASE_URL' was invalid.");
                    return 1;
                }
                if (string.IsNullOrWhiteSpace(personalAccessToken))
                {
                    _logger.LogError("Environment variable 'UET_GITLAB_PERSONAL_ACCESS_TOKEN' was invalid.");
                    return 1;
                }

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add(
                        "PRIVATE-TOKEN",
                        personalAccessToken);

                    // Iterate through registration specs, registering where necessary.
                    foreach (var spec in registrationSpec)
                    {
                        if (File.Exists(spec.IdPath))
                        {
                            _logger.LogInformation($"Checking if existing runner with ID at path {spec.IdPath} is already registered...");
                            try
                            {
                                var id = (await File.ReadAllTextAsync(spec.IdPath, context.GetCancellationToken()).ConfigureAwait(false)).Trim();
                                var json = await client.GetFromJsonAsync(
                                    $"{baseUrl}/api/v4/runners/{id}",
                                    RegisterGitLabRunnerJsonSerializerContext.Default.GitLabGetRunnerResponse);
                                _logger.LogInformation($"Runner with ID at path {spec.IdPath} is already registered.");
                                continue;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Runner is not registered or failed to read from GitLab.");
                            }
                        }

                        var form = new Dictionary<string, string>
                        {
                            { "runner_type", spec.RunnerType },
                        };
                        if (spec.GroupId.HasValue)
                        {
                            form.Add("group_id", spec.GroupId.Value.ToString(CultureInfo.InvariantCulture));
                        }
                        if (spec.ProjectId.HasValue)
                        {
                            form.Add("project_id", spec.ProjectId.Value.ToString(CultureInfo.InvariantCulture));
                        }
                        if (!string.IsNullOrWhiteSpace(spec.Description))
                        {
                            form.Add("description", spec.Description
                                .Replace(
                                    "__HOSTNAME__",
                                    Environment.MachineName.ToLowerInvariant(),
                                    StringComparison.Ordinal));
                        }
                        form.Add("paused", spec.Paused ? "true" : "false");
                        form.Add("locked", spec.Locked ? "true" : "false");
                        form.Add("run_untagged", spec.RunUntagged ? "true" : "false");
                        if (!string.IsNullOrWhiteSpace(spec.TagList))
                        {
                            form.Add("tag_list", spec.TagList
                                .Replace(
                                    "__HOSTNAME__",
                                    Environment.MachineName.ToLowerInvariant(),
                                    StringComparison.Ordinal));
                        }
                        if (!string.IsNullOrWhiteSpace(spec.AccessLevel))
                        {
                            form.Add("access_level", spec.AccessLevel);
                        }
                        if (spec.MaximumTimeout.HasValue)
                        {
                            form.Add("maximum_timeout", spec.MaximumTimeout.Value.ToString(CultureInfo.InvariantCulture));
                        }
                        if (!string.IsNullOrWhiteSpace(spec.MaintainenceNote))
                        {
                            form.Add("maintenance_note", spec.MaintainenceNote);
                        }

                        _logger.LogInformation($"Registering new GitLab runner...");
                        var response = await client.PostAsync(
                            new Uri($"{baseUrl}/api/v4/user/runners"),
                            new FormUrlEncodedContent(form),
                            context.GetCancellationToken());
                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogError($"Got unexpected response from GitLab: {await response.Content.ReadAsStringAsync(context.GetCancellationToken())}");
                            response.EnsureSuccessStatusCode();
                        }

                        var register = await response.Content.ReadFromJsonAsync(
                            RegisterGitLabRunnerJsonSerializerContext.Default.GitLabRegisterRunnerResponse,
                            context.GetCancellationToken());
                        if (register == null)
                        {
                            throw new InvalidOperationException("Expected non-null response from runner registration.");
                        }

                        var idPathDirectory = Path.GetDirectoryName(spec.IdPath);
                        var tokenPathDirectory = Path.GetDirectoryName(spec.TokenPath);
                        if (!string.IsNullOrWhiteSpace(idPathDirectory))
                        {
                            Directory.CreateDirectory(idPathDirectory);
                        }
                        if (!string.IsNullOrWhiteSpace(tokenPathDirectory))
                        {
                            Directory.CreateDirectory(tokenPathDirectory);
                        }
                        await File.WriteAllTextAsync(
                            spec.IdPath,
                            register.Id.ToString(CultureInfo.InvariantCulture),
                            context.GetCancellationToken());
                        await File.WriteAllTextAsync(
                            spec.TokenPath,
                            register.Token,
                            context.GetCancellationToken());

                        _logger.LogInformation($"Runner with ID at path {spec.IdPath} now successfully registered.");
                    }
                }

                return 0;
            }
        }
    }
}
