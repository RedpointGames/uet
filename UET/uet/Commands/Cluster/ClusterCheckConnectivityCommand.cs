using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redpoint.Concurrency;
using Redpoint.KubernetesManager.Services;
using Redpoint.ServiceControl;
using Redpoint.Windows.HostNetworkingService;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;

namespace UET.Commands.Cluster
{
    internal sealed class ClusterCheckConnectivityCommand
    {
        internal sealed class Options
        {
            public Option<FileInfo> HealthCheckPath = new Option<FileInfo>("--health-check-path");
        }

        public static Command CreateClusterCheckConnectivityCommand()
        {
            var options = new Options();
            var command = new Command(
                "check-connectivity",
                "Perform various connectivity tests in a loop, exiting if any fail.");
            command.IsHidden = true;
            command.AddAllOptions(options);
            command.AddCommonHandler<ClusterCheckConnectivityCommandInstance>(options);
            return command;
        }

        private sealed class ClusterCheckConnectivityCommandInstance : ICommandInstance
        {
            private readonly ILogger<ClusterCheckConnectivityCommandInstance> _logger;
            private readonly Options _options;

            public ClusterCheckConnectivityCommandInstance(
                ILogger<ClusterCheckConnectivityCommandInstance> logger,
                Options options)
            {
                _logger = logger;
                _options = options;
            }

            private static readonly (string host, int port)[] _httpListenAddresses =
                OperatingSystem.IsWindows()
                ? [
                    ("127.0.0.1", 55001),
                    ("[::1]", 55002),
                    ("*", 55003),
                    ("+", 55004),
                  ]
                : [
                    ("127.0.0.1", 55001),
                    // Linux does not like '[::1]' as a hostname specifically for HTTP listener.
                    ("*", 55003),
                    ("+", 55004),
                  ];
            private static readonly IPEndPoint[] _tcpListenAddresses =
            [
                new (IPAddress.Loopback, 55005),
                new (IPAddress.Any, 55006),
                new (IPAddress.IPv6Loopback, 55007),
                new (IPAddress.IPv6Any, 55008),
            ];
            private static readonly IPEndPoint[] _udpListenAddresses =
            [
                new (IPAddress.Loopback, 55009),
                new (IPAddress.Any, 55010),
                new (IPAddress.IPv6Loopback, 55011),
                new (IPAddress.IPv6Any, 55012),
            ];
            private static readonly string[] _domainsToResolve =
            [
                "one.one.one.one",
                "google.com",
                "localhost"
            ];

