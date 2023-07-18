namespace Redpoint.OpenGE.Tests
{
    using Redpoint.OpenGE.Executor;

    public class ArgumentParsingTests
    {
        [Fact]
        public void ClangTidyArgumentsAreParsedCorrectly()
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

        [Fact]
        public void CmdCopyArgumentsAreParsedCorrectly()
        {
            var arguments = DefaultOpenGEExecutor.SplitArguments("/c copy \"C:\\AAAA\\BBBB\\CCCC\\../DDDD/EEEE/FFFF\\GGGG.HHH\" \"C:\\UES\\1bf8s4lvhbyqqk-1\\Engine\\Intermediate\\Build\\IIII\\JJJJ\\UnrealGame\\Development\\KKKK\\LLLL\"");

            Assert.Equal(4, arguments.Length);
            Assert.Equal("/c", arguments[0]);
            Assert.Equal("copy", arguments[1]);
            Assert.Equal("C:\\AAAA\\BBBB\\CCCC\\../DDDD/EEEE/FFFF\\GGGG.HHH", arguments[2]);
            Assert.Equal("C:\\UES\\1bf8s4lvhbyqqk-1\\Engine\\Intermediate\\Build\\IIII\\JJJJ\\UnrealGame\\Development\\KKKK\\LLLL", arguments[3]);
        }
    }
}