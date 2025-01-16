// Copyright (c) Spekt Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Spekt.TestLogger.Platform
{
    using System;

    public class ConsoleOutput : IConsoleOutput
    {
        public void WriteMessage(string message)
        {
            Console.WriteLine(message);
        }

        public void WriteError(string message)
        {
            Console.Error.WriteLine(message);
        }
    }
}