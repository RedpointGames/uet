namespace Redpoint.Uet.Configuration
{
    // @todo: Move this somewhere better
    public interface IGlobalArgsProvider
    {
        string GlobalArgsString { get; }

        IReadOnlyCollection<string> GlobalArgsArray { get; }
    }
}
