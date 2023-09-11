namespace Redpoint.Uet.Configuration
{
    // @todo: Move this somewhere better
    public interface IGlobalArgsProvider
    {
        string GlobalArgsString { get; }

        IReadOnlyList<string> GlobalArgsArray { get; }
    }
}
