namespace Redpoint.Uefs.Package
{
    using Microsoft.Extensions.DependencyInjection;

    internal class DefaultPackageMounterDetector : IPackageMounterDetector
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultPackageMounterDetector(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IPackageMounter? CreateMounterForPackage(string path)
        {
            var mounterFactories = _serviceProvider.GetServices<IPackageMounterFactory>();

            IPackageMounter? selectedMounter = null;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    foreach (var mounterFactory in mounterFactories)
                    {
                        var magicHeader = mounterFactory.MagicHeader.ToArray();
                        fs.Seek(0, SeekOrigin.Begin);
                        if (fs.Length >= magicHeader.Length)
                        {
                            byte[] buffer = new byte[magicHeader.Length];
                            fs.Read(buffer);
                            var matches = true;
                            for (int i = 0; i < buffer.Length; i++)
                            {
                                if (buffer[i] != magicHeader[i])
                                {
                                    matches = false;
                                    break;
                                }
                            }
                            if (matches)
                            {
                                selectedMounter = mounterFactory.CreatePackageMounter();
                                break;
                            }
                        }
                    }
                }
            }
            catch (IOException ex) when (ex.Message.Contains("incorrect", StringComparison.Ordinal))
            {
                // Bad filename.
                return null;
            }
            catch (FileNotFoundException)
            {
                // File doesn't exist.
                return null;
            }
            if (selectedMounter == null)
            {
                return null;
            }
            return selectedMounter;
        }
    }
}
