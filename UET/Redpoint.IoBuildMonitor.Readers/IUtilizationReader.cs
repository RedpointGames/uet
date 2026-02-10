namespace Io.Readers
{
    using Io.Json.Frontend;
    using System.Threading.Tasks;

    public interface IUtilizationReader
    {
        Task<UtilizationStats> ReadAsync();
    }
}