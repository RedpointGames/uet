namespace Redpoint.Collections
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents a read-only dependency graph between objects of type <typeparamref name="K"/>.
    /// </summary>
    /// <typeparam name="K">The type of objects that can depend on one another.</typeparam>
    public interface IReadOnlyDependencyGraph<K> where K : notnull
    {
        /// <summary>
        /// Returns what the target object immediately depends on.
        /// </summary>
        /// <param name="target">The target object.</param>
        /// <returns>The immediate dependencies.</returns>
        IReadOnlySet<K> WhatTargetDependsOn(K target);

        /// <summary>
        /// Returns what the target object recursively depends on.
        /// </summary>
        /// <param name="target">The target object.</param>
        /// <param name="dependencies">A hashset to be filled with all of the dependencies recursively.</param>
        void WhatTargetDependsOnRecursive(K target, HashSet<K> dependencies);

        /// <summary>
        /// Returns what depends on the target object.
        /// </summary>
        /// <param name="target">The target object.</param>
        /// <returns>The objects that have dependencies on the target.</returns>
        IReadOnlySet<K> WhatDependsOnTarget(K target);

        /// <summary>
        /// Returns what directly or indirectly depends on the target object.
        /// </summary>
        /// <param name="target">The target object.</param>
        /// <param name="dependents">A hashset to be filled with all of the objects that directly or indirectly depend on the target object.</param>
        void WhatDependsOnTargetRecursive(K target, HashSet<K> dependents);
    }
}
