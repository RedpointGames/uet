namespace Redpoint.ProcessExecution
{
    using System.Threading.Tasks;

    public interface IProcessExecutorHook
    {
        Task ModifyProcessSpecificationAsync(ProcessSpecification processSpecification, CancellationToken cancellationToken);
    }
}
