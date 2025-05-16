namespace Redpoint.Concurrency
{
    using System.Net;
    using System.Threading.Tasks;

    public static class TaskExtensions
    {
        /// <summary>
        /// Runs a task synchronously with the specified cancellation token. The cancellation token can be used to cancel the synchronous task regardless of whether the underlying code accepts a cancellation token.
        /// </summary>
        /// <remarks>
        /// This is used to cancel <see cref="HttpListener.GetContextAsync"/>, which does not accept a cancellation token.
        /// </remarks>
        /// <typeparam name="T">The task return type.</typeparam>
        /// <param name="task">The task to cancel.</param>
        /// <param name="token">The cancellation token that indicates the task should be cancelled.</param>
        /// <returns>The wrapped task.</returns>
        public static Task<T> AsCancellable<T>(this Task<T> task, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(task);

            if (!token.CanBeCanceled)
            {
                return task;
            }

            var tcs = new TaskCompletionSource<T>();
            // This cancels the returned task:
            // 1. If the token has been canceled, it cancels the TCS straightaway
            // 2. Otherwise, it attempts to cancel the TCS whenever
            //    the token indicates cancelled
            token.Register(() => tcs.TrySetCanceled(token),
                useSynchronizationContext: false);

            task.ContinueWith(
                t =>
                {
                    // Complete the TCS per task status
                    // If the TCS has been cancelled, this continuation does nothing
                    if (task.IsCanceled)
                    {
                        tcs.TrySetCanceled();
                    }
                    else if (task.IsFaulted)
                    {
                        tcs.TrySetException(t.Exception!);
                    }
                    else
                    {
                        tcs.TrySetResult(t.Result);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            return tcs.Task;
        }
    }
}