            private static async Task HttpLoop(HttpListener listener, CancellationToken cancellationToken)
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var context = await listener.GetContextAsync().AsCancellable(cancellationToken);

                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        using (var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8, leaveOpen: true))
                        {
                            writer.WriteLine("OK");
                        }
                        context.Response.OutputStream.Flush();
                        context.Response.Close();
                    }
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine($"failure in HTTP loop: {ex}");
                }
            }

            private static async Task TcpLoop(TcpListener listener, CancellationToken cancellationToken)
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var client = await listener.AcceptTcpClientAsync(cancellationToken);

                        using (var writer = new StreamWriter(client.GetStream()))
                        {
                            await writer.WriteAsync("OK");
                            await writer.FlushAsync(cancellationToken);
                        }
                    }
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine($"failure in TCP loop: {ex}");
                }
            }

            private static async Task UdpLoop(UdpClient client, CancellationToken cancellationToken)
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var result = await client.ReceiveAsync(cancellationToken);
                        var bytes = Encoding.UTF8.GetBytes("OK");
                        await client.SendAsync(bytes, result.RemoteEndPoint, cancellationToken);
                    }
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine($"failure in UDP loop: {ex}");
                }
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var healthCheckPath = context.ParseResult.GetValueForOption(_options.HealthCheckPath);

                while (!context.GetCancellationToken().IsCancellationRequested)
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.GetCancellationToken());
                    var disposable = new List<IDisposable>();
                    var tasks = new List<Task>();
                    var anyFailure = false;

                    try
                    {
                        // Test HTTP listening.
                        foreach (var pair in _httpListenAddresses)
                        {
                            var address = $"http://{pair.host}:{pair.port}/";
                            try
                            {
                                Console.Write($"[{DateTimeOffset.UtcNow}] Testing HTTP listener on {address} ...");
                                var listener = new HttpListener();
                                disposable.Add(listener);
                                listener.Prefixes.Add(address);
                                listener.Start();

                                tasks.Add(Task.Run(async () => await HttpLoop(listener, cts.Token), cts.Token));

                                Console.WriteLine(" okay.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(" fail!");

                                Console.WriteLine();
                                Console.WriteLine(ex);
                                Console.WriteLine();

                                anyFailure = true;
                            }
                        }

                        // Test TCP listening.
                        foreach (var endpoint in _tcpListenAddresses)
                        {
                            try
                            {
                                Console.Write($"[{DateTimeOffset.UtcNow}] Testing TCP listener on {endpoint} ...");
                                var listener = new TcpListener(endpoint);
                                disposable.Add(listener);
                                listener.Start();

                                tasks.Add(Task.Run(async () => await TcpLoop(listener, cts.Token), cts.Token));

                                Console.WriteLine(" okay.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(" fail!");

                                Console.WriteLine();
                                Console.WriteLine(ex);
                                Console.WriteLine();

                                anyFailure = true;
                            }
                        }

                        // Test UDP listening.
                        foreach (var endpoint in _udpListenAddresses)
                        {
                            try
                            {
                                Console.Write($"[{DateTimeOffset.UtcNow}] Testing UDP listener on {endpoint} ...");
                                var listener = new UdpClient(endpoint);
                                disposable.Add(listener);

                                tasks.Add(Task.Run(async () => await UdpLoop(listener, cts.Token), cts.Token));

                                Console.WriteLine(" okay.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(" fail!");

                                Console.WriteLine();
                                Console.WriteLine(ex);
                                Console.WriteLine();

                                anyFailure = true;
                            }
                        }

                        // If we do not have any failures at this point, proceed to connection tests.
                        if (!anyFailure)
                        {
                            // Test HTTP clients.
                            foreach (var pair in _httpListenAddresses)
                            {
                                var subaddresses = new List<string>();
                                if (pair.host == "*" || pair.host == "+")
                                {
                                    var isWindows = OperatingSystem.IsWindows();
                                    foreach (var subaddress in NetworkInterface.GetAllNetworkInterfaces()
                                        .Where(x => x.OperationalStatus == OperationalStatus.Up)
                                        .SelectMany(adapter =>
                                            adapter?.GetIPProperties()
                                                ?.UnicastAddresses
                                                ?.Where(x => isWindows || x.Address.AddressFamily == AddressFamily.InterNetwork)
                                                ?.Select(x =>
                                                    x.Address.AddressFamily == AddressFamily.InterNetworkV6
                                                        ? $"[{x.Address}]"
                                                        : x.Address.ToString())
                                                ?.ToHashSet() ?? []))
                                    {
                                        subaddresses.Add(subaddress.ToString());
                                    }
                                    subaddresses.Add("localhost");
                                }
                                else
                                {
                                    subaddresses.Add(pair.host);
                                }

                                foreach (var subaddress in subaddresses)
                                {
                                    var subaddressWithPort = $"http://{subaddress}:{pair.port}/";

                                    Console.Write($"[{DateTimeOffset.UtcNow}] Testing HTTP client to {subaddressWithPort} ...");
                                    try
                                    {
                                        using var client = new HttpClient();
                                        var result = await client.GetStringAsync(new Uri(subaddressWithPort), cts.Token);
                                        if (result?.Trim() != "OK")
                                        {
                                            throw new InvalidOperationException("expected 'OK' from HTTP endpoint");
                                        }

                                        Console.WriteLine(" okay.");
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine(" fail!");

                                        Console.WriteLine();
                                        Console.WriteLine(ex);
                                        Console.WriteLine();

                                        anyFailure = true;
                                    }
                                }
                            }

                            // Test TCP clients.
                            foreach (var endpoint in _tcpListenAddresses)
                            {
                                var subendpoints = new List<IPEndPoint>();
                                if (endpoint.Address == IPAddress.Any)
                                {
                                    foreach (var subaddress in NetworkInterface.GetAllNetworkInterfaces()
                                        .Where(x => x.OperationalStatus == OperationalStatus.Up)
                                        .SelectMany(adapter =>
                                            adapter?.GetIPProperties()
                                                ?.UnicastAddresses
                                                ?.Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork)
                                                ?.Select(x => x.Address)
                                                ?.ToHashSet() ?? []))
                                    {
                                        subendpoints.Add(new(subaddress, endpoint.Port));
                                    }
                                }
                                else if (endpoint.Address == IPAddress.IPv6Any)
                                {
                                    foreach (var subaddress in NetworkInterface.GetAllNetworkInterfaces()
                                        .Where(x => x.OperationalStatus == OperationalStatus.Up)
                                        .SelectMany(adapter =>
                                            adapter?.GetIPProperties()
                                                ?.UnicastAddresses
                                                ?.Where(x => x.Address.AddressFamily == AddressFamily.InterNetworkV6)
                                                ?.Select(x => x.Address)
                                                ?.ToHashSet() ?? []))
                                    {
                                        subendpoints.Add(new(subaddress, endpoint.Port));
                                    }
                                }
                                else
                                {
                                    subendpoints.Add(endpoint);
                                }

                                foreach (var subendpoint in subendpoints)
                                {
                                    Console.Write($"[{DateTimeOffset.UtcNow}] Testing TCP client to {subendpoint} ...");
                                    try
                                    {
                                        using var client = new TcpClient();
                                        await client.ConnectAsync(subendpoint);
                                        using (var reader = new StreamReader(client.GetStream(), Encoding.UTF8))
                                        {
                                            var result = await reader.ReadToEndAsync();
                                            if (result?.Trim() != "OK")
                                            {
                                                throw new InvalidOperationException("expected 'OK' from TCP endpoint");
                                            }
                                        }
                                        client.Close();

                                        Console.WriteLine(" okay.");
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine(" fail!");

                                        Console.WriteLine();
                                        Console.WriteLine(ex);
                                        Console.WriteLine();

                                        anyFailure = true;
                                    }
                                }
                            }

                            // Test UDP clients.
                            foreach (var endpoint in _udpListenAddresses)
                            {
                                var subendpoints = new List<IPEndPoint>();
                                if (endpoint.Address == IPAddress.Any)
                                {
                                    foreach (var subaddress in NetworkInterface.GetAllNetworkInterfaces()
                                        .Where(x => x.OperationalStatus == OperationalStatus.Up)
                                        .SelectMany(adapter =>
                                            adapter?.GetIPProperties()
                                                ?.UnicastAddresses
                                                ?.Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork)
                                                ?.Select(x => x.Address)
                                                ?.ToHashSet() ?? []))
                                    {
                                        subendpoints.Add(new(subaddress, endpoint.Port));
                                    }
                                }
                                else if (endpoint.Address == IPAddress.IPv6Any)
                                {
                                    foreach (var subaddress in NetworkInterface.GetAllNetworkInterfaces()
                                        .Where(x => x.OperationalStatus == OperationalStatus.Up)
                                        .SelectMany(adapter =>
                                            adapter?.GetIPProperties()
                                                ?.UnicastAddresses
                                                ?.Where(x => x.Address.AddressFamily == AddressFamily.InterNetworkV6)
                                                ?.Select(x => x.Address)
                                                ?.ToHashSet() ?? []))
                                    {
                                        subendpoints.Add(new(subaddress, endpoint.Port));
                                    }
                                }
                                else
                                {
                                    subendpoints.Add(endpoint);
                                }

                                foreach (var subendpoint in subendpoints)
                                {
                                    Console.Write($"[{DateTimeOffset.UtcNow}] Testing UDP client to {subendpoint} ...");
                                    try
                                    {
                                        using var client = new UdpClient(subendpoint.AddressFamily);
                                        await client.SendAsync(new byte[] { 1 }, 1, subendpoint);
                                        var result = await client.ReceiveAsync();
                                        if (Encoding.UTF8.GetString(result.Buffer) != "OK")
                                        {
                                            throw new InvalidOperationException("expected 'OK' from UDP endpoint");
                                        }
                                        client.Close();

                                        Console.WriteLine(" okay.");
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine(" fail!");

                                        Console.WriteLine();
                                        Console.WriteLine(ex);
                                        Console.WriteLine();

                                        anyFailure = true;
                                    }
                                }
                            }
                        }

                        // Test DNS resolution.
                        foreach (var domain in _domainsToResolve)
                        {
                            Console.Write($"[{DateTimeOffset.UtcNow}] Resolving domain name {domain} ...");
                            try
                            {
                                var result = await Dns.GetHostAddressesAsync(domain);
                                if (result.Length == 0)
                                {
                                    throw new InvalidOperationException("expected to resolve at least one entry!");
                                }

                                Console.WriteLine(" okay.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(" fail!");

                                Console.WriteLine();
                                Console.WriteLine(ex);
                                Console.WriteLine();

                                anyFailure = true;
                            }
                        }

                        // Create or delete the health check file based on whether we're passing right now.
                        if (healthCheckPath != null)
                        {
                            if (anyFailure)
                            {
                                try
                                {
                                    File.Delete(healthCheckPath.FullName);
                                    Console.WriteLine($"Deleted health check file because health checks aren't passing: {healthCheckPath.FullName}");
                                }
                                catch
                                {
                                }
                            }
                            else
                            {
                                try
                                {
                                    File.WriteAllText(healthCheckPath.FullName, "ok");
                                    Console.WriteLine($"Created or updated health check file because health checks are passing: {healthCheckPath.FullName}");
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                    finally
                    {
                        cts.Cancel();

                        foreach (var t in tasks)
                        {
                            try
                            {
                                await t;
                            }
                            catch
                            {
                            }
                        }

                        foreach (var d in disposable)
                        {
                            try
                            {
                                d.Dispose();
                            }
                            catch
                            {
                            }
                        }

                        cts.Dispose();

                        if (!context.GetCancellationToken().IsCancellationRequested)
                        {
                            // Wait 5 seconds if we're not cancelling.
                            try
                            {
                                await Task.Delay(5000, context.GetCancellationToken());
                            }
                            catch (OperationCanceledException) when (context.GetCancellationToken().IsCancellationRequested)
                            {
                            }
                        }
                    }
                }

                return 0;
            }
        }
    }
}
