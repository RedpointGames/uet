namespace UET.Commands.Internal.SetupAppleTwoFactorProxy
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using Microsoft.Extensions.Logging;
    using System.Net.Http.Headers;
    using System.Text.Json.Serialization.Metadata;
    using System.Net.Http.Json;
    using System.Runtime.CompilerServices;
    using System.Reflection;
    using System.Text.Json;

    internal class SetupAppleTwoFactoryProxyCommand
    {
        public class Options
        {
            public Option<string> CloudflareApiToken;
            public Option<string> CloudflareAccountId;
            public Option<string> CloudflareWorkerName;
            public Option<string> PlivoAuthId;
            public Option<string> PlivoAuthToken;

            public Options()
            {
                CloudflareApiToken = new Option<string>(
                    "--cloudflare-api-token",
                    "The Cloudflare API token that should be used to deploy the Cloudflare Worker. You can create a token at https://dash.cloudflare.com/profile/api-tokens.")
                {
                    IsRequired = true
                };
                CloudflareAccountId = new Option<string>(
                    "--cloudflare-account-id",
                    "The Cloudflare account ID that the Cloudflare Worker should be created under. This can be found in the URL when viewing the Cloudflare Dashboard, at https://dash.cloudflare.com/(account ID shown here).")
                {
                    IsRequired = true
                };
                CloudflareWorkerName = new Option<string>(
                    "--cloudflare-worker-name",
                    description: "The name of the Cloudflare Worker to deploy. You should not need to change this.",
                    getDefaultValue: () => "uet-apple-2fa-proxy");

                PlivoAuthId = new Option<string>(
                    "--plivo-auth-id",
                    "The Plivo 'Auth ID', found on the Plivo Dashboard at https://console.plivo.com/dashboard/.")
                {
                    IsRequired = true
                };
                PlivoAuthToken = new Option<string>(
                    "--plivo-auth-token",
                    "The Plivo 'Auth Token', found on the Plivo Dashboard at https://console.plivo.com/dashboard/.")
                {
                    IsRequired = true
                };
            }
        }

        public static Command CreateSetupAppleTwoFactoryProxyCommand()
        {
            var options = new Options();
            var command = new Command(
                "setup-apple-two-factor-proxy",
                description: "Deploys a Cloudflare Worker and connects it to a preprovisioned phone number on Plivo so that UET can retrieve the two-factor code when authenticating Apple accounts. UET needs to authenticate with an Apple account to download Xcode on demand, but Apple accounts only permit two-factor codes through phone numbers these days.");
            command.AddAllOptions(options);
            command.AddCommonHandler<SetupAppleTwoFactoryProxyCommandInstance>(options);
            return command;
        }

        public class SetupAppleTwoFactoryProxyCommandInstance : ICommandInstance
        {
            private readonly ILogger<SetupAppleTwoFactoryProxyCommandInstance> _logger;
            private readonly Options _options;

            public SetupAppleTwoFactoryProxyCommandInstance(
                ILogger<SetupAppleTwoFactoryProxyCommandInstance> logger,
                Options options)
            {
                _logger = logger;
                _options = options;
            }

            private bool HandleCloudflareResult<T>(CloudflareResult<T> result)
            {
                if (result.Errors != null)
                {
                    foreach (var error in result.Errors)
                    {
                        _logger.LogError($"Error from Cloudflare: ({error.Code}) {error.Message}");
                    }
                }

                if (result.Messages != null)
                {
                    foreach (var message in result.Messages)
                    {
                        _logger.LogError($"Message from Cloudflare: ({message.Code}) {message.Message}");
                    }
                }

                if (!result.Success)
                {
                    _logger.LogError("Cloudflare returned an error response. See above for errors.");
                    return false;
                }

                return true;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var cloudflareApiToken = context.ParseResult.GetValueForOption(_options.CloudflareApiToken)!;
                var cloudflareAccountId = context.ParseResult.GetValueForOption(_options.CloudflareAccountId)!;
                var cloudflareWorkerName = context.ParseResult.GetValueForOption(_options.CloudflareWorkerName)!;
                var plivoAuthId = context.ParseResult.GetValueForOption(_options.PlivoAuthId)!;
                var plivoAuthToken = context.ParseResult.GetValueForOption(_options.PlivoAuthToken)!;

                using (var plivoClient = new HttpClient())
                {
                    var plivoBasic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{plivoAuthId}:{plivoAuthToken}"));
                    plivoClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", plivoBasic);

                    // Find a phone number with the same name as the Cloudflare Worker.
                    string? existingPhoneNumber = null;
                    await foreach (var phoneNumber in FetchPlivoListAsync(
                        plivoClient,
                        $"https://api.plivo.com/v1/Account/{plivoAuthId}/Number/",
                        PlivoJsonSerializerContext.Default.PlivoListPlivoNumber,
                        context.GetCancellationToken()))
                    {
                        if (phoneNumber.Alias == cloudflareWorkerName)
                        {
                            existingPhoneNumber = phoneNumber.Number;
                            break;
                        }
                    }
                    if (existingPhoneNumber == null)
                    {
                        _logger.LogError($"Could not find a phone number in Plivo with the alias '{cloudflareWorkerName}'. Since virtual phone numbers incur billing charges, this command will not automatically allocate a phone number for you. Buy a United States phone number in the Plivo dashboard at https://console.plivo.com/phone-numbers/search/ with SMS capability, set its alias to be '{cloudflareWorkerName}' and then run this command again.");
                        return 1;
                    }
                    _logger.LogInformation($"Using existing Plivo phone number: {existingPhoneNumber}");

                    string twoFactorProxyUrl;
                    using (var cfClient = new HttpClient())
                    {
                        cfClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cloudflareApiToken);

                        // Get the Cloudflare Workers subdomain for routing.
                        var subdomain = await cfClient.GetFromJsonAsync(
                            $"https://api.cloudflare.com/client/v4/accounts/{cloudflareAccountId}/workers/subdomain",
                            CloudflareJsonSerializerContext.Default.CloudflareResultCloudflareSubdomain);
                        if (!HandleCloudflareResult(subdomain!))
                        {
                            return 1;
                        }
                        var domain = $"{cloudflareWorkerName}.{subdomain!.Result!.Subdomain}.workers.dev";
                        _logger.LogInformation($"Deploying Cloudflare Worker to route: {domain}");

                        // Create the KV namespace that we'll use to store the latest Apple 2FA code.
                        string? kvNamespaceId = null;
                        await foreach (var ns in FetchCloudflareListAsync(
                            cfClient,
                            $"https://api.cloudflare.com/client/v4/accounts/{cloudflareAccountId}/storage/kv/namespaces",
                            CloudflareJsonSerializerContext.Default.CloudflareResultCloudflareKvNamespaceArray,
                            context.GetCancellationToken()))
                        {
                            if (ns.Title == cloudflareWorkerName)
                            {
                                kvNamespaceId = ns.Id;
                                _logger.LogInformation($"Using existing Cloudflare KV namespace: {kvNamespaceId}");
                                break;
                            }
                        }
                        if (kvNamespaceId == null)
                        {
                            var createResponse = await cfClient.PostAsJsonAsync(
                                $"https://api.cloudflare.com/client/v4/accounts/{cloudflareAccountId}/storage/kv/namespaces",
                                new CloudflareKvNamespaceCreate
                                {
                                    Title = cloudflareWorkerName
                                },
                                CloudflareJsonSerializerContext.Default.CloudflareKvNamespaceCreate,
                                context.GetCancellationToken());
                            var createResponseJson = JsonSerializer.Deserialize(
                                await createResponse.Content.ReadAsStringAsync(),
                                CloudflareJsonSerializerContext.Default.CloudflareResultCloudflareKvNamespace);
                            if (!HandleCloudflareResult(createResponseJson!))
                            {
                                return 1;
                            }
                            kvNamespaceId = createResponseJson!.Result!.Id;
                            _logger.LogInformation($"Created new Cloudflare KV namespace: {kvNamespaceId}");
                        }

                        // Create or upload the worker.
                        var request = new HttpRequestMessage(
                            HttpMethod.Put,
                            $"https://api.cloudflare.com/client/v4/accounts/{cloudflareAccountId}/workers/scripts/{cloudflareWorkerName}");
                        var workerResourceName = typeof(SetupAppleTwoFactoryProxyCommandInstance).Namespace + ".worker.js";
                        var workerResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(workerResourceName);
                        if (workerResourceStream == null)
                        {
                            throw new InvalidOperationException($"Missing resource: {workerResourceName}");
                        }
                        var workerResourceStreamContent = new StreamContent(workerResourceStream);
                        workerResourceStreamContent.Headers.ContentType = new MediaTypeHeaderValue("application/javascript+module");
                        var content = new MultipartFormDataContent
                        {
                            { new StringContent("Automated deployment of Cloudflare Worker for UET Apple 2FA Proxy."), "message" },
                            { workerResourceStreamContent, "worker.js", "worker.js" },
                            {
                                new StringContent(
                                    /*lang=json,strict*/ 
                                    $$"""
                                    {
                                        "main_module": "worker.js",
                                        "bindings": [
                                            {
                                                "name": "KV",
                                                "type": "kv_namespace",
                                                "namespace_id": "{{kvNamespaceId}}"
                                            },
                                            {
                                                "name": "UET_INBOUND_PHONE_NUMBER",
                                                "type": "plain_text",
                                                "text": "{{existingPhoneNumber}}"
                                            }
                                        ]
                                    }
                                    """
                                ),
                                "metadata"
                            },
                        };
                        request.Content = content;
                        var response = await cfClient.SendAsync(request);
                        var result = JsonSerializer.Deserialize(
                            await response.Content.ReadAsStringAsync(),
                            CloudflareJsonSerializerContext.Default.CloudflareResultCloudflareWorker)!;
                        if (!HandleCloudflareResult(result))
                        {
                            return 1;
                        }
                        if (result.Result == null)
                        {
                            _logger.LogError("Cloudflare indicated success but the response was malformed.");
                            return 1;
                        }
                        _logger.LogInformation($"Created or updated Cloudflare Worker: {result.Result.Id}");

                        // Enable the subdomain for the Cloudflare Worker. This is an undocumented API, but it's used
                        // by Wrangler, so it's fairly safe to rely on.
                        response = await cfClient.PostAsJsonAsync(
                            $"https://api.cloudflare.com/client/v4/accounts/{cloudflareAccountId}/workers/scripts/{cloudflareWorkerName}/subdomain",
                            new CloudflareSubdomainEnable
                            {
                                Enabled = true,
                            },
                            CloudflareJsonSerializerContext.Default.CloudflareSubdomainEnable);
                        var subdomainResult = JsonSerializer.Deserialize(
                            await response.Content.ReadAsStringAsync(),
                            CloudflareJsonSerializerContext.Default.CloudflareResultObject);
                        if (!HandleCloudflareResult(subdomainResult!))
                        {
                            return 1;
                        }
                        _logger.LogInformation($"Enabled Cloudflare Worker to deploy on workers.dev.");

                        twoFactorProxyUrl = $"https://{cloudflareWorkerName}.{subdomain!.Result!.Subdomain}.workers.dev";
                    }

                    // See if we have an application for the 2FA proxy.
                    string? existingApplicationId = null;
                    await foreach (var application in FetchPlivoListAsync(
                        plivoClient,
                        $"https://api.plivo.com/v1/Account/{plivoAuthId}/Application/",
                        PlivoJsonSerializerContext.Default.PlivoListPlivoApplication,
                        context.GetCancellationToken()))
                    {
                        if (application.AppName == cloudflareWorkerName)
                        {
                            if (application.MessageUrl != twoFactorProxyUrl)
                            {
                                var updatedApplicationResponse = await plivoClient.PostAsJsonAsync(
                                    $"https://api.plivo.com/v1/Account/{plivoAuthId}/Application/{application.AppId}/",
                                    new PlivoApplicationUpdateRequest
                                    {
                                        MessageUrl = twoFactorProxyUrl,
                                    },
                                    PlivoJsonSerializerContext.Default.PlivoApplicationUpdateRequest,
                                    context.GetCancellationToken());
                                updatedApplicationResponse.EnsureSuccessStatusCode();
                                _logger.LogInformation($"Updated existing Plivo application: {application.AppName}");
                            }
                            else
                            {
                                _logger.LogInformation($"Using existing Plivo application: {application.AppName}");
                            }
                            existingApplicationId = application.AppId;
                        }
                    }
                    if (existingApplicationId == null)
                    {
                        var createdApplicationResponse = await plivoClient.PostAsJsonAsync(
                            $"https://api.plivo.com/v1/Account/{plivoAuthId}/Application/",
                            new PlivoApplicationCreateRequest
                            {
                                AppName = cloudflareWorkerName,
                                MessageUrl = twoFactorProxyUrl,
                            },
                            PlivoJsonSerializerContext.Default.PlivoApplicationCreateRequest,
                            context.GetCancellationToken());
                        createdApplicationResponse.EnsureSuccessStatusCode();
                        var createdApplicationResponseJson = JsonSerializer.Deserialize(
                            await createdApplicationResponse.Content.ReadAsStringAsync(),
                            PlivoJsonSerializerContext.Default.PlivoApplicationCreateResponse);
                        existingApplicationId = createdApplicationResponseJson!.AppId!;
                        _logger.LogInformation($"Created new Plivo application: {cloudflareWorkerName}");
                    }

                    // Ensure the phone number is associated with the Plivo application that points at the Cloudflare Worker.
                    var updatedNumberResponse = await plivoClient.PostAsJsonAsync(
                        $"https://api.plivo.com/v1/Account/{plivoAuthId}/Number/{existingPhoneNumber}/",
                        new PlivoNumberUpdateRequest
                        {
                            AppId = existingApplicationId,
                        },
                        PlivoJsonSerializerContext.Default.PlivoNumberUpdateRequest,
                        context.GetCancellationToken());
                    updatedNumberResponse.EnsureSuccessStatusCode();
                    _logger.LogInformation($"Associated Plivo phone number '{existingPhoneNumber}' with Plivo application {existingApplicationId}.");

                    _logger.LogInformation(string.Empty);
                    _logger.LogInformation("Success! The two-factor proxy for Apple accounts has been configured. The next steps are:");
                    _logger.LogInformation($"1. Open https://appleid.apple.com/account/manage/section/security in your browser.");
                    _logger.LogInformation($"2. Add '{existingPhoneNumber}' as a trusted phone number, and remove any existing trusted phone numbers.");
                    _logger.LogInformation($"3. When prompted for the 2FA code, head to '{twoFactorProxyUrl}?number={existingPhoneNumber}'. If there's no code yet, refresh the page every few seconds until the proxy receives the SMS.");
                    _logger.LogInformation($"4. Set the following environment variables in your build server configuration:");
                    _logger.LogInformation($"   UET_APPLE_EMAIL=...");
                    _logger.LogInformation($"   UET_APPLE_PASSWORD=...");
                    _logger.LogInformation($"   UET_APPLE_PHONE_NUMBER={existingPhoneNumber}");
                    _logger.LogInformation($"   UET_APPLE_2FA_PROXY_URL={twoFactorProxyUrl}");
                }

                return 0;
            }

            private async IAsyncEnumerable<T> FetchCloudflareListAsync<T>(
                HttpClient client,
                string url,
                JsonTypeInfo<CloudflareResult<T[]>> typeInfo,
                [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var page = await client.GetFromJsonAsync(
                    url,
                    typeInfo,
                    cancellationToken);
                foreach (var r in page!.Result!)
                {
                    yield return r!;
                }
            }

            private async IAsyncEnumerable<T> FetchPlivoListAsync<T>(
                HttpClient client,
                string url,
                JsonTypeInfo<PlivoList<T>> typeInfo,
                [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var page = await client.GetFromJsonAsync(
                    url,
                    typeInfo,
                    cancellationToken);
                foreach (var o in page!.Objects!)
                {
                    yield return o;
                }
                while (page!.Meta!.Next != null)
                {
                    var uri = new Uri(url);
                    url = $"{uri.Scheme}://{uri.Host}{page!.Meta!.Next}";
                    page = await client.GetFromJsonAsync(
                        url,
                        typeInfo,
                        cancellationToken);
                    foreach (var o in page!.Objects!)
                    {
                        yield return o;
                    }
                }
            }
        }
    }
}
