namespace Redpoint.Tpm.Internal
{
    using Tpm2Lib;

    internal class DefaultTpmOperationHandles : ITpmOperationHandles
    {
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

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _tpm?.Dispose();
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
