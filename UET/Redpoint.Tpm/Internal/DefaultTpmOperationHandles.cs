namespace Redpoint.Tpm.Internal
{
    using Microsoft.Extensions.Logging;
    using Tpm2Lib;

    internal class DefaultTpmOperationHandles : ITpmOperationHandles
    {
        private readonly ILogger _logger;

        internal Tpm2Device? _tpmDevice;
        internal Tpm2? _tpm;
        internal TpmHandle? _ekHandle;
        internal TpmPublic? _ekPublic;
        internal TpmHandle? _aikHandle;
        internal TpmPublic? _aikPublic;
        private bool _disposedValue;

        public Tpm2Device TpmDevice => _tpmDevice!;
        public Tpm2 Tpm => _tpm!;
        public TpmHandle EkHandle => _ekHandle!;
        public TpmPublic EkPublic => _ekPublic!;
        public TpmHandle AikHandle => _aikHandle!;
        public TpmPublic AikPublic => _aikPublic!;

        public DefaultTpmOperationHandles(ILogger logger)
        {
            _logger = logger;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (_aikHandle != null && _tpm != null)
                    {
                        _logger.LogTrace("Disposing AIK handle...");
                        _tpm.FlushContext(_aikHandle);
                        _aikHandle = null;
                    }

                    if (_ekHandle != null && _tpm != null)
                    {
                        _logger.LogTrace("Disposing EK handle...");
                        try
                        {
                            _tpm.FlushContext(_ekHandle);
                        }
                        catch { }
                        _ekHandle = null;
                    }

                    if (_tpm != null)
                    {
                        _logger.LogTrace("Disposing TPM...");
                    }
                    _tpm?.Dispose();

                    if (_tpmDevice != null)
                    {
                        _logger.LogTrace("Disposing TPM device...");
                    }
                    _tpmDevice?.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
