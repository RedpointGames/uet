namespace Redpoint.ProcessExecution.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class ArgumentParsingTests
    {
        [Fact]
        public void ClangTidyArgumentsAreParsedCorrectly()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddProcessExecution();
            var sp = services.BuildServiceProvider();

            var parser = sp.GetRequiredService<IProcessArgumentParser>();

            var arguments = parser.SplitArguments("--lua-script-path=\"C:\\ProgramData\\ClangTidyForUnrealEngine-15\\ClangTidySystem.lua\" --lua-script-path=\"TEST\\H\\Plugins\\P\\Resources\\ClangTidy.lua\" --checks=-* --touch-path=\"TEST\\H\\Plugins\\P\\Intermediate\\Build\\Win64\\x64\\UnrealEditor\\Development\\OnlineSubsystem RedpointItchIo\\_CT\\Module.OnlineSubsystem RedpointItchIo.cpp.cttouch\" --quiet \"--header-filter=.*/OnlineSubsystem RedpointItchIo/.*\" \"-p=TEST\\H\\Plugins\\P\\Intermediate\\Build\\Win64\\x64\\UnrealEditor\\Development\\OnlineSubsystem RedpointItchIo\\_CT\\0a4a2f7ff700\\compile_commands.json\" \"TEST\\H\\Plugins\\P\\Intermediate\\Build\\Win64\\x64\\UnrealEditor\\Development\\OnlineSubsystem RedpointItchIo\\Module.OnlineSubsystem RedpointItchIo.cpp\"");

            Assert.Equal(8, arguments.Count);
            Assert.Equal("--lua-script-path=C:\\ProgramData\\ClangTidyForUnrealEngine-15\\ClangTidySystem.lua", arguments[0].LogicalValue);
            Assert.Equal("--lua-script-path=TEST\\H\\Plugins\\P\\Resources\\ClangTidy.lua", arguments[1].LogicalValue);
            Assert.Equal("--checks=-*", arguments[2].LogicalValue);
            Assert.Equal("--touch-path=TEST\\H\\Plugins\\P\\Intermediate\\Build\\Win64\\x64\\UnrealEditor\\Development\\OnlineSubsystem RedpointItchIo\\_CT\\Module.OnlineSubsystem RedpointItchIo.cpp.cttouch", arguments[3].LogicalValue);
            Assert.Equal("--quiet", arguments[4].LogicalValue);
            Assert.Equal("--header-filter=.*/OnlineSubsystem RedpointItchIo/.*", arguments[5].LogicalValue);
            Assert.Equal("-p=TEST\\H\\Plugins\\P\\Intermediate\\Build\\Win64\\x64\\UnrealEditor\\Development\\OnlineSubsystem RedpointItchIo\\_CT\\0a4a2f7ff700\\compile_commands.json", arguments[6].LogicalValue);
            Assert.Equal("TEST\\H\\Plugins\\P\\Intermediate\\Build\\Win64\\x64\\UnrealEditor\\Development\\OnlineSubsystem RedpointItchIo\\Module.OnlineSubsystem RedpointItchIo.cpp", arguments[7].LogicalValue);
        }

        [Fact]
        public void CmdCopyArgumentsAreParsedCorrectly()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddProcessExecution();
            var sp = services.BuildServiceProvider();

            var parser = sp.GetRequiredService<IProcessArgumentParser>();

            var arguments = parser.SplitArguments("/c copy \"C:\\AAAA\\BBBB\\CCCC\\../DDDD/EEEE/FFFF\\GGGG.HHH\" \"C:\\UES\\1bf8s4lvhbyqqk-1\\Engine\\Intermediate\\Build\\IIII\\JJJJ\\UnrealGame\\Development\\KKKK\\LLLL\"");

            Assert.Equal(4, arguments.Count);
            Assert.Equal("/c", arguments[0].LogicalValue);
            Assert.Equal("copy", arguments[1].LogicalValue);
            Assert.Equal("C:\\AAAA\\BBBB\\CCCC\\../DDDD/EEEE/FFFF\\GGGG.HHH", arguments[2].LogicalValue);
            Assert.Equal("C:\\UES\\1bf8s4lvhbyqqk-1\\Engine\\Intermediate\\Build\\IIII\\JJJJ\\UnrealGame\\Development\\KKKK\\LLLL", arguments[3].LogicalValue);
        }

        [Fact]
        public void CreateArgumentFromLogicalValue()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddProcessExecution();
            var sp = services.BuildServiceProvider();

            var parser = sp.GetRequiredService<IProcessArgumentParser>();

            Assert.Equal(
                new EscapedProcessArgument("a", "a"),
                parser.CreateArgumentFromLogicalValue("a"));
            Assert.Equal(
                new EscapedProcessArgument("a b", "\"a b\""),
                parser.CreateArgumentFromLogicalValue("a b"));
            Assert.Equal(
                new EscapedProcessArgument("\"a b\"", "\"a b\""),
                parser.CreateArgumentFromLogicalValue("\"a b\""));
            Assert.Equal(
                new EscapedProcessArgument(
                    @"--lua-script-path=""C:\ProgramData\ClangTidyForUnrealEngine-15\ClangTidySystem.lua""",
                    @"--lua-script-path=""C:\ProgramData\ClangTidyForUnrealEngine-15\ClangTidySystem.lua"""),
                parser.CreateArgumentFromLogicalValue(@"--lua-script-path=""C:\ProgramData\ClangTidyForUnrealEngine-15\ClangTidySystem.lua"""));
            Assert.Equal(
                new EscapedProcessArgument(
                    @"/DSOME_DIR=""C:\Path\With\Trailing\Slash\""",
                    @"/DSOME_DIR=""C:\Path\With\Trailing\Slash\"""),
                parser.CreateArgumentFromLogicalValue(@"/DSOME_DIR=""C:\Path\With\Trailing\Slash\"""));
        }

        [Fact]
        public void CreateArgumentFromOriginalValue()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddProcessExecution();
            var sp = services.BuildServiceProvider();

            var parser = sp.GetRequiredService<IProcessArgumentParser>();

            Assert.Equal(
                new EscapedProcessArgument("a", "a"),
                parser.CreateArgumentFromOriginalValue("a"));
            Assert.Equal(
                new EscapedProcessArgument("a b", "\"a b\""),
                parser.CreateArgumentFromOriginalValue("\"a b\""));
            Assert.Equal(
                new EscapedProcessArgument(
                    @"--lua-script-path=C:\ProgramData\ClangTidyForUnrealEngine-15\ClangTidySystem.lua",
                    @"--lua-script-path=""C:\ProgramData\ClangTidyForUnrealEngine-15\ClangTidySystem.lua"""),
                parser.CreateArgumentFromOriginalValue(@"--lua-script-path=""C:\ProgramData\ClangTidyForUnrealEngine-15\ClangTidySystem.lua"""));
            Assert.Equal(
                @"/DSOME_DIR=C:\Path\With\Trailing\Slash\",
                parser.CreateArgumentFromOriginalValue(@"/DSOME_DIR=""C:\Path\With\Trailing\Slash\""").LogicalValue);
            Assert.Equal(
                @"/DSOME_DIR=""C:\Path\With\Trailing\Slash\""",
                parser.CreateArgumentFromOriginalValue(@"/DSOME_DIR=""C:\Path\With\Trailing\Slash\""").OriginalValue);
        }

        [Fact]
        public void RoundTripComplexCommandLine()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddProcessExecution();
            var sp = services.BuildServiceProvider();

            var parser = sp.GetRequiredService<IProcessArgumentParser>();

            var originalCommandLine = @"/nologo /D_WIN64 /l 0x409 /I ""."" /I ""C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.35.32215\INCLUDE"" /I ""C:\ProgramData\UET\SDKs\AutoSDK-xrrwsh.drhos-q4j4x5t\HostWin64\Win64\Windows Kits\10\include\10.0.19041.0\ucrt"" /I ""C:\ProgramData\UET\SDKs\AutoSDK-xrrwsh.drhos-q4j4x5t\HostWin64\Win64\Windows Kits\10\include\10.0.19041.0\shared"" /I ""C:\ProgramData\UET\SDKs\AutoSDK-xrrwsh.drhos-q4j4x5t\HostWin64\Win64\Windows Kits\10\include\10.0.19041.0\um"" /I ""C:\ProgramData\UET\SDKs\AutoSDK-xrrwsh.drhos-q4j4x5t\HostWin64\Win64\Windows Kits\10\include\10.0.19041.0\winrt"" /DIS_PROGRAM=0 /DUE_GAME=1 /DUSE_SHADER_COMPILER_WORKER_TRACE=0 /DUE_REFERENCE_COLLECTOR_REQUIRE_OBJECTPTR=1 /DWITH_VERSE_VM=0 /DENABLE_PGO_PROFILE=0 /DUSE_VORBIS_FOR_STREAMING=1 /DUSE_XMA2_FOR_STREAMING=1 /DWITH_DEV_AUTOMATION_TESTS=1 /DWITH_PERF_AUTOMATION_TESTS=1 /DWITH_LOW_LEVEL_TESTS=0 /DEXPLICIT_TESTS_TARGET=0 /DWITH_TESTS=1 /DUNICODE /D_UNICODE /D__UNREAL__ /DIS_MONOLITHIC=1 /DWITH_ENGINE=1 /DWITH_UNREAL_DEVELOPER_TOOLS=1 /DWITH_UNREAL_TARGET_DEVELOPER_TOOLS=1 /DWITH_APPLICATION_CORE=1 /DWITH_COREUOBJECT=1 /DUE_TRACE_ENABLED=1 /DWITH_VERSE=1 /DUE_USE_VERSE_PATHS=1 /DWITH_VERSE_BPVM=1 /DUSE_STATS_WITHOUT_ENGINE=0 /DWITH_PLUGIN_SUPPORT=0 /DWITH_ACCESSIBILITY=1 /DWITH_PERFCOUNTERS=0 /DWITH_FIXED_TIME_STEP_SUPPORT=1 /DUSE_LOGGING_IN_SHIPPING=0 /DALLOW_CONSOLE_IN_SHIPPING=0 /DALLOW_PROFILEGPU_IN_TEST=0 /DALLOW_PROFILEGPU_IN_SHIPPING=0 /DWITH_LOGGING_TO_MEMORY=0 /DUSE_CACHE_FREED_OS_ALLOCS=1 /DUSE_CHECKS_IN_SHIPPING=0 /DUSE_UTF8_TCHARS=0 /DUSE_ESTIMATED_UTCNOW=0 /DUE_ALLOW_EXEC_COMMANDS_IN_SHIPPING=1 /DWITH_EDITOR=0 /DWITH_EDITORONLY_DATA=0 /DWITH_SERVER_CODE=1 /DUE_FNAME_OUTLINE_NUMBER=0 /DWITH_PUSH_MODEL=0 /DWITH_CEF3=1 /DWITH_LIVE_CODING=1 /DUE_LIVE_CODING_ENGINE_DIR=""C:\\UES\\9i950g5-xow43x-001\\Engine"" /DUE_LIVE_CODING_PROJECT=""C:\\UES\\g8yyhpr1t600zh-000\\Lyra\\Lyra.uproject"" /DWITH_CPP_MODULES=0 /DWITH_CPP_COROUTINES=0 /DWITH_PROCESS_PRIORITY_CONTROL=0 /DUBT_MODULE_MANIFEST=""UnrealGame.modules"" /DUBT_MODULE_MANIFEST_DEBUGGAME=""UnrealGame-Win64-DebugGame.modules"" /DUBT_COMPILED_PLATFORM=Win64 /DUBT_COMPILED_TARGET=Game /DUE_APP_NAME=""UnrealGame"" /DUE_WARNINGS_AS_ERRORS=0 /DUE_ENGINE_DIRECTORY=""../../../../9i950g5-xow43x-001/Engine/"" /DNDIS_MINIPORT_MAJOR_VERSION=0 /DWIN32=1 /D_WIN32_WINNT=0x0601 /DWINVER=0x0601 /DPLATFORM_WINDOWS=1 /DPLATFORM_MICROSOFT=1 /DOVERRIDE_PLATFORM_HEADER_NAME=Windows /DRHI_RAYTRACING=1 /DWINDOWS_MAX_NUM_TLS_SLOTS=2048 /DWINDOWS_MAX_NUM_THREADS_WITH_TLS_SLOTS=512 /DNDEBUG=1 /DUE_BUILD_DEVELOPMENT=1 /DORIGINAL_FILE_NAME=""LyraGame-Win64-DebugGame.exe"" /DBUILD_ICON_FILE_NAME=""\""C:\\UES\\g8yyhpr1t600zh-000\\Lyra\\Build\\Windows\\Application.ico\"""" /DPROJECT_COPYRIGHT_STRING=""Fill out your copyright notice in the Description page of Project Settings."" /DPROJECT_PRODUCT_NAME=Lyra /DPROJECT_PRODUCT_IDENTIFIER=Lyra /fo ""C:\UES\g8yyhpr1t600zh-000\Lyra\Intermediate\Build\Win64\x64\LyraGame\DebugGame\Default.rc2.res"" ""..\Build\Windows\Resources\Default.rc2""";

            var expectedArguments = new string[]
            {
                @"/nologo",
                @"/D_WIN64",
                @"/l",
                @"0x409",
                @"/I",
                @""".""",
                @"/I",
                @"""C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.35.32215\INCLUDE""",
                @"/I",
                @"""C:\ProgramData\UET\SDKs\AutoSDK-xrrwsh.drhos-q4j4x5t\HostWin64\Win64\Windows Kits\10\include\10.0.19041.0\ucrt""",
                @"/I",
                @"""C:\ProgramData\UET\SDKs\AutoSDK-xrrwsh.drhos-q4j4x5t\HostWin64\Win64\Windows Kits\10\include\10.0.19041.0\shared""",
                @"/I",
                @"""C:\ProgramData\UET\SDKs\AutoSDK-xrrwsh.drhos-q4j4x5t\HostWin64\Win64\Windows Kits\10\include\10.0.19041.0\um""",
                @"/I",
                @"""C:\ProgramData\UET\SDKs\AutoSDK-xrrwsh.drhos-q4j4x5t\HostWin64\Win64\Windows Kits\10\include\10.0.19041.0\winrt""",
                @"/DIS_PROGRAM=0",
                @"/DUE_GAME=1",
                @"/DUSE_SHADER_COMPILER_WORKER_TRACE=0",
                @"/DUE_REFERENCE_COLLECTOR_REQUIRE_OBJECTPTR=1",
                @"/DWITH_VERSE_VM=0",
                @"/DENABLE_PGO_PROFILE=0",
                @"/DUSE_VORBIS_FOR_STREAMING=1",
                @"/DUSE_XMA2_FOR_STREAMING=1",
                @"/DWITH_DEV_AUTOMATION_TESTS=1",
                @"/DWITH_PERF_AUTOMATION_TESTS=1",
                @"/DWITH_LOW_LEVEL_TESTS=0",
                @"/DEXPLICIT_TESTS_TARGET=0",
                @"/DWITH_TESTS=1",
                @"/DUNICODE",
                @"/D_UNICODE",
                @"/D__UNREAL__",
                @"/DIS_MONOLITHIC=1",
                @"/DWITH_ENGINE=1",
                @"/DWITH_UNREAL_DEVELOPER_TOOLS=1",
                @"/DWITH_UNREAL_TARGET_DEVELOPER_TOOLS=1",
                @"/DWITH_APPLICATION_CORE=1",
                @"/DWITH_COREUOBJECT=1",
                @"/DUE_TRACE_ENABLED=1",
                @"/DWITH_VERSE=1",
                @"/DUE_USE_VERSE_PATHS=1",
                @"/DWITH_VERSE_BPVM=1",
                @"/DUSE_STATS_WITHOUT_ENGINE=0",
                @"/DWITH_PLUGIN_SUPPORT=0",
                @"/DWITH_ACCESSIBILITY=1",
                @"/DWITH_PERFCOUNTERS=0",
                @"/DWITH_FIXED_TIME_STEP_SUPPORT=1",
                @"/DUSE_LOGGING_IN_SHIPPING=0",
                @"/DALLOW_CONSOLE_IN_SHIPPING=0",
                @"/DALLOW_PROFILEGPU_IN_TEST=0",
                @"/DALLOW_PROFILEGPU_IN_SHIPPING=0",
                @"/DWITH_LOGGING_TO_MEMORY=0",
                @"/DUSE_CACHE_FREED_OS_ALLOCS=1",
                @"/DUSE_CHECKS_IN_SHIPPING=0",
                @"/DUSE_UTF8_TCHARS=0",
                @"/DUSE_ESTIMATED_UTCNOW=0",
                @"/DUE_ALLOW_EXEC_COMMANDS_IN_SHIPPING=1",
                @"/DWITH_EDITOR=0",
                @"/DWITH_EDITORONLY_DATA=0",
                @"/DWITH_SERVER_CODE=1",
                @"/DUE_FNAME_OUTLINE_NUMBER=0",
                @"/DWITH_PUSH_MODEL=0",
                @"/DWITH_CEF3=1",
                @"/DWITH_LIVE_CODING=1",
                @"/DUE_LIVE_CODING_ENGINE_DIR=""C:\\UES\\9i950g5-xow43x-001\\Engine""",
                @"/DUE_LIVE_CODING_PROJECT=""C:\\UES\\g8yyhpr1t600zh-000\\Lyra\\Lyra.uproject""",
                @"/DWITH_CPP_MODULES=0",
                @"/DWITH_CPP_COROUTINES=0",
                @"/DWITH_PROCESS_PRIORITY_CONTROL=0",
                @"/DUBT_MODULE_MANIFEST=""UnrealGame.modules""",
                @"/DUBT_MODULE_MANIFEST_DEBUGGAME=""UnrealGame-Win64-DebugGame.modules""",
                @"/DUBT_COMPILED_PLATFORM=Win64",
                @"/DUBT_COMPILED_TARGET=Game",
                @"/DUE_APP_NAME=""UnrealGame""",
                @"/DUE_WARNINGS_AS_ERRORS=0",
                @"/DUE_ENGINE_DIRECTORY=""../../../../9i950g5-xow43x-001/Engine/""",
                @"/DNDIS_MINIPORT_MAJOR_VERSION=0",
                @"/DWIN32=1",
                @"/D_WIN32_WINNT=0x0601",
                @"/DWINVER=0x0601",
                @"/DPLATFORM_WINDOWS=1",
                @"/DPLATFORM_MICROSOFT=1",
                @"/DOVERRIDE_PLATFORM_HEADER_NAME=Windows",
                @"/DRHI_RAYTRACING=1",
                @"/DWINDOWS_MAX_NUM_TLS_SLOTS=2048",
                @"/DWINDOWS_MAX_NUM_THREADS_WITH_TLS_SLOTS=512",
                @"/DNDEBUG=1",
                @"/DUE_BUILD_DEVELOPMENT=1",
                @"/DORIGINAL_FILE_NAME=""LyraGame-Win64-DebugGame.exe""",
                @"/DBUILD_ICON_FILE_NAME=""\""C:\\UES\\g8yyhpr1t600zh-000\\Lyra\\Build\\Windows\\Application.ico\""""",
                @"/DPROJECT_COPYRIGHT_STRING=""Fill out your copyright notice in the Description page of Project Settings.""",
                @"/DPROJECT_PRODUCT_NAME=Lyra",
                @"/DPROJECT_PRODUCT_IDENTIFIER=Lyra",
                @"/fo",
                @"""C:\UES\g8yyhpr1t600zh-000\Lyra\Intermediate\Build\Win64\x64\LyraGame\DebugGame\Default.rc2.res""",
                @"""..\Build\Windows\Resources\Default.rc2"""
            }.Select(parser.CreateArgumentFromOriginalValue).ToList();

            var arguments = parser.SplitArguments(originalCommandLine);
            Assert.Equal(expectedArguments, arguments);

            var restoredCommandLine = parser.JoinArguments(arguments);
            Assert.Equal(originalCommandLine, restoredCommandLine);
        }
    }
}
