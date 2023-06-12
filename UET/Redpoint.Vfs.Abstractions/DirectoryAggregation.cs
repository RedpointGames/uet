namespace Redpoint.Vfs.Abstractions
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Provides <see cref="Aggregate(IEnumerable{VfsEntry}?, IEnumerable{VfsEntry}, bool)"/> to aggregate virtual filesystem entries across layers.
    /// </summary>
    public static class DirectoryAggregation
    {
        private static IComparer<string> _comparer = new FileSystemNameComparer();

        /// <summary>
        /// Aggregate the virtual filesystem entries across two virtual filesystem layers.
        /// </summary>
        /// <param name="upstream">Entries from the parent virtual filesystem layer.</param>
        /// <param name="local">Entries from the current virtual filesystem layer.</param>
        /// <param name="enableCorrectnessChecks">If true, this function will validate it's inputs to ensure they are correct for returning to the virtual filesystem driver.</param>
        /// <returns>The aggregated virtual filesystem entries.</returns>
        /// <exception cref="CorrectnessCheckFailureException">Thrown if <paramref name="enableCorrectnessChecks"/> is true and the entries are not sorted correctly.</exception>
        public static IEnumerable<VfsEntry> Aggregate(
            IEnumerable<VfsEntry>? upstream,
            IEnumerable<VfsEntry> local,
            bool enableCorrectnessChecks = false)
        {
            var upstreamEnumerator = upstream?.GetEnumerator();
            var localEnumerator = local.GetEnumerator();

            var hasUpstream = upstreamEnumerator == null ? false : upstreamEnumerator.MoveNext();
            var hasLocal = localEnumerator.MoveNext();

            string? previousUpstreamName = null;
            string? previousLocalName = null;

            while (hasUpstream || hasLocal)
            {
                if (hasUpstream && hasLocal)
                {
                    var upstreamItem = upstreamEnumerator!.Current;
                    var localItem = localEnumerator.Current;

                    if (enableCorrectnessChecks)
                    {
                        if (previousUpstreamName != null && _comparer.Compare(upstreamItem.Name, previousUpstreamName) < 0)
                        {
                            throw new CorrectnessCheckFailureException("Input upstream enumerable is not sorted correctly!");
                        }
                        if (previousLocalName != null && _comparer.Compare(localItem.Name, previousLocalName) < 0)
                        {
                            throw new CorrectnessCheckFailureException("Input local enumerable is not sorted correctly!");
                        }
                    }

                    int comparison = _comparer.Compare(upstreamItem.Name, localItem.Name);
                    if (comparison == 0)
                    {
                        // Both the upstream and local have an entry with the same name.
                        // In this case we take the local item and move *both* enumerators
                        // forward.
                        yield return localItem;
                        previousLocalName = localItem.Name;
                        previousUpstreamName = upstreamItem.Name;
                        hasLocal = localEnumerator.MoveNext();
                        hasUpstream = upstreamEnumerator.MoveNext();
                        continue;
                    }
                    else if (comparison < 0)
                    {
                        yield return upstreamItem;
                        previousUpstreamName = upstreamItem.Name;
                        hasUpstream = upstreamEnumerator.MoveNext();
                        continue;
                    }
                    else
                    {
                        yield return localItem;
                        previousLocalName = localItem.Name;
                        hasLocal = localEnumerator.MoveNext();
                        continue;
                    }
                }
                else if (hasUpstream)
                {
                    var upstreamItem = upstreamEnumerator!.Current;

                    if (enableCorrectnessChecks)
                    {
                        if (previousUpstreamName != null && _comparer.Compare(upstreamItem.Name, previousUpstreamName) < 0)
                        {
                            throw new CorrectnessCheckFailureException("Input upstream enumerable is not sorted correctly!");
                        }
                    }

                    yield return upstreamItem;
                    previousUpstreamName = upstreamItem.Name;
                    hasUpstream = upstreamEnumerator.MoveNext();
                }
                else if (hasLocal)
                {
                    var localItem = localEnumerator.Current;

                    if (enableCorrectnessChecks)
                    {
                        if (previousLocalName != null && _comparer.Compare(localItem.Name, previousLocalName) < 0)
                        {
                            throw new CorrectnessCheckFailureException("Input local enumerable is not sorted correctly!");
                        }
                    }

                    // Prevent scratch database from being emitted.
                    if (localItem.Name != ".uefs.db")
                    {
                        yield return localItem;
                        previousLocalName = localItem.Name;
                    }

                    hasLocal = localEnumerator.MoveNext();
                }
            }
        }
    }
}
