namespace Redpoint.CloudFramework.React
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// This hosted service runs "webpack --watch --mode development" while
    /// the app is being run under a debugger in development. This is because
    /// the built-in ASP.NET Core functionality surrounding webpack is all designed
    /// around SPAs and proxying requests, but we really just need to update the
    /// built content on disk and then get the web browser to reload the page.
    /// </summary>
    public sealed class WebpackDevWatchHostedService : IHostedService, IDisposable
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<WebpackDevWatchHostedService> _logger;

        private Process? _webpack;
        private bool _expectExit;

        public WebpackDevWatchHostedService(
            IWebHostEnvironment webHostEnvironment,
            ILogger<WebpackDevWatchHostedService> logger)
        {
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
            _expectExit = false;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                FileName = Path.Combine(_webHostEnvironment.ContentRootPath, "ClientApp", "node_modules", ".bin", "webpack.cmd"),
                ArgumentList =
                {
                    "--mode",
                    "development",
                    "--watch"
                },
                WorkingDirectory = Path.Combine(_webHostEnvironment.ContentRootPath, "ClientApp"),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            if (File.Exists(Path.Combine(_webHostEnvironment.ContentRootPath, "ClientApp", "tsconfig.webpack.json")))
            {
                startInfo.EnvironmentVariables.Add("TS_NODE_PROJECT", "tsconfig.webpack.json");
            }

            _webpack = new Process();
            _webpack.StartInfo = startInfo;
            _webpack.Exited += OnWebpackExited;
            _webpack.OutputDataReceived += OnWebpackOutput;
            _webpack.ErrorDataReceived += OnWebpackError;
            _webpack.EnableRaisingEvents = true;
            _webpack.Start();
            _webpack.BeginOutputReadLine();
            _webpack.BeginErrorReadLine();

            return Task.CompletedTask;
        }

        private void OnWebpackError(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _logger.LogInformation(e.Data.Trim());
            }
        }

        private void OnWebpackOutput(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _logger.LogInformation(e.Data.Trim());
            }
        }

        private async void OnWebpackExited(object? sender, EventArgs e)
        {
            if (_expectExit)
            {
                return;
            }

            _logger.LogInformation("webpack --watch exited unexpectedly, restarting in 1000ms.");
            _webpack = null;
            await Task.Delay(1000).ConfigureAwait(false);
            if (_expectExit)
            {
                return;
            }

            await StartAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _expectExit = true;
            if (_webpack != null)
            {
                _webpack.Kill();
                _webpack.Dispose();
                _webpack = null;
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _expectExit = true;
            if (_webpack != null)
            {
                _webpack.Kill();
                _webpack.Dispose();
                _webpack = null;
            }
        }
    }
}
