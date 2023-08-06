namespace Redpoint.OpenGE.JobXml.Tests
{
    using System.Reflection;
    using Xunit;

    public class JobXmlReaderTests
    {
        [Fact]
        public void CanReadJobXmlFromFile()
        {
            var job = JobXmlReader.ParseJobXml(Assembly.GetExecutingAssembly().GetManifestResourceStream("Redpoint.OpenGE.JobXml.Tests.UAT_XGE.xml")!);

            Assert.NotNull(job);

            Assert.True(job.Environments.Count == 1);
            Assert.True(job.Environments.ContainsKey("Env_0"));
            Assert.True(job.Environments["Env_0"].Tools.Count == 2);

            Assert.True(job.Environments["Env_0"].Tools.ContainsKey("Tool1_0"));
            Assert.Equal("Tool1_0", job.Environments["Env_0"].Tools["Tool1_0"].Name);
            // Assert.True(buildSet.Environments["Env_0"].Tools["Tool1_0"].AllowRemote);
            // Assert.Equal("GROUP_PREFIX_1", buildSet.Environments["Env_0"].Tools["Tool1_0"].GroupPrefix);
            Assert.Equal("PARAMS_1", job.Environments["Env_0"].Tools["Tool1_0"].Params);
            Assert.Equal("PATH_1", job.Environments["Env_0"].Tools["Tool1_0"].Path);
            // Assert.True(buildSet.Environments["Env_0"].Tools["Tool1_0"].SkipIfProjectFailed);
            // Assert.Equal("*.pch", buildSet.Environments["Env_0"].Tools["Tool1_0"].AutoReserveMemory);
            // Assert.Equal("OUTPUT_MASK_1", buildSet.Environments["Env_0"].Tools["Tool1_0"].OutputFileMasks);
            // Assert.Equal("C1060,C1076,C3859", buildSet.Environments["Env_0"].Tools["Tool1_0"].AutoRecover);

            Assert.True(job.Environments["Env_0"].Tools.ContainsKey("Tool2_0"));
            Assert.Equal("Tool2_0", job.Environments["Env_0"].Tools["Tool2_0"].Name);
            // Assert.True(buildSet.Environments["Env_0"].Tools["Tool2_0"].AllowRemote);
            // Assert.Equal("GROUP_PREFIX_2", buildSet.Environments["Env_0"].Tools["Tool2_0"].GroupPrefix);
            Assert.Equal("PARAMS_2", job.Environments["Env_0"].Tools["Tool2_0"].Params);
            Assert.Equal("PATH_2", job.Environments["Env_0"].Tools["Tool2_0"].Path);
            // Assert.True(buildSet.Environments["Env_0"].Tools["Tool2_0"].SkipIfProjectFailed);
            // Assert.Equal("*.pch", buildSet.Environments["Env_0"].Tools["Tool2_0"].AutoReserveMemory);
            // Assert.Equal("OUTPUT_MASK_2", buildSet.Environments["Env_0"].Tools["Tool2_0"].OutputFileMasks);
            // Assert.Equal("C1060,C1076,C3859", buildSet.Environments["Env_0"].Tools["Tool2_0"].AutoRecover);

            Assert.True(job.Environments["Env_0"].Variables.Count == 1);
            Assert.True(job.Environments["Env_0"].Variables.ContainsKey("VARIABLE_1"));
            Assert.Equal("VARIABLE_VALUE_1", job.Environments["Env_0"].Variables["VARIABLE_1"]);

            Assert.True(job.Projects.Count == 1);
            Assert.True(job.Projects.ContainsKey("Env_0"));
            Assert.Equal("Env_0", job.Projects["Env_0"].Name);
            Assert.Equal("Env_0", job.Projects["Env_0"].Env);

            Assert.True(job.Projects["Env_0"].Tasks.Count == 2);

            Assert.True(job.Projects["Env_0"].Tasks.ContainsKey("Action1_0"));
            Assert.Equal("Action1_0", job.Projects["Env_0"].Tasks["Action1_0"].Name);
            // Assert.Equal(string.Empty, buildSet.Projects["Env_0"].Tasks["Action1_0"].SourceFile);
            Assert.Equal("CAPTION_1", job.Projects["Env_0"].Tasks["Action1_0"].Caption);
            Assert.Equal("Tool1_0", job.Projects["Env_0"].Tasks["Action1_0"].Tool);
            Assert.Equal("WORKING_DIR_1", job.Projects["Env_0"].Tasks["Action1_0"].WorkingDir);
            Assert.True(job.Projects["Env_0"].Tasks["Action1_0"].SkipIfProjectFailed);
            Assert.Null(job.Projects["Env_0"].Tasks["Action1_0"].DependsOn);

            Assert.True(job.Projects["Env_0"].Tasks.ContainsKey("Action2_0"));
            Assert.Equal("Action2_0", job.Projects["Env_0"].Tasks["Action2_0"].Name);
            // Assert.Equal(string.Empty, buildSet.Projects["Env_0"].Tasks["Action2_0"].SourceFile);
            Assert.Equal("CAPTION_2", job.Projects["Env_0"].Tasks["Action2_0"].Caption);
            Assert.Equal("Tool2_0", job.Projects["Env_0"].Tasks["Action2_0"].Tool);
            Assert.Equal("WORKING_DIR_2", job.Projects["Env_0"].Tasks["Action2_0"].WorkingDir);
            Assert.True(job.Projects["Env_0"].Tasks["Action2_0"].SkipIfProjectFailed);
            Assert.Equal("Action1_0", job.Projects["Env_0"].Tasks["Action2_0"].DependsOn);
        }
    }
}