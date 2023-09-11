namespace Redpoint.Uefs.Package.SparseImage
{
    using System.Runtime.Versioning;

    [SupportedOSPlatform("macos")]
    internal sealed class SparseImagePackageWriterFactory : IPackageWriterFactory
    {
        public string Format => "sparseimage";

        public IPackageWriter CreatePackageWriter()
        {
            return new SparseImagePackageWriter();
        }
    }
}
