namespace Redpoint.Uet.Database
{
    using System.Threading.Tasks;

    public interface IUetDbConnectionFactory
    {
        Task<IUetDbConnection> ConnectToDefaultDatabaseAsync(CancellationToken cancellationToken);

        Task<IUetDbConnection> ConnectToSpecificDatabaseFileAsync(string databasePath, CancellationToken cancellationToken);
    }
}
