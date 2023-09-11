namespace Redpoint.Uefs.Package.Vhd
{
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows6.2")]
    internal sealed class VhdPackageWriterFactory : IPackageWriterFactory
    {
        public string Format => "vhd";

        public IPackageWriter CreatePackageWriter()
        {
            return new VhdPackageWriter();
        }
    }
}
