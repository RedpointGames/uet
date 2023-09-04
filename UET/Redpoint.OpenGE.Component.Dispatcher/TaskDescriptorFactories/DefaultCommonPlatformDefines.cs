namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories
{
    using Redpoint.OpenGE.Protocol;
    using System.Collections.Generic;

    internal class DefaultCommonPlatformDefines : ICommonPlatformDefines
    {
        public void ApplyDefines(string platform, CompilerArchitype compilerArchitype)
        {
            switch (platform)
            {
                case "Win64":
                    compilerArchitype.TargetPlatformNumericDefines.Add(new Dictionary<string, long>
                    {
                        { "__x86_64__", 1 },
                        { "_WIN32", 1 },
                        { "_WIN64", 1 },
                        { "_WIN32_WINNT", 0x0601 /* Windows 7 */ },
                        { "_WIN32_WINNT_WIN10_TH2", 0x0A01 },
                        { "_WIN32_WINNT_WIN10_RS1", 0x0A02 },
                        { "_WIN32_WINNT_WIN10_RS2", 0x0A03 },
                        { "_WIN32_WINNT_WIN10_RS3", 0x0A04 },
                        { "_WIN32_WINNT_WIN10_RS4", 0x0A05 },
                        { "_NT_TARGET_VERSION_WIN10_RS4", 0x0A05 },
                        { "_WIN32_WINNT_WIN10_RS5", 0x0A06 },
                        { "_M_X64", 1 },
                        { "_VCRT_COMPILER_PREPROCESSOR", 1 },
                        // Hmmm...
                        { "_USE_MATH_DEFINES", 1 },
                    });
                    break;
            }
        }
    }
}
