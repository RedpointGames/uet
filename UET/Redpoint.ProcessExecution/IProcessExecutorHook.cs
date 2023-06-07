namespace Redpoint.ProcessExecution
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public interface IProcessExecutorHook
    {
        Task ModifyProcessSpecificationAsync(ProcessSpecification processSpecification, CancellationToken cancellationToken);
    }
}
