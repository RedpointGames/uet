// Copyright (c) Spekt Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Spekt.TestLogger.Core
{
    using Spekt.TestLogger.Extensions;
    using Spekt.TestLogger.Platform;

    public class TestRun : ITestRun
    {
        public LoggerConfiguration LoggerConfiguration { get; internal set; }

        public TestRunConfiguration RunConfiguration { get; internal set; }

        public ITestAdapterFactory AdapterFactory { get; internal set; }

        public ITestResultStore Store { get; internal set; }

        public ITestResultSerializer Serializer { get; internal set; }

        public IConsoleOutput ConsoleOutput { get; internal set; }

        public IFileSystem FileSystem { get; internal set; }
    }
}