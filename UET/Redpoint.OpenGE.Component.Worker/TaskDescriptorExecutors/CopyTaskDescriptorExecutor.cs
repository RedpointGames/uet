namespace Redpoint.OpenGE.Component.Worker.TaskDescriptorExecutors
{
    using Redpoint.OpenGE.Protocol;
    using System.Collections.Generic;

    internal class CopyTaskDescriptorExecutor : ITaskDescriptorExecutor<CopyTaskDescriptor>
    {
        public IAsyncEnumerable<ProcessResponse> ExecuteAsync(
            CopyTaskDescriptor descriptor,
            CancellationToken cancellationToken)
        {
            return Execute(descriptor, cancellationToken).ToAsyncEnumerable();
        }

        private IEnumerable<ProcessResponse> Execute(
            CopyTaskDescriptor descriptor,
            CancellationToken cancellationToken)
        {
            yield return new ProcessResponse
            {
                StandardOutputLine = $"Copying '{descriptor.FromAbsolutePath}' to '{descriptor.ToAbsolutePath}' via OpenGE...",
            };
            Exception? error = null;
            try
            {
                File.Copy(descriptor.FromAbsolutePath, descriptor.ToAbsolutePath, true);
            }
            catch (Exception ex)
            {
                error = ex;
            }
            if (error == null)
            {
                yield return new ProcessResponse
                {
                    ExitCode = 0,
                };
            }
            else
            {
                yield return new ProcessResponse
                {
                    StandardErrorLine = $"Failed to copy '{descriptor.FromAbsolutePath}' to '{descriptor.ToAbsolutePath}' via OpenGE: {error.Message}",
                };
                yield return new ProcessResponse
                {
                    ExitCode = 1,
                };
            }
        }
    }
}
