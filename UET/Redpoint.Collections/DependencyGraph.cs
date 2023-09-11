namespace Redpoint.Collections
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents a dependency graph between objects of type <typeparamref name="TValue"/>.
    /// </summary>
    /// <typeparam name="TValue">The type of objects that can depend on one another.</typeparam>
    public class DependencyGraph<TValue> : IReadOnlyDependencyGraph<TValue> where TValue : notnull
    {
        private Dictionary<TValue, HashSet<TValue>> _keyDependsOn = new Dictionary<TValue, HashSet<TValue>>();
        private Dictionary<TValue, HashSet<TValue>> _dependsOnKey = new Dictionary<TValue, HashSet<TValue>>();
        private static readonly HashSet<TValue> _readonlySet = new HashSet<TValue>();

        /// <summary>
        /// Sets that <paramref name="key"/> depends on <paramref name="dependsOn"/>.
        /// </summary>
        /// <param name="key">The target object.</param>
        /// <param name="dependsOn">The objects it depends on.</param>
        public void SetDependsOn(TValue key, IEnumerable<TValue> dependsOn)
        {
            // If we have an old entries, get the things it depends on
            // and tell them we no longer depend on them.
            if (_keyDependsOn.TryGetValue(key, out var iter))
            {
                foreach (var downstream in iter)
                {
                    _dependsOnKey[downstream].Remove(key);
                }
            }

            _keyDependsOn[key] = new HashSet<TValue>(dependsOn);

            foreach (var downstream in _keyDependsOn[key])
            {
                if (!_dependsOnKey.ContainsKey(downstream))
                {
                    _dependsOnKey[downstream] = new HashSet<TValue>();
                }
                _dependsOnKey[downstream].Add(key);
            }
        }

        /// <inheritdoc />
        public IReadOnlySet<TValue> WhatTargetDependsOn(TValue target)
        {
            if (_keyDependsOn.TryGetValue(target, out var value))
            {
                return value;
            }
            return _readonlySet;
        }

        /// <inheritdoc />
        public void WhatTargetDependsOnRecursive(TValue target, HashSet<TValue> dependencies)
        {
            if (dependencies == null) throw new ArgumentNullException(nameof(dependencies));

            if (_keyDependsOn.TryGetValue(target, out var iter))
            {
                foreach (var downstream in iter)
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
        public IReadOnlySet<TValue> WhatDependsOnTarget(TValue target)
        {
            if (_dependsOnKey.TryGetValue(target, out var value))
            {
                return value;
            }
            return _readonlySet;
        }

        /// <inheritdoc />
        public void WhatDependsOnTargetRecursive(TValue target, HashSet<TValue> dependents)
        {
            if (dependents == null) throw new ArgumentNullException(nameof(dependents));

            if (_dependsOnKey.TryGetValue(target, out var iter))
            {
                foreach (var upstream in iter)
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
