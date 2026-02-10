namespace Io.Readers
{
    using Io.Json.Frontend;
    using System.Threading.Tasks;

    public interface IHistoryReader
    {
        Task<HistoryStats> ReadAsync();
    }
}