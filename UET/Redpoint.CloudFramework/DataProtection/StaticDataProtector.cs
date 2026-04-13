namespace Redpoint.CloudFramework.DataProtection
{
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NSec.Cryptography;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text.Json;
    using System.Text.Json.Nodes;

    /// <summary>
    /// We don't protect anything sensitive (ASP.NET Core just needs some encryption for the session key
    /// that it stores in cookies), and all of the built-in data protection mechanisms are unreliable.
    /// 
    /// You need to create XChaCha20Poly1305 key settings to your appsettings.json:
    /// 
    /// {
    ///   "CloudFramework": {
    ///     "Security": {
    ///       "XChaCha20Poly1305": {
    ///         "Key": ""
    ///       }
    ///     }
    ///   }
    /// }
    /// 
    /// If you run your application unconfigured, the framework will throw an exception that you can
    /// get newly generated values out of to use in appsettings.json.
    /// </summary>
    public class StaticDataProtector : IDataProtector
    {
        private readonly AeadAlgorithm _algorithm = AeadAlgorithm.XChaCha20Poly1305;
        private readonly Key _aeadKey;

        private static T CreatePath<T>(JsonObject current, string name, T newValue) where T : JsonNode
        {
            if (!current.ContainsKey(name))
            {
                current.Add(name, newValue);
                return newValue;
            }
            return (T)current[name]!;
        }

        private static JsonSerializerOptions _indentedJsonSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
        public StaticDataProtector(IHostEnvironment hostEnvironment, IConfiguration configuration, ILogger<StaticDataProtector> logger)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            // If the developer is running their app unconfigured, generate the key and throw an exception with
            // the values to make it easy to set values into appsettings.json
            if (string.IsNullOrEmpty(configuration["CloudFramework:Security:XChaCha20Poly1305:Key"]))
            {
                bool needsThrow = true;
                if (hostEnvironment.IsDevelopment())
                {
                    // If we can, automatically fix up appsettings.json for the developer.
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file
                    var filePath = Path.Combine(new FileInfo(Assembly.GetEntryAssembly()!.Location).DirectoryName!, "..", "..", "..", "appsettings.json");
#pragma warning restore IL3000 // Avoid accessing Assembly file path when publishing as a single file
                    if (File.Exists(filePath))
                    {
                        _aeadKey = Key.Create(_algorithm, new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

                        var parentJson = JsonObject.Parse(File.ReadAllText(filePath))!.AsObject();
                        var json = CreatePath(parentJson, "CloudFramework", new JsonObject());
                        json = CreatePath(json, "Security", new JsonObject());
                        json = CreatePath(json, "XChaCha20Poly1305", new JsonObject());
                        CreatePath(json, "Key", JsonValue.Create(Convert.ToBase64String(_aeadKey.Export(KeyBlobFormat.NSecSymmetricKey))));
                        File.WriteAllText(
                            filePath,
                            JsonSerializer.Serialize(
                                parentJson,
                                _indentedJsonSerializerOptions));

                        logger.LogInformation("Automatically updated your appsettings.json file with the requires XChaCha20Poly1305 key settings.");

                        // We've updated appsettings. We don't need to restart because we're already set our settings, and next time the app starts it will be using the settings we just persisted.
                        needsThrow = false;
                    }
                }

                if (needsThrow)
                {
                    var message = "You haven't set the XChaCha20Poly1305 key in appsettings.json. Here are newly generated values for you. Key: '" + Convert.ToBase64String(Key.Create(_algorithm).Export(KeyBlobFormat.NSecSymmetricKey)) + "'. Refer to documentation on how to set this up.";
                    logger.LogError(message);
                    throw new InvalidOperationException(message);
                }
            }
            else
            {
                _aeadKey = Key.Import(_algorithm, Convert.FromBase64String(configuration["CloudFramework:Security:XChaCha20Poly1305:Key"] ?? string.Empty), KeyBlobFormat.NSecSymmetricKey);
            }

            if (_aeadKey == null)
            {
                throw new InvalidOperationException("XChaCha20Poly1305 key not loaded; this code path should not be hit.");
            }
        }

        public IDataProtector CreateProtector(string purpose)
        {
            return this;
        }

        public byte[] Protect(byte[] plaintext)
        {
            ArgumentNullException.ThrowIfNull(plaintext);

            var nonce = RandomNumberGenerator.GetBytes(_algorithm.NonceSize);
            var encrypted = _algorithm.Encrypt(
                _aeadKey,
                nonce,
                [],
                plaintext);

            byte[] result = [.. nonce, .. encrypted];
            return result;
        }

        public byte[] Unprotect(byte[] protectedData)
        {
            ArgumentNullException.ThrowIfNull(protectedData);

            if (protectedData.Length < _algorithm.NonceSize)
            {
                throw new ArgumentException("Protected data must include nonce.", nameof(protectedData));
            }

            var nonce = protectedData.AsSpan(0, _algorithm.NonceSize);
            var encrypted = protectedData.AsSpan(_algorithm.NonceSize);

            var decrypted = _algorithm.Decrypt(
                _aeadKey,
                nonce,
                [],
                encrypted);
            if (decrypted == null)
            {
                throw new InvalidOperationException("Failed to decrypt data.");
            }
            return decrypted;
        }
    }
}
