namespace Redpoint.Uet.Configuration.Project
{
    using System.Text.Json.Serialization;

    public class BuildConfigProjectBuild
    {
        /// <summary>
        /// The build configuration for the editor target.
        /// </summary>
        [JsonPropertyName("Editor"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigProjectBuildEditor? Editor { get; set; }

        /// <summary>
        /// The build configuration for the game target.
        /// </summary>
        [JsonPropertyName("Game"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigProjectBuildTarget? Game { get; set; }

        /// <summary>
        /// The build configuration for the client target.
        /// </summary>
        [JsonPropertyName("Client"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigProjectBuildTarget? Client { get; set; }

        /// <summary>
        /// The build configuration for the server target.
        /// </summary>
        [JsonPropertyName("Server"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigProjectBuildTarget? Server { get; set; }

        /// <summary>
        /// If set to false, the build will not pass '-CreateReleaseVersion' when cooking and staging, so no
        /// release data is written under the project's 'Releases' folder. Defaults to true.
        /// </summary>
        [JsonPropertyName("CreateReleaseVersion"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? CreateReleaseVersion { get; set; }

        /// <summary>
        /// If set to false, content is cooked unversioned, which is what BuildCookRun does by default and produces
        /// smaller packages. Defaults to true.
        /// </summary>
        [JsonPropertyName("CookVersioned"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? CookVersioned { get; set; }

        /// <summary>
        /// Additional arguments appended verbatim to every BuildCookRun invocation when staging and packaging,
        /// for example "-iostore -compressed -archive -archivedirectory=C:\Builds".
        /// </summary>
        [JsonPropertyName("BuildCookRunArguments"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? BuildCookRunArguments { get; set; }

        /// <summary>
        /// If set to false, the release version is not written into the project's Config/DefaultEngine.ini under
        /// [Redpoint.UnrealEngineTool]. Writing it rewrites the file and drops its comments. Defaults to true.
        /// </summary>
        [JsonPropertyName("WriteReleaseVersionToConfig"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? WriteReleaseVersionToConfig { get; set; }
    }
}
