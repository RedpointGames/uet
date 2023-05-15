namespace Redpoint.UET.Core
{
    public interface IProjectPathProvider
    {
        string? ProjectRoot { get; }

        string? ProjectName { get; }
    }
}
