namespace Redpoint.OpenGE.Executor
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
#if HAS_WMI
    using WmiLight;
#endif

    internal class ProcessWideCoreReservation : ICoreReservation
    {
        private HashSet<int> _reservedCores = new HashSet<int>();
        private SemaphoreSlim _reservedCoreLock = new SemaphoreSlim(1);

        public async Task<int> AllocateCoreAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // @todo: Make this check memory availability as well.
                int? nextCore = null;
                await _reservedCoreLock.WaitAsync();
                try
                {
                    /*
                     * @note: Neither System.Management nor WmiLight work when trimming is enabled,
                     * because they both rely on COM. That means at the moment we can't look at the
                     * current processor usage on Windows and only start tasks when the processors
                     * are free.
                     */
#if HAS_WMI
                    if (OperatingSystem.IsWindows())
                    {
                        nextCore = GetNextSuitableCoreForAssignment(_reservedCores);
                        if (nextCore != null)
                        {
                            _reservedCores.Add(nextCore.Value);
                        }
                    }
                    else
                    {
#endif
                    for (int i = 0; i < Environment.ProcessorCount; i++)
                    {
                        if (!_reservedCores.Contains(i))
                        {
                            nextCore = i;
                            _reservedCores.Add(nextCore.Value);
                            break;
                        }
                    }
#if HAS_WMI
                    }
#endif
                }
                finally
                {
                    _reservedCoreLock.Release();
                }

                if (nextCore == null)
                {
                    // We don't have an available core to schedule on because the system
                    // is too busy. Wait a little bit and then try again.
                    await Task.Delay(1000, cancellationToken);
                }
                else
                {
                    // We reserved this core.
                    return nextCore.Value;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException();
        }

        public async Task ReleaseCoreAsync(int core, CancellationToken cancellationToken)
        {
            await _reservedCoreLock.WaitAsync();
            try
            {
                _reservedCores.Remove(core);
            }
            finally
            {
                _reservedCoreLock.Release();
            }
        }

        /*
         * @note: Neither System.Management nor WmiLight work when trimming is enabled,
         * because they both rely on COM. That means at the moment we can't look at the
         * current processor usage on Windows and only start tasks when the processors
         * are free.
         */
#if HAS_WMI
        [SupportedOSPlatform("windows")]
        internal static int? GetNextSuitableCoreForAssignment(HashSet<int> reservedCores)
        {
            using (var conn = new WmiConnection())
            {
                var cores = conn.CreateQuery("select * from Win32_PerfFormattedData_PerfOS_Processor")
                    .Where(mo => (string)mo["Name"] != "_Total")
                    .Select(mo => new
                    {
                        Name = int.Parse((string)mo["Name"]),
                        Usage = (ulong)mo["PercentProcessorTime"]
                    })
                    .ToList();
                return cores
                    .Where(x => x.Usage <= 15)
                    .Where(x => !reservedCores.Contains(x.Name))
                    .FirstOrDefault()?.Name;
            }
        }
#endif
    }
}
