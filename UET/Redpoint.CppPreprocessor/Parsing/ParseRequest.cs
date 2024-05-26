namespace Redpoint.CppPreprocessor.Parsing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A request to parse out the include dependencies for a C++ file.
    /// </summary>
    public class ParseRequest
    {
        /// <summary>
        /// The input file for the compiler.
        /// </summary>
        [JsonPropertyName("inputPath")]
        public string InputPath { get; set; } = string.Empty;

        /// <summary>
        /// The additional headers to forcibly include.
        /// </summary>
        [JsonPropertyName("forceIncludePaths")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Used for JSON serialization.")]
        public string[] ForceIncludePaths { get; set; } = [];

        /// <summary>
        /// The include directories to search for include files under.
        /// </summary>
        [JsonPropertyName("includeDirectoryPaths")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Used for JSON serialization.")]
        public string[] IncludeDirectoryPaths { get; set; } = Array.Empty<string>();

        /// <summary>
        /// The global definitions for the preprocessor.
        /// </summary>
        [JsonPropertyName("globalDefinitions")]
        public Dictionary<string, string> GlobalDefinitions { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// True if the compiler is Clang.
        /// </summary>
        [JsonPropertyName("compilerIsClang")]
        public bool CompilerIsClang { get; set; }

        /// <summary>
        /// The compiler's target platform string-based definitions.
        /// </summary>
        [JsonPropertyName("compilerTargetPlatformStringDefinitions")]
        public Dictionary<string, string> CompilerTargetPlatformStringDefinitions { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// The compiler's target platform numeric-based definitions.
        /// </summary>
        [JsonPropertyName("compilerTargetPlatformInt64Definitions")]
        public Dictionary<string, long> CompilerTargetPlatformInt64Definitions { get; set; } = new Dictionary<string, long>();
    }
}