namespace Redpoint.Uefs.Package
{
    public interface IPackageWriterFactory
    {
        string Format { get; }

        IPackageWriter CreatePackageWriter();
    }
}
