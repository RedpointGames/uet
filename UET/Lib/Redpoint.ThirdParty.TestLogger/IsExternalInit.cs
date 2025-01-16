// Copyright (c) Spekt Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    /// <summary>
    /// Required for using the C# 9.0 init properties language feature.
    /// Defining this explicitly since this is a .NET standard 1.5 library.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}
