using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitHub.JPMikkers.Dhcp;

internal static class TaskExtensions
{
    // see https://devblogs.microsoft.com/pfxteam/tasks-and-unhandled-exceptions/
    public static Task IgnoreExceptions(this Task task)
    {
        task.ContinueWith(c => { var ignored = c.Exception; },
            TaskContinuationOptions.OnlyOnFaulted |
            TaskContinuationOptions.ExecuteSynchronously);  // | TaskContinuationOptions.DetachedFromParent);
        return task;
    }
}
