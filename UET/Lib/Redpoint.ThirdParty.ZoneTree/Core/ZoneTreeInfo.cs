using System.Diagnostics;
using System.Reflection;

namespace Tenray.ZoneTree.Core;

public static class ZoneTreeInfo
{
    static Version Version = new Version(1, 6, 2);

    /// <summary>
    /// Gets ZoneTree Product Version
    /// </summary>
    /// <returns></returns>
    public static Version ProductVersion
    {
        get
        {
            return Version;
        }
    }
}
