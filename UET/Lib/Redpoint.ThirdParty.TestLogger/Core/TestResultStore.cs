// Copyright (c) Spekt Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Spekt.TestLogger.Core
{
    using System.Collections.Generic;
    using System.Threading;

    public class TestResultStore : ITestResultStore
    {
        private readonly Lock messageLock = new ();
        private readonly Lock resultLock = new ();

        private List<TestResultInfo> results;
        private List<TestMessageInfo> messages;

        public TestResultStore()
        {
            this.results = new List<TestResultInfo>();
            this.messages = new List<TestMessageInfo>();
        }

        public void Add(TestResultInfo result)
        {
            lock (this.resultLock)
            {
                this.results.Add(result);
            }
        }

        public void Add(TestMessageInfo message)
        {
            lock (this.messageLock)
            {
                this.messages.Add(message);
            }
        }

        public void Pop(out List<TestResultInfo> results, out List<TestMessageInfo> messages)
        {
            lock (this.resultLock)
            {
                results = this.results;
                this.results = new List<TestResultInfo>();
            }

            lock (this.messageLock)
            {
                messages = this.messages;
                this.messages = new List<TestMessageInfo>();
            }
        }
    }
}
