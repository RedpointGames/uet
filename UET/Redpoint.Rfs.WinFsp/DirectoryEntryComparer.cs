namespace Redpoint.Rfs.WinFsp
{
    using System;
    using System.Collections;

    internal sealed class DirectoryEntryComparer : IComparer
    {
        public int Compare(object? x, object? y)
        {
            return String.Compare(
                (String)((DictionaryEntry)x!).Key,
                (String)((DictionaryEntry)y!).Key, StringComparison.Ordinal);
        }
    }
}
