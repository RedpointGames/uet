namespace Redpoint.CloudFramework.GoogleInfrastructure
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    public interface IGoogleApiRetry
    {
        void DoRetryableOperation(GoogleApiCallContext callContext, ILogger logger, Action operation);
        T DoRetryableOperation<T>(GoogleApiCallContext callContext, ILogger logger, Func<T> operation);
        Task DoRetryableOperationAsync(GoogleApiCallContext callContext, ILogger logger, Func<Task> operation);
        Task<T> DoRetryableOperationAsync<T>(GoogleApiCallContext callContext, ILogger logger, Func<Task<T>> operation);
    }
}
