namespace Redpoint.UET.Automation.Runner
{
    using System.Threading;
    using System.Threading.Tasks;

    internal class Gate
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);
        private bool _unlocked = false;

        public void Unlock()
        {
            _unlocked = true;
            _semaphore.Release();
        }

        public bool Unlocked => _unlocked;

        public async Task WaitAsync(CancellationToken cancellationToken = default)
        {
            if (_unlocked)
            {
                return;
            }

            await _semaphore.WaitAsync(cancellationToken);
            _semaphore.Release();
        }
    }
}
