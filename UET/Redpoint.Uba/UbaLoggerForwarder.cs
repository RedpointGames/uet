namespace Redpoint.Uba
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uba.Native;
    using System;
    using System.Runtime.InteropServices;

    internal partial class UbaLoggerForwarder : IDisposable
    {
        private readonly ILogger<UbaLoggerForwarder> _logger;
        private readonly nint _ubaLogger;
        private bool _hasDisposed;

        #region Library Imports

        static UbaLoggerForwarder()
        {
            UbaNative.ThrowIfNotInitialized();
        }

        private delegate void BeginScopeCallback();
        private delegate void EndScopeCallback();
        private delegate void LogCallback(byte logEntryType, nint str, uint strLen, nint prefix, uint prefixLen);

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static partial nint CreateCallbackLogWriter(
            BeginScopeCallback beginScope,
            EndScopeCallback endScope,
            LogCallback log);

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static partial void DestroyCallbackLogWriter(
            nint ubaLogger);

        #endregion

        public UbaLoggerForwarder(ILogger<UbaLoggerForwarder> logger)
        {
            _logger = logger;
            _ubaLogger = CreateCallbackLogWriter(
                BeginScope,
                EndScope,
                Log);
            if (_ubaLogger == nint.Zero)
            {
                throw new InvalidOperationException("Unable to create UBA logger!");
            }
        }

        public nint Logger => _ubaLogger;

        private static void BeginScope()
        {
        }

        private static void EndScope()
        {
        }

        private void Log(byte logEntryType, nint str, uint strLen, nint prefix, uint prefixLen)
        {
            string message;
            if (OperatingSystem.IsWindows())
            {
                message = Marshal.PtrToStringUni(str, (int)strLen) ?? string.Empty;
            }
            else
            {
                message = Marshal.PtrToStringUTF8(str, (int)strLen) ?? string.Empty;
            }

            switch (logEntryType)
            {
                case 0:
                    _logger.LogError(message);
                    break;
                case 1:
                    _logger.LogWarning(message);
                    break;
                case 2:
                    _logger.LogInformation(message);
                    break;
                case 3:
                case 4:
                default:
                    _logger.LogDebug(message);
                    break;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_hasDisposed)
            {
                if (_ubaLogger != nint.Zero)
                {
                    DestroyCallbackLogWriter(_ubaLogger);
                }

                _hasDisposed = true;
            }
        }

        ~UbaLoggerForwarder()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
