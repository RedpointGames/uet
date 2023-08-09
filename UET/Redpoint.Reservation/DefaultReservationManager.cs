namespace Redpoint.Reservation
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32.SafeHandles;
    using System;
    using System.Collections.Concurrent;
    using System.Numerics;
    using System.Reflection.Metadata;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultReservationManager : IReservationManager
    {
        private readonly string _rootPath;
        private readonly string _lockPath;
        private readonly ILogger<DefaultReservationManager> _logger;
        private readonly string _metaPath;
        private static ConcurrentDictionary<string, bool> _localReservations = new ConcurrentDictionary<string, bool>();

        private string GetStabilityHash(string inputString, int? length)
        {
            using (var sha = SHA256.Create())
            {
                var inputBytes = sha.ComputeHash(Encoding.ASCII.GetBytes(inputString));
                const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz.-_";
                var dividend = new BigInteger(inputBytes);
                var builder = new StringBuilder();
                while (dividend != 0)
                {
                    dividend = BigInteger.DivRem(dividend, alphabet.Length, out BigInteger remainder);
                    builder.Insert(0, alphabet[Math.Abs((int)remainder)]);
                }
                if (!length.HasValue)
                {
                    return builder.ToString().Trim('.');
                }
                else
                {
                    return builder.ToString().Substring(0, length.Value).Trim('.');
                }
            }
        }


        private static void CreateDirectory(string path)
        {
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                Directory.CreateDirectory(
                    path,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
            }
        }

        public DefaultReservationManager(
            ILogger<DefaultReservationManager> logger,
            string rootPath)
        {
            _rootPath = rootPath;
            CreateDirectory(_rootPath);
            _lockPath = Path.Combine(_rootPath, ".lock");
            CreateDirectory(_lockPath);
            _metaPath = Path.Combine(_rootPath, ".meta");
            CreateDirectory(_metaPath);
            _logger = logger;
        }

        private class Reservation : IReservation
        {
            private readonly SafeFileHandle _handle;
            private readonly string _metaPath;
            private readonly Action _localReservationRelease;

            public string ReservedPath { get; }

            public Reservation(
                SafeFileHandle handle,
                string reservedPath,
                string metaPath,
                Action localReservationRelease)
            {
                _handle = handle;
                ReservedPath = reservedPath;
                _metaPath = metaPath;
                _localReservationRelease = localReservationRelease;
            }

            public ValueTask DisposeAsync()
            {
                File.WriteAllText(_metaPath, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
                _localReservationRelease();
                _handle.Close();
                return ValueTask.CompletedTask;
            }
        }

        public Task<IReservation> ReserveAsync(string @namespace, params string[] parameters)
        {
            var id = GetStabilityHash($"{@namespace}:{string.Join("-", parameters)}", 14);
            for (int i = 0; i < 1000; i++)
            {
                var targetName = $"{id}-{i}";
                if (_localReservations.TryAdd(targetName, true))
                {
                    try
                    {
                        var handle = File.OpenHandle(
                            Path.Combine(_lockPath, targetName),
                            FileMode.Create,
                            FileAccess.ReadWrite,
                            FileShare.None,
                            FileOptions.DeleteOnClose);
                        var reservedPath = Path.Combine(_rootPath, targetName);
                        Directory.CreateDirectory(reservedPath);
                        _logger.LogInformation($"Reservation target '{targetName}' has been acquired in this process.");
                        File.WriteAllLines(Path.Combine(_metaPath, "desc." + targetName), new[] { @namespace }.Concat(parameters));
                        File.WriteAllText(Path.Combine(_metaPath, "date." + targetName), DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
                        return Task.FromResult<IReservation>(new Reservation(
                            handle,
                            reservedPath,
                            Path.Combine(_metaPath, "date." + targetName),
                            () =>
                            {
                                _logger.LogInformation($"Reservation target '{targetName}' has been released in this process.");
                                _localReservations.Remove(targetName, out _);
                            }));
                    }
                    catch (IOException ex) when (ex.Message.Contains("another process"))
                    {
                        // Attempt the next reservation.
                        _logger.LogInformation($"Reservation target '{targetName}' is in use by another process.");
                        _localReservations.Remove(targetName, out _);
                        continue;
                    }
                }
                else
                {
                    _logger.LogInformation($"Reservation target '{targetName}' is internally used elsewhere in this process.");
                }
            }

            throw new InvalidOperationException($"There are too many reservations for the stability ID '{id}'!");
        }

        public async Task<IReservation> ReserveExactAsync(string name, CancellationToken cancellationToken)
        {
            return (await ReserveExactInternalAsync(
                name,
                cancellationToken,
                false))!;
        }

        public Task<IReservation?> TryReserveExactAsync(string name)
        {
            return ReserveExactInternalAsync(
                name,
                CancellationToken.None,
                true);
        }

        public IReservation? TryReserveExact(string targetName)
        {
            if (_localReservations.TryAdd(targetName, true))
            {
                try
                {
                    var handle = File.OpenHandle(
                        Path.Combine(_lockPath, targetName),
                        FileMode.Create,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        FileOptions.DeleteOnClose);
                    var reservedPath = Path.Combine(_rootPath, targetName);
                    Directory.CreateDirectory(reservedPath);
                    _logger.LogInformation($"Reservation target '{targetName}' has been acquired in this process.");
                    File.WriteAllLines(Path.Combine(_metaPath, "desc." + targetName), new[] { targetName });
                    File.WriteAllText(Path.Combine(_metaPath, "date." + targetName), DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
                    return new Reservation(
                        handle,
                        reservedPath,
                        Path.Combine(_metaPath, "date." + targetName),
                        () =>
                        {
                            _logger.LogInformation($"Reservation target '{targetName}' has been released in this process.");
                            _localReservations.Remove(targetName, out _);
                        });
                }
                catch (IOException ex) when (ex.Message.Contains("another process"))
                {
                    // Attempt the next reservation.
                    _logger.LogInformation($"Reservation target '{targetName}' is in use by another process.");
                    _localReservations.Remove(targetName, out _);
                    return null;
                }
            }
            else
            {
                _logger.LogInformation($"Reservation target '{targetName}' is internally used elsewhere in this process.");
                return null;
            }
        }

        private async Task<IReservation?> ReserveExactInternalAsync(string targetName, CancellationToken cancellationToken, bool allowFailure)
        {
            do
            {
                if (_localReservations.TryAdd(targetName, true))
                {
                    try
                    {
                        var handle = File.OpenHandle(
                            Path.Combine(_lockPath, targetName),
                            FileMode.Create,
                            FileAccess.ReadWrite,
                            FileShare.None,
                            FileOptions.DeleteOnClose);
                        var reservedPath = Path.Combine(_rootPath, targetName);
                        Directory.CreateDirectory(reservedPath);
                        _logger.LogInformation($"Reservation target '{targetName}' has been acquired in this process.");
                        File.WriteAllLines(Path.Combine(_metaPath, "desc." + targetName), new[] { targetName });
                        File.WriteAllText(Path.Combine(_metaPath, "date." + targetName), DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
                        return new Reservation(
                            handle,
                            reservedPath,
                            Path.Combine(_metaPath, "date." + targetName),
                            () =>
                            {
                                _logger.LogInformation($"Reservation target '{targetName}' has been released in this process.");
                                _localReservations.Remove(targetName, out _);
                            });
                    }
                    catch (IOException ex) when (ex.Message.Contains("another process"))
                    {
                        // Attempt the next reservation.
                        _logger.LogInformation($"Reservation target '{targetName}' is in use by another process.");
                        _localReservations.Remove(targetName, out _);
                        if (allowFailure)
                        {
                            return null;
                        }
                        else
                        {
                            await Task.Delay(1000, cancellationToken);
                            continue;
                        }
                    }
                }
                else
                {
                    _logger.LogInformation($"Reservation target '{targetName}' is internally used elsewhere in this process.");
                    if (allowFailure)
                    {
                        return null;
                    }
                    else
                    {
                        await Task.Delay(100, cancellationToken);
                        continue;
                    }
                }
            }
            while (!cancellationToken.IsCancellationRequested);
            if (allowFailure)
            {
                return null;
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new OperationCanceledException();
            }
        }
    }
}
