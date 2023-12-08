namespace Redpoint.Uefs.Package
{
    internal sealed class DefaultPackageManifestAssembler : IPackageManifestAssembler
    {
        public PackageManifest CreateManifestFromSourceDirectory(IPackageWriter packageWriter, string path)
        {
            long fileCount = 0;
            long indexSize = packageWriter.IndexHeaderSize;
            long dataSize = 0;
            long lastConsoleProgressEmitSize = 0;

            var manifest = new PackageManifest();

            FindFiles(packageWriter, manifest, path, string.Empty, ref fileCount, ref indexSize, ref dataSize, ref lastConsoleProgressEmitSize);

            manifest.FileCount = fileCount;
            manifest.IndexSizeBytes = indexSize;
            manifest.DataSizeBytes = dataSize;

            return manifest;
        }

        private static void FindFiles(IPackageWriter packageWriter, PackageManifest packageManifest, string directory, string rel_directory, ref long file_count, ref long index_size, ref long data_size, ref long last_console_progress_emit_size)
        {
            string tmp = directory + Path.DirectorySeparatorChar;
            string rel = rel_directory + Path.DirectorySeparatorChar;
            if (rel == Path.DirectorySeparatorChar.ToString())
            {
                rel = "";
            }

            var dirInfo = new DirectoryInfo(tmp);
            foreach (var e in dirInfo.GetDirectories()
                .Select(x => (entry: (FileSystemInfo)x, isDir: true))
                .Concat(dirInfo.GetFiles()
                    .Select(x => (entry: (FileSystemInfo)x, isDir: false))))
            {
                if (e.entry.Name == "." || e.entry.Name == ".." || e.entry.Name == ".stfolder" || e.entry.Name == ".egstore" ||
                    e.entry.Name.StartsWith('$') || e.entry.Name == "System Volume Information")
                {
                    continue;
                }

                string rel_entry = rel + e.entry.Name;
                string loc_entry = directory + Path.DirectorySeparatorChar + e.entry.Name;

                index_size += packageWriter.ComputeEntryIndexSize(rel_entry);

                if (e.isDir)
                {
                    packageManifest.AddDirectoryToManifest(rel_entry);

                    FindFiles(packageWriter, packageManifest, loc_entry, rel_entry, ref file_count, ref index_size, ref data_size, ref last_console_progress_emit_size);
                }
                else
                {
                    packageManifest.AddFileToManifest(rel_entry, loc_entry, data_size, ((FileInfo)e.entry).Length);

                    data_size += packageWriter.ComputeEntryDataSize(rel_entry, ((FileInfo)e.entry).Length);
                    file_count += 1;
                }

                if (last_console_progress_emit_size != packageManifest.Count && packageManifest.Count % 10000 == 0)
                {
                    Console.WriteLine($"search progress: {packageManifest.Count} entries found so far");
                    last_console_progress_emit_size = packageManifest.Count;
                }
            }
        }
    }
}
