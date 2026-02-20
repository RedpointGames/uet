namespace Redpoint.Uefs.Commands.Hash
{
    using System.Threading.Tasks;

    public interface IFileHasher
    {
        Task<string> ComputeHashAsync(FileInfo package);
    }
}
