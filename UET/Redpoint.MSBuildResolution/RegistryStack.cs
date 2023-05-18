namespace Redpoint.MSBuildResolution
{
    using Microsoft.Win32;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows")]
    internal class RegistryStack : IDisposable
    {
        private readonly string _path;
        private readonly RegistryKey[] _openedKeys;
        private readonly bool _exists;

        private RegistryStack(string path, RegistryKey[] openedKeys, bool exists)
        {
            _path = path;
            _openedKeys = openedKeys;
            _exists = exists;
        }

        public static RegistryStack OpenPath(string path)
        {
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
                throw new NotSupportedException();
            }
            openedKeys.Add(currentKey);
            var exists = true;
            for (int i = 1; i < components.Length; i++)
            {
                currentKey = currentKey!.OpenSubKey(components[i]);
                if (currentKey == null)
                {
                    exists = false;
                    break;
                }
                openedKeys.Add(currentKey);
            }
            return new RegistryStack(path, openedKeys.ToArray(), exists);
        }

        public RegistryKey Key
        {
            get
            {
                if (_exists)
                {
                    return _openedKeys[_openedKeys.Length - 1];
                }
                throw new InvalidOperationException();
            }
        }

        public bool Exists => _exists;

        public void Dispose()
        {
            for (int i = _openedKeys.Length - 1; i >= 0; i--)
            {
                _openedKeys[i].Dispose();
            }
        }
    }
}