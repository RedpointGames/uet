namespace Redpoint.Registry
{
    using Microsoft.Win32;
    using System.Runtime.Versioning;

    /// <summary>
    /// This class allows you to access a registry key using the PowerShell style convention of <code>HKXX:\Path\To\Key</code>, which significantly reduces the boilerplate required to open nested registry keys.
    /// </summary>
    [SupportedOSPlatform("windows")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "This class behaves like a stack.")]
    public sealed class RegistryStack : IDisposable
    {
        private readonly RegistryKey[] _openedKeys;
        private readonly bool _exists;

        private RegistryStack(RegistryKey[] openedKeys, bool exists)
        {
            _openedKeys = openedKeys;
            _exists = exists;
        }

        /// <summary>
        /// Opens the specified registry path and returns a <see cref="RegistryStack"/> that represents the opened key. You must dispose the returned registry stack when you are finished with it.
        /// 
        /// If <code>create</code> is true, creates the necessary registry keys on the path if they do not exist.
        /// 
        /// This function always succeeds if you provide it a well formed path. If <code>create</code> is false and the registry key doesn't not exist at the path, the <see cref="Exists"/> property will be false on the returned object.
        /// </summary>
        /// <param name="path">A registry path like <code>HKCU:\SOFTWARE\Microsoft\VisualStudio\SxS\VS7</code>.</param>
        /// <param name="writable">If true, the target registry key is opened in writable mode.</param>
        /// <param name="create">If true, registry keys will be created as neede to open this registry path.</param>
        /// <returns>A new <see cref="RegistryStack"/> object which represents the registry path.</returns>
        /// <exception cref="RegistryPathNotWellFormedException">Thrown if the registry path does not start with "HKCU:" or "HKLM:".</exception>
        public static RegistryStack OpenPath(string path, bool writable = false, bool create = false)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            var openedKeys = new List<RegistryKey>();
            RegistryKey? currentKey;
            var components = path.Split("\\");
            if (components[0] == "HKCU:")
            {
                currentKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            }
            else if (components[0] == "HKLM:")
            {
                currentKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            }
            else
            {
                throw new RegistryPathNotWellFormedException();
            }
            openedKeys.Add(currentKey);
            var exists = true;
            for (int i = 1; i < components.Length; i++)
            {
                currentKey = currentKey!.OpenSubKey(components[i], i == components.Length - 1 && (writable || create));
                if (currentKey == null)
                {
                    if (create)
                    {
                        if (i >= 2)
                        {
                            // We need to re-open the parent in writable mode so we can
                            // create the subkey.
                            var parentKey = openedKeys.Last();
                            parentKey.Dispose();
                            openedKeys.Remove(parentKey);
                            var parentParentKey = openedKeys.Last();
                            currentKey = parentParentKey.OpenSubKey(components[i - 1], true)!;
                            openedKeys.Add(currentKey);
                        }
                        else
                        {
                            // The latest key is the RegistryHive itself, which does not
                            // have a writable parameter, but we do need to set currentKey
                            // back so we can call CreateSubKey properly.
                            currentKey = openedKeys.Last();
                        }
                        currentKey = currentKey!.CreateSubKey(components[i], i == components.Length - 1 && (writable || create));
                    }
                    else
                    {
                        exists = false;
                        break;
                    }
                }
                openedKeys.Add(currentKey);
            }
            return new RegistryStack(openedKeys.ToArray(), exists);
        }

        /// <summary>
        /// The registry key opened at the specified path. You must use <see cref="Exists"/> to check if the key exists before using this property.
        /// </summary>
        /// <exception cref="RegistryKeyNotFoundException">Thrown if the registry key did not exist when the registry stack was opened.</exception>
        public RegistryKey Key
        {
            get
            {
                if (_exists)
                {
                    return _openedKeys[_openedKeys.Length - 1];
                }
#pragma warning disable CA1065 // Do not raise exceptions in unexpected locations
                throw new RegistryKeyNotFoundException();
#pragma warning restore CA1065 // Do not raise exceptions in unexpected locations
            }
        }

        /// <summary>
        /// Returns true if the registry key exists.
        /// </summary>
        public bool Exists => _exists;

        /// <summary>
        /// Disposes the registry stack, which disposes all of the underlying <see cref="RegistryKey"/> objects.
        /// </summary>
        public void Dispose()
        {
            for (int i = _openedKeys.Length - 1; i >= 0; i--)
            {
                _openedKeys[i].Dispose();
            }
        }
    }
}