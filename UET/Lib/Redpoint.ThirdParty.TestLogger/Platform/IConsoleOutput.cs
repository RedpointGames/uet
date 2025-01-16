// Copyright (c) Spekt Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Spekt.TestLogger.Platform
{
    public interface IConsoleOutput
    {
        void WriteMessage(string message);

        void WriteError(string message);
    }
}