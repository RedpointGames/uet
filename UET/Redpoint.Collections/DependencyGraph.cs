namespace Redpoint.Collections
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents a dependency graph between objects of type <typeparamref name="K"/>.
    /// </summary>
    /// <typeparam name="K">The type of objects that can depend on one another.</typeparam>
    public class DependencyGraph<K> : IReadOnlyDependencyGraph<K> where K : notnull
    {
        private Dictionary<K, HashSet<K>> _keyDependsOn = new Dictionary<K, HashSet<K>>();
        private Dictionary<K, HashSet<K>> _dependsOnKey = new Dictionary<K, HashSet<K>>();
        private static readonly HashSet<K> _readonlySet = new HashSet<K>();

        /// <summary>
        /// Sets that <paramref name="key"/> depends on <paramref name="dependsOn"/>.
        /// </summary>
        /// <param name="key">The target object.</param>
        /// <param name="dependsOn">The objects it depends on.</param>
        public void SetDependsOn(K key, IEnumerable<K> dependsOn)
        {
            // If we have an old entries, get the things it depends on
            // and tell them we no longer depend on them.
            if (_keyDependsOn.ContainsKey(key))
            {
                foreach (var downstream in _keyDependsOn[key])
                {
                    _dependsOnKey[downstream].Remove(key);
                }
            }

            _keyDependsOn[key] = new HashSet<K>(dependsOn);

            foreach (var downstream in _keyDependsOn[key])
            {
                if (!_dependsOnKey.ContainsKey(downstream))
                {
                    _dependsOnKey[downstream] = new HashSet<K>();
                }
                _dependsOnKey[downstream].Add(key);
            }
        }

        /// <inheritdoc />
        public IReadOnlySet<K> WhatTargetDependsOn(K target)
        {
            if (_keyDependsOn.ContainsKey(target))
            {
                return _keyDependsOn[target];
            }
            return _readonlySet;
        }

        /// <inheritdoc />
        public void WhatTargetDependsOnRecursive(K target, HashSet<K> dependencies)
        {
            if (_keyDependsOn.ContainsKey(target))
            {
                foreach (var downstream in _keyDependsOn[target])
                {
                    if (!dependencies.Contains(downstream))
                    {
                        dependencies.Add(downstream);
                        WhatTargetDependsOnRecursive(downstream, dependencies);
                    }
                }
            }
        }

        /// <inheritdoc />
        public IReadOnlySet<K> WhatDependsOnTarget(K target)
        {
            if (_dependsOnKey.ContainsKey(target))
            {
                return _dependsOnKey[target];
            }
            return _readonlySet;
        }

        /// <inheritdoc />
        public void WhatDependsOnTargetRecursive(K target, HashSet<K> dependents)
        {
            if (_dependsOnKey.ContainsKey(target))
            {
                foreach (var upstream in _dependsOnKey[target])
                {
                    if (!dependents.Contains(upstream))
                    {
                        dependents.Add(upstream);
                        WhatDependsOnTargetRecursive(upstream, dependents);
                    }
                }
            }
        }
    }
}
