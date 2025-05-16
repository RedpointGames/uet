namespace Redpoint.KubernetesManager.Signalling
{
    using System.Threading.Tasks;

    internal class FlagSlim : IDisposable
    {
        private bool _signalled;
        private int _waitCount;
        private IAssociatedData? _associatedData;
        private SemaphoreSlim _semaphoreSlim;
        private SemaphoreSlim _countSemaphoreSlim;

        public FlagSlim()
        {
            _signalled = false;
            _waitCount = 0;
            _semaphoreSlim = new SemaphoreSlim(0);
            _countSemaphoreSlim = new SemaphoreSlim(1);
        }

        public void Dispose()
        {
            ((IDisposable)_semaphoreSlim).Dispose();
            ((IDisposable)_countSemaphoreSlim).Dispose();
        }

        public void Set(IAssociatedData? associatedData)
        {
            if (_signalled)
            {
                throw new FlagAlreadySetException();
            }

            _countSemaphoreSlim.Wait();
            try
            {
                if (_signalled)
                {
                    throw new FlagAlreadySetException();
                }

                _associatedData = associatedData;
                _signalled = true;
                if (_waitCount > 0)
                {
                    _semaphoreSlim.Release(_waitCount);
                    _waitCount = 0;
                }
            }
            finally
            {
                _countSemaphoreSlim.Release();
            }
        }

        public async Task<IAssociatedData?> WaitAsync(CancellationToken cancellationToken)
        {
            if (_signalled)
            {
                return _associatedData;
            }

            Task semaphoreTask;
            await _countSemaphoreSlim.WaitAsync(cancellationToken);
            try
            {
                if (_signalled)
                {
                    return _associatedData;
                }

                _waitCount++;
                semaphoreTask = _semaphoreSlim.WaitAsync(cancellationToken);
            }
            finally
            {
                _countSemaphoreSlim.Release();
            }

            await semaphoreTask;
            return _associatedData;
        }
    }
}
