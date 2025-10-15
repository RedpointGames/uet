namespace Redpoint.CloudFramework.DataProtection
{
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
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
    /// You need to create AES key/IV settings to your appsettings.json:
    /// 
    /// {
    ///   "CloudFramework": {
    ///     "Security": {
    ///       "AES": {
    ///         "Key": "",
    ///         "IV": ""
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
        private readonly Aes _aes;
        private readonly byte[] _aesKey;
        private readonly byte[] _aesIV;

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

            _aes = Aes.Create();
            _aes.BlockSize = 128;
            _aes.Mode = CipherMode.CBC;
            _aes.Padding = PaddingMode.PKCS7;

            // If the developer is running their app unconfigured, generate the key and IV and throw an exception with
            // the values to make it easy to set values into appsettings.json
            if (string.IsNullOrEmpty(configuration["CloudFramework:Security:AES:Key"]) ||
                string.IsNullOrEmpty(configuration["CloudFramework:Security:AES:IV"]))
            {
                _aes.GenerateIV();
                _aes.GenerateKey();

                bool needsThrow = true;
                if (hostEnvironment.IsDevelopment())
                {
                    // If we can, automatically fix up appsettings.json for the developer.
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file
                    var filePath = Path.Combine(new FileInfo(Assembly.GetEntryAssembly()!.Location).DirectoryName!, "..", "..", "..", "appsettings.json");
#pragma warning restore IL3000 // Avoid accessing Assembly file path when publishing as a single file
                    if (File.Exists(filePath))
                    {
                        var parentJson = JsonObject.Parse(File.ReadAllText(filePath))!.AsObject();
                        var json = CreatePath(parentJson, "CloudFramework", new JsonObject());
                        json = CreatePath(json, "Security", new JsonObject());
                        json = CreatePath(json, "AES", new JsonObject());
                        CreatePath(json, "Key", JsonValue.Create(Convert.ToBase64String(_aes.Key)));
                        CreatePath(json, "IV", JsonValue.Create(Convert.ToBase64String(_aes.IV)));
                        File.WriteAllText(
                            filePath,
                            JsonSerializer.Serialize(
                                parentJson, 
                                _indentedJsonSerializerOptions));

                        logger.LogInformation("Automatically updated your appsettings.json file with the requires AES key/IV settings.");

                        _aesKey = _aes.Key;
                        _aesIV = _aes.IV;

                        // We've updated appsettings. We don't need to restart because we're already set our settings, and next time the app starts it will be using the settings we just persisted.
                        needsThrow = false;
                    }
                }

                if (needsThrow)
                {
                    var message = "You haven't set the AES key/IV in appsettings.json. Here are newly generated values for you. Key: '" + Convert.ToBase64String(_aes.Key) + "', IV: '" + Convert.ToBase64String(_aes.IV) + "'. Refer to documentation on how to set this up.";
                    logger.LogError(message);
                    throw new InvalidOperationException(message);
                }
            }
            else
            {
                _aesKey = Convert.FromBase64String(configuration["CloudFramework:Security:AES:Key"] ?? string.Empty);
                _aesIV = Convert.FromBase64String(configuration["CloudFramework:Security:AES:IV"] ?? string.Empty);
            }

            if (_aesKey == null || _aesIV == null)
            {
                throw new InvalidOperationException("AES key/IV not loaded; this code path should not be hit.");
            }
        }

        public IDataProtector CreateProtector(string purpose)
        {
            return this;
        }

        public byte[] Protect(byte[] plaintext)
        {
            ArgumentNullException.ThrowIfNull(plaintext);

            // We must have the IV the same every time, or the content can't be decrypted.
#pragma warning disable CA5401
            using var encryptor = _aes.CreateEncryptor(_aesKey, _aesIV);
#pragma warning restore CA5401
            using var result = new MemoryStream();

            using (var stream = new CryptoStream(result, encryptor, CryptoStreamMode.Write, true))
            {
                stream.Write(plaintext, 0, plaintext.Length);
            }

            var l = new byte[result.Position];
            result.Seek(0, SeekOrigin.Begin);
            result.Read(l, 0, l.Length);
            return l;
        }

        public byte[] Unprotect(byte[] protectedData)
        {
            ArgumentNullException.ThrowIfNull(protectedData);

            try
            {
                // We must have the IV the same every time, or the content can't be decrypted.
#pragma warning disable CA5401
                using var decryptor = _aes.CreateDecryptor(_aesKey, _aesIV);
#pragma warning restore CA5401
                using var result = new MemoryStream();

                using (var stream = new CryptoStream(result, decryptor, CryptoStreamMode.Write, true))
                {
                    stream.Write(protectedData, 0, protectedData.Length);
                }

                var l = new byte[result.Position];
                result.Seek(0, SeekOrigin.Begin);
                result.Read(l, 0, l.Length);
                return l;
            }
            catch (CryptographicException)
            {
                return Array.Empty<byte>();
            }
        }
    }
}
