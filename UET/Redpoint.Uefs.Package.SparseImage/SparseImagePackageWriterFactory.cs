using System.Runtime.Versioning;

namespace Redpoint.Uefs.Package.SparseImage
{
    [SupportedOSPlatform("macos")]
    internal class SparseImagePackageWriterFactory : IPackageWriterFactory
    {
        public string Format => "sparseimage";

        public IPackageWriter CreatePackageWriter()
        {
            return new SparseImagePackageWriter();
        }
    }
}
