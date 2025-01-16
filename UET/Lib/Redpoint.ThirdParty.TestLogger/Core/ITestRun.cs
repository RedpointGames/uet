// Copyright (c) Spekt Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Spekt.TestLogger.Core
{
    using Spekt.TestLogger.Extensions;
    using Spekt.TestLogger.Platform;

    public interface ITestRun
    {
        LoggerConfiguration LoggerConfiguration { get; }

        TestRunConfiguration RunConfiguration { get; }

        ITestAdapterFactory AdapterFactory { get; }

        ITestResultStore Store { get; }

        ITestResultSerializer Serializer { get; }

        IConsoleOutput ConsoleOutput { get; }

        IFileSystem FileSystem { get; }
    }
}