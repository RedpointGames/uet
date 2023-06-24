namespace Redpoint.Uefs.Package
{
    public interface IPackageMounterDetector
    {
        IPackageMounter? CreateMounterForPackage(string path);
    }
}
