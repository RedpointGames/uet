namespace Redpoint.OpenGE.Component.PreprocessorCache.Filesystem
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class CachingFilesystemProvider
    {
        //
        // Directories on Windows have their modification times changed whenever
        // a file within them is created, renamed or deleted. It only happens 
        // to the immediate directory (not parents) and does not detect if files
        // themselves have changed, but since all we're trying to build is an
        // existence cache, that's good enough.
        //
        // We're also optimizing for scenarios where a file does not exist, because
        // there will be an order of magnitude more of these than files that do. The
        // cache can indicate a file exists when it's since been deleted, so any
        // time the cache would return true, it runs File.Exists before returning
        // to be sure.
        //
        // We need a ZoneTree cache that can store the following information:
        //
        // file path as the key:
        //   whether it exists as a boolean
        //   the deepest directory that actually exists in the file path
        //   the modification time of the deepest directory when this entry was cached
        //
        // directory path as the key:
        //   the last modification time of that directory
        //   the last time the modification time was checked
        //   a list of files and subdirectories when the directory was last checked
        //
        // Requests to the cache need to have a "since" value, which should be the
        // time that the overall resolution request started. Any file changes that
        // happen between the resolution process starting and returning are not
        // observed, since the preprocessor assumes the filesystem is unchanging
        // for the duration of it's run.
        //
        // When a request to see if a file exists comes in, we look up the file
        // path in the cache and then:
        //
        // -- If the file path is not cached:
        //    1. Recursively scan down the directory path, updating the
        //       directory tree cache until we reach a leaf node or the file
        //       itself.
        //    2. Store the cache result for the file path.
        //
        // -- If the file path is cached as "not exists":
        //    1. Get the deepest directory value.
        //    2. Lookup the directory cache for the deepest directory value.
        //    3. If the modification time hasn't been checked since "since",
        //       query the modification time on disk.
        //    4. If the modification time on disk is the same as the cached
        //       version, return "not exists".
        //    5. Otherwise, update the files/subdirectories cache.
        //    6. Start recursing down the directory tree if intermediate nodes
        //       have since appeared until we reach a leaf node or the file
        //       itself.
        //    7. Update the cached entry for the file path as appropriate.
        //
        // -- If the file path is cached as "exists":
        //    1. Run File.Exists to check that it still exists.
        //    2. If it exists, return "exists".
        //    3. Otherwise, reverse recurse up the directory
        //       tree to invalidate directory caches (since directories
        //       themselves might have been deleted).
        //    4. Stop at the new deepest directory based on that upward
        //       recursion, update the file cache entry to point at it
        //       and return "not exists".
        //

    }
}
