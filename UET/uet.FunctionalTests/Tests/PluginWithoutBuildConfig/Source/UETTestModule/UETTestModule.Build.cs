using UnrealBuildTool;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Diagnostics;

public class UETTestModule : ModuleRules
{
    public UETTestModule(ReadOnlyTargetRules Target) : base(Target)
    {
        DefaultBuildSettings = BuildSettingsVersion.V2;
        PrivateDependencyModuleNames.AddRange(new string[] {
            "Core",
        });
    }
}