namespace Redpoint.OpenGE.Tests
{
    using Redpoint.OpenGE.Executor.BuildSetData;
    using System.Reflection;

    public class BuildSetReaderTests
    {
        [Fact]
        public void CanReadBuildSetFromFile()
        {
            var buildSet = BuildSetReader.ParseBuildSet(Assembly.GetExecutingAssembly().GetManifestResourceStream("Redpoint.OpenGE.Executor.Tests.UAT_XGE.xml")!);

            Assert.NotNull(buildSet);

            Assert.True(buildSet.Environments.Count == 1);
            Assert.True(buildSet.Environments.ContainsKey("Env_0"));
            Assert.True(buildSet.Environments["Env_0"].Tools.Count == 2);

            Assert.True(buildSet.Environments["Env_0"].Tools.ContainsKey("Tool1_0"));
            Assert.Equal("Tool1_0", buildSet.Environments["Env_0"].Tools["Tool1_0"].Name);
            // Assert.True(buildSet.Environments["Env_0"].Tools["Tool1_0"].AllowRemote);
            // Assert.Equal("GROUP_PREFIX_1", buildSet.Environments["Env_0"].Tools["Tool1_0"].GroupPrefix);
            Assert.Equal("PARAMS_1", buildSet.Environments["Env_0"].Tools["Tool1_0"].Params);
            Assert.Equal("PATH_1", buildSet.Environments["Env_0"].Tools["Tool1_0"].Path);
            // Assert.True(buildSet.Environments["Env_0"].Tools["Tool1_0"].SkipIfProjectFailed);
            // Assert.Equal("*.pch", buildSet.Environments["Env_0"].Tools["Tool1_0"].AutoReserveMemory);
            // Assert.Equal("OUTPUT_MASK_1", buildSet.Environments["Env_0"].Tools["Tool1_0"].OutputFileMasks);
            // Assert.Equal("C1060,C1076,C3859", buildSet.Environments["Env_0"].Tools["Tool1_0"].AutoRecover);

            Assert.True(buildSet.Environments["Env_0"].Tools.ContainsKey("Tool2_0"));
            Assert.Equal("Tool2_0", buildSet.Environments["Env_0"].Tools["Tool2_0"].Name);
            // Assert.True(buildSet.Environments["Env_0"].Tools["Tool2_0"].AllowRemote);
            // Assert.Equal("GROUP_PREFIX_2", buildSet.Environments["Env_0"].Tools["Tool2_0"].GroupPrefix);
            Assert.Equal("PARAMS_2", buildSet.Environments["Env_0"].Tools["Tool2_0"].Params);
            Assert.Equal("PATH_2", buildSet.Environments["Env_0"].Tools["Tool2_0"].Path);
            // Assert.True(buildSet.Environments["Env_0"].Tools["Tool2_0"].SkipIfProjectFailed);
            // Assert.Equal("*.pch", buildSet.Environments["Env_0"].Tools["Tool2_0"].AutoReserveMemory);
            // Assert.Equal("OUTPUT_MASK_2", buildSet.Environments["Env_0"].Tools["Tool2_0"].OutputFileMasks);
            // Assert.Equal("C1060,C1076,C3859", buildSet.Environments["Env_0"].Tools["Tool2_0"].AutoRecover);

            Assert.True(buildSet.Environments["Env_0"].Variables.Count == 1);
            Assert.True(buildSet.Environments["Env_0"].Variables.ContainsKey("VARIABLE_1"));
            Assert.Equal("VARIABLE_VALUE_1", buildSet.Environments["Env_0"].Variables["VARIABLE_1"]);

            Assert.True(buildSet.Projects.Count == 1);
            Assert.True(buildSet.Projects.ContainsKey("Env_0"));
            Assert.Equal("Env_0", buildSet.Projects["Env_0"].Name);
            Assert.Equal("Env_0", buildSet.Projects["Env_0"].Env);

            Assert.True(buildSet.Projects["Env_0"].Tasks.Count == 2);

            Assert.True(buildSet.Projects["Env_0"].Tasks.ContainsKey("Action1_0"));
            Assert.Equal("Action1_0", buildSet.Projects["Env_0"].Tasks["Action1_0"].Name);
            // Assert.Equal(string.Empty, buildSet.Projects["Env_0"].Tasks["Action1_0"].SourceFile);
            Assert.Equal("CAPTION_1", buildSet.Projects["Env_0"].Tasks["Action1_0"].Caption);
            Assert.Equal("Tool1_0", buildSet.Projects["Env_0"].Tasks["Action1_0"].Tool);
            Assert.Equal("WORKING_DIR_1", buildSet.Projects["Env_0"].Tasks["Action1_0"].WorkingDir);
            Assert.True(buildSet.Projects["Env_0"].Tasks["Action1_0"].SkipIfProjectFailed);
            Assert.Null(buildSet.Projects["Env_0"].Tasks["Action1_0"].DependsOn);

            Assert.True(buildSet.Projects["Env_0"].Tasks.ContainsKey("Action2_0"));
            Assert.Equal("Action2_0", buildSet.Projects["Env_0"].Tasks["Action2_0"].Name);
            // Assert.Equal(string.Empty, buildSet.Projects["Env_0"].Tasks["Action2_0"].SourceFile);
            Assert.Equal("CAPTION_2", buildSet.Projects["Env_0"].Tasks["Action2_0"].Caption);
            Assert.Equal("Tool2_0", buildSet.Projects["Env_0"].Tasks["Action2_0"].Tool);
            Assert.Equal("WORKING_DIR_2", buildSet.Projects["Env_0"].Tasks["Action2_0"].WorkingDir);
            Assert.True(buildSet.Projects["Env_0"].Tasks["Action2_0"].SkipIfProjectFailed);
            Assert.Equal("Action1_0", buildSet.Projects["Env_0"].Tasks["Action2_0"].DependsOn);
        }
    }
}