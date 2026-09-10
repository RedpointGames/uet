namespace Redpoint.Uet.Uat
{
    using System.Collections.Generic;

    internal interface IUatStartupHookProvider
    {
        Task<string> GetDotnetStartupHooksPathAsync();
    }
}
