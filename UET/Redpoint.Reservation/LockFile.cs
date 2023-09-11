namespace Redpoint.Reservation
{
    using Microsoft.Win32.SafeHandles;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a lightweight lockfile that can be used across platforms.
    /// Unlike <see cref="IReservationManager"/> which provides directories to
    /// work within, <see cref="LockFile"/> allows you to take locks on
    /// individual files without requiring the use of asynchronous APIs or
    /// dependency injection.
    /// </summary>
    public sealed class LockFile : IDisposable
    {
        private static ConcurrentDictionary<string, bool> _localLocks = new ConcurrentDictionary<string, bool>(OperatingSystem.IsWindows() ? StringComparer.InvariantCultureIgnoreCase : StringComparer.InvariantCulture);
        private readonly SafeFileHandle _handle;
        private readonly string _path;

        /// <summary>
        /// Try to obtain a lock file at the specified path. If the file already
        /// exists and is locked, this returns <c>null</c>.
        /// </summary>
        /// <param name="path">The path to the lock file.</param>
        /// <returns>Either the <see cref="IDisposable"/> that should be disposed when done with the lock, or <c>null</c> if the lock could not be obtained.</returns>
        public static IDisposable? TryObtainLock(string path)
        {
            path = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
            var parentPath = Path.GetDirectoryName(path);
            if (parentPath != null)
            {
                Directory.CreateDirectory(parentPath);
            }
            if (_localLocks.TryAdd(path, true))
            {
                try
                {
                    var handle = File.OpenHandle(
                        path,
                        FileMode.Create,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        FileOptions.DeleteOnClose);
                    return new LockFile(handle, path);
                }
                catch (IOException ex) when (ex.Message.Contains("another process", StringComparison.Ordinal))
                {
                    _localLocks.Remove(path, out _);
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        private LockFile(SafeFileHandle handle, string path)
        {
            _handle = handle;
            _path = path;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _handle.Close();
            _localLocks.Remove(_path, out _);
        }
    }
}
