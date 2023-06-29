namespace Redpoint.Uet.Configuration
{
    // @todo: Move this somewhere better
    public interface IGlobalArgsProvider
    {
        string GlobalArgsString { get; }

        string[] GlobalArgsArray { get; }
    }
}
