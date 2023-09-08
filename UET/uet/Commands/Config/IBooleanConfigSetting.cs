namespace UET.Commands.Config
{
    using System.Threading.Tasks;

    internal interface IBooleanConfigSetting
    {
        string Name { get; }

        string Description { get; }

        Task<bool> GetValueAsync(CancellationToken cancellationToken);

        Task SetValueAsync(bool value, CancellationToken cancellationToken);
    }
}
