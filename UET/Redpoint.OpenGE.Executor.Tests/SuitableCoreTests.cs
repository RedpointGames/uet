namespace Redpoint.OpenGE.Tests
{
    using Redpoint.OpenGE.Executor;
    using System.Runtime.Versioning;

#if HAS_WMI
    public class SuitableCoreTests
    {
        [Fact]
        [SupportedOSPlatform("windows")]
        public void CanGetSuitableCores()
        {
            var nextCore = ProcessWideCoreReservation.GetNextSuitableCoreForAssignment(new HashSet<int>());

            // We just want to make sure the above method doesn't crash.
            Assert.True(true);
        }
    }
#endif

    public class ArgumentParsingTests
    {
        [Fact]
        public void ArgumentsAreParsedCorrectly()
        {
            var arguments = DefaultOpenGEExecutor.SplitArguments("--lua-script-path=\"C:\\ProgramData\\ClangTidyForUnrealEngine-15\\ClangTidySystem.lua\" --lua-script-path=\"TEST\\H\\Plugins\\P\\Resources\\ClangTidy.lua\" --checks=-* --touch-path=\"TEST\\H\\Plugins\\P\\Intermediate\\Build\\Win64\\x64\\UnrealEditor\\Development\\OnlineSubsystem RedpointItchIo\\_CT\\Module.OnlineSubsystem RedpointItchIo.cpp.cttouch\" --quiet \"--header-filter=.*/OnlineSubsystem RedpointItchIo/.*\" \"-p=TEST\\H\\Plugins\\P\\Intermediate\\Build\\Win64\\x64\\UnrealEditor\\Development\\OnlineSubsystem RedpointItchIo\\_CT\\0a4a2f7ff700\\compile_commands.json\" \"TEST\\H\\Plugins\\P\\Intermediate\\Build\\Win64\\x64\\UnrealEditor\\Development\\OnlineSubsystem RedpointItchIo\\Module.OnlineSubsystem RedpointItchIo.cpp\"");

            Assert.Equal(8, arguments.Length);
            Assert.Equal("--lua-script-path=C:\\ProgramData\\ClangTidyForUnrealEngine-15\\ClangTidySystem.lua", arguments[0]);
            Assert.Equal("--lua-script-path=TEST\\H\\Plugins\\P\\Resources\\ClangTidy.lua", arguments[1]);
            Assert.Equal("--checks=-*", arguments[2]);
            Assert.Equal("--touch-path=TEST\\H\\Plugins\\P\\Intermediate\\Build\\Win64\\x64\\UnrealEditor\\Development\\OnlineSubsystem RedpointItchIo\\_CT\\Module.OnlineSubsystem RedpointItchIo.cpp.cttouch", arguments[3]);
            Assert.Equal("--quiet", arguments[4]);
            Assert.Equal("--header-filter=.*/OnlineSubsystem RedpointItchIo/.*", arguments[5]);
            Assert.Equal("-p=TEST\\H\\Plugins\\P\\Intermediate\\Build\\Win64\\x64\\UnrealEditor\\Development\\OnlineSubsystem RedpointItchIo\\_CT\\0a4a2f7ff700\\compile_commands.json", arguments[6]);
            Assert.Equal("TEST\\H\\Plugins\\P\\Intermediate\\Build\\Win64\\x64\\UnrealEditor\\Development\\OnlineSubsystem RedpointItchIo\\Module.OnlineSubsystem RedpointItchIo.cpp", arguments[7]);
        }
    }
}