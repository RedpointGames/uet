namespace BuildRunner.Services
{
    internal interface IProjectPathProvider
    {
        string? ProjectRoot { get; }

        string? ProjectName { get; }
    }
}
