namespace Redpoint.ProgressMonitor
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Additional helpers when you want to perform the (common) task of writing
    /// progress information to the system console.
    /// </summary>
    public static class SystemConsole
    {
        private sealed class SystemConsoleInformation : IConsoleInformation
        {
            public int? Width => ConsoleWidth;
        }

        private static Lazy<int?> _consoleWidth = new Lazy<int?>(() =>
        {
            try
            {
                return Console.BufferWidth;
            }
            catch
            {
                // Not connected to a console, e.g. output is redirected.
                return null;
            }
        });

        private static Lazy<IConsoleInformation> _consoleInformation = new Lazy<IConsoleInformation>(() => new SystemConsoleInformation());

        private static Lazy<ProgressEmit> _writeProgressToConsole = new Lazy<ProgressEmit>(() => (string message, long count) =>
        {
            if (ConsoleWidth.HasValue)
            {
                // Emit the progress information in such a
                // way that we overwrite the previous info
                // reported to the console.
                Console.Write($"\r{message}".PadRight(ConsoleWidth.Value));
            }
            else
            {
                // Emit onto a new line every 5 seconds. This
                // callback is invoked every 100ms.
                if (count % 50 == 0)
                {
                    Console.WriteLine(message);
                }
            }
        });

        /// <summary>
        /// Provides the console width, or <c>null</c> if the process output is redirected.
        /// </summary>
        public static int? ConsoleWidth => _consoleWidth.Value;

        /// <summary>
        /// Provides the <see cref="IConsoleInformation"/> instance for the system console, or <c>null</c> if the process output is redirected.
        /// </summary>
        public static IConsoleInformation? ConsoleInformation => ConsoleWidth.HasValue ? _consoleInformation.Value : null;

        /// <summary>
        /// Provides a helper function which can be passed into <see cref="IMonitor{T}.MonitorAsync(T, IConsoleInformation?, ProgressEmit, System.Threading.CancellationToken)"/> as the progress emit delegate if you want to perform the simple task of emitting progress information to the console. If the console is not redirected, the progress is updated in-place; if the console is redirected, progress is written to a new line every 5 seconds.
        /// </summary>
        public static ProgressEmit WriteProgressToConsole => _writeProgressToConsole.Value;

        /// <summary>
        /// Cancel a monitoring task and wait for it to finish. If console output is not redirected, emit a final new line to ensure
        /// further output after the progress monitor is rendered correctly.
        /// </summary>
        /// <param name="task">The monitor task.</param>
        /// <param name="cts">The cancellation token source to cancel.</param>
        /// <returns>The awaitable task.</returns>
        public static async Task CancelAndWaitForConsoleMonitoringTaskAsync(Task task, CancellationTokenSource cts)
        {
#if NETFRAMEWORK
            if (task == null) throw new ArgumentNullException(nameof(task));
            if (cts == null) throw new ArgumentNullException(nameof(cts));
#else
            ArgumentNullException.ThrowIfNull(task);
            ArgumentNullException.ThrowIfNull(cts);
#endif

            // Stop monitoring.
            cts.Cancel();
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }

            // Emit a newline after our progress message.
            if (ConsoleWidth.HasValue)
            {
                Console.WriteLine();
            }
        }
    }
}
