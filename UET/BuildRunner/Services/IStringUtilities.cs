namespace BuildRunner.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    internal interface IStringUtilities
    {
        string GetStabilityHash(string inputString, int? length);
    }
}
