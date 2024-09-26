namespace Redpoint.Uba.Native
{
    using System.Reflection;
    using System.Runtime.InteropServices;

    /// <summary>
    /// The static class that can be used to initialize UBA. You must call <see cref="Init(string)"/> before using any other library functions.
    /// </summary>
    public static class UbaNative
    {
        private static string? _ubaPath = null;
        private static string? _ubaHostFileName = null;

        /// <summary>
        /// Returns the path to the directory that contains the UbaHost library and supporting executable files.
        /// </summary>
        /// <exception cref="UbaNotInitializedException">Thrown if <see cref="Init(string)"/> has not yet been called.</exception>
        public static string UbaPath
        {
            get
            {
                ThrowIfNotInitialized();
                return _ubaPath!;
            }
        }

        /// <summary>
        /// Returns the path to the UbaHost library itself.
        /// </summary>
        /// <exception cref="UbaNotInitializedException">Thrown if <see cref="Init(string)"/> has not yet been called.</exception>
        public static string UbaHostFilePath
        {
            get
            {
                ThrowIfNotInitialized();
                return Path.Combine(_ubaPath!, _ubaHostFileName!);
            }
        }

        /// <summary>
        /// Initialize UBA by specifying the path to the directory that contains 'UbaHost.dll', 'libUbaHost.dylib' or 'libUbaHost.so' file, depending on the platform. On Windows, you can also pass the path to the directory that contains the 'x64' and 'arm64' architecture directories (which themselves contain 'UbaHost.dll'), and this function will pick the correct architecture subdirectory based on the architecture of the current machine.
        /// 
        /// This function must be called prior to any other UBA functions being used.
        /// </summary>
        /// <param name="ubaPath">The fully qualified, absolute path to the directory that contains 'UbaHost.dll', 'libUbaHost.dylib' or 'libUbaHost.so' file, depending on the platform. On Windows, you can also pass the path to the directory that contains the 'x64' and 'arm64' architecture directories.</param>
        /// <exception cref="UbaAlreadyInitializedException">Thrown if <see cref="Init(string)"/> has already been called.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="ubaPath"/> is not a fully qualified path. You can not pass relative paths to this function.</exception>
        /// <exception cref="PlatformNotSupportedException">Thrown if UBA does not support the current platform.</exception>
        /// <exception cref="UbaHostLibraryNotFoundException">Thrown if the directory specified <paramref name="ubaPath"/> does not contain the expected UbaHost library file.</exception>
        public static void Init(string ubaPath)
        {
            if (!Path.IsPathFullyQualified(ubaPath))
            {
                throw new ArgumentException("The path must be fully qualified.", nameof(ubaPath));
            }

            string ubaHostFileName;
            if (OperatingSystem.IsWindows())
            {
                ubaHostFileName = "UbaHost.dll";

                // If the caller didn't pass the path to the architecture-specific directory, add the architecture to the path automatically.
                if (!File.Exists(Path.Combine(ubaPath, ubaHostFileName)))
                {
                    if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                    {
                        ubaPath = Path.Combine(ubaPath, "x64");
                    }
                    else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                    {
                        ubaPath = Path.Combine(ubaPath, "arm64");
                    }
                    else
                    {
                        throw new PlatformNotSupportedException("UBA does not support this platform's architecture.");
                    }
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                ubaHostFileName = "libUbaHost.dylib";
            }
            else if (OperatingSystem.IsLinux())
            {
                ubaHostFileName = "libUbaHost.so";
            }
            else
            {
                throw new PlatformNotSupportedException("UBA does not support this platform.");
            }

            var ubaHostPath = Path.Combine(ubaPath, ubaHostFileName);
            if (!File.Exists(ubaHostPath))
            {
                throw new UbaHostLibraryNotFoundException(ubaPath, ubaHostFileName);
            }

            // @note: We wait until here to check _ubaPath so that our comparison is correct
            // when using architecture subdirectories on Windows.
            if (_ubaPath != null)
            {
                if (_ubaPath == ubaPath)
                {
                    // Same path; the call is safe because we aren't trying to change to a different UBA library.
                    return;
                }
                else
                {
                    // Different path; unsafe because the UBA host library has potentially already been loaded.
                    throw new UbaAlreadyInitializedException(_ubaPath);
                }
            }

            _ubaPath = ubaPath;
            _ubaHostFileName = ubaHostFileName;

            NativeLibrary.SetDllImportResolver(typeof(UbaNative).Assembly, ImportResolver);
        }

        /// <summary>
        /// Resolves the UbaHost library at runtime for functions marked with the <see cref="LibraryImportAttribute"/> attribute.
        /// </summary>
        /// <param name="libraryName">The name of the library to load.</param>
        /// <param name="assembly">The assembly that is performing the load.</param>
        /// <param name="searchPath">The search path rules, if specified.</param>
        /// <returns>The handle to the native library if loaded, otherwise <see cref="nint.Zero"/>.</returns>
        /// <exception cref="UbaNotInitializedException">Thrown if <see cref="Init(string)"/> has not yet been called.</exception>
        private static nint ImportResolver(
            string libraryName,
            Assembly assembly,
            DllImportSearchPath? searchPath)
        {
            if (_ubaPath == null)
            {
                throw new UbaNotInitializedException();
            }

            if (libraryName == "UbaHost")
            {
                return NativeLibrary.Load(Path.Combine(_ubaPath, _ubaHostFileName!), assembly, null);
            }

            return nint.Zero;
        }

        /// <summary>
        /// Throws the <see cref="UbaNotInitializedException"/> exception if <see cref="Init(string)"/> has not yet been called.
        /// </summary>
        /// <exception cref="UbaNotInitializedException">Thrown if <see cref="Init(string)"/> has not yet been called.</exception>
        internal static void ThrowIfNotInitialized()
        {
            if (_ubaPath == null)
            {
                throw new UbaNotInitializedException();
            }
        }
    }
}
