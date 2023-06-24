namespace Redpoint.Uefs.Package
{
    public interface IPackageMounterFactory
    {
        Memory<byte> MagicHeader { get; }

        IPackageMounter CreatePackageMounter();
    }
}
