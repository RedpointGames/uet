// Copyright (c) Spekt Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Spekt.TestLogger.Core
{
    using System;
    using System.Collections.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    public sealed class TestResultInfo
    {
        private readonly TestResult result;

        public TestResultInfo(
            TestResult result,
            string @namespace,
            string type,
            string method)
        {
            this.result = result;
            this.Namespace = @namespace;
            this.Type = type;
            this.Method = method;
            this.Outcome = result.Outcome;
        }

        public TestCase TestCase => this.result.TestCase;

        public TestOutcome Outcome { get; set; }

        public string AssemblyPath => this.result.TestCase.Source;

        public string Namespace { get; private set; }

        public string Type { get; private set; }

        public string FullTypeName => this.Namespace + "." + this.Type;

        /// <summary>
        /// Gets a string that contain the method name, along with any paramaterized data related to
        /// the method. For example, `SomeMethod` or `SomeParameterizedMethod(true)`.
        /// </summary>
        public string Method { get; internal set; }

        public DateTime StartTime => this.result.StartTime.UtcDateTime;

        public DateTime EndTime => this.result.EndTime.UtcDateTime;

        public TimeSpan Duration => this.result.Duration;

        public string ErrorMessage => this.result.ErrorMessage;

        public string ErrorStackTrace => this.result.ErrorStackTrace;

        public Collection<TestResultMessage> Messages => this.result.Messages;

        public TraitCollection Traits => this.result.Traits;

        internal TestResult Result => this.result;

        public override int GetHashCode()
        {
            return this.result.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is not TestResultInfo objectToCompare)
            {
                return false;
            }

            return string.Compare(this.ErrorMessage, objectToCompare.ErrorMessage, StringComparison.CurrentCulture) == 0
                   && string.Compare(this.ErrorStackTrace, objectToCompare.ErrorStackTrace, StringComparison.CurrentCulture) == 0;
        }
    }
}
