namespace Redpoint.Uba
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uba.Native;
    using System;
    using System.Runtime.InteropServices;

    internal partial class UbaLoggerForwarder
    {
        private static ILogger<UbaLoggerForwarder>? _logger;
        private static nint? _ubaLogger;

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

        public static nint GetUbaLogger(ILogger<UbaLoggerForwarder> logger)
        {
            if (_ubaLogger.HasValue)
            {
                return _ubaLogger.Value;
            }

            _logger = logger;
            _ubaLogger = CreateCallbackLogWriter(
                BeginScope,
                EndScope,
                Log);
            if (_ubaLogger == nint.Zero)
            {
                throw new InvalidOperationException("Unable to create UBA logger!");
            }

            return _ubaLogger.Value;
        }

        private static void BeginScope()
        {
        }

        private static void EndScope()
        {
        }

        private static void Log(byte logEntryType, nint str, uint strLen, nint prefix, uint prefixLen)
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
                    _logger!.LogError(message);
                    break;
                case 1:
                    _logger!.LogWarning(message);
                    break;
                case 2:
                    _logger!.LogInformation(message);
                    break;
                case 3:
                case 4:
                default:
                    _logger!.LogDebug(message);
                    break;
            }
        }
    }
}
