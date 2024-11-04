namespace Redpoint.Uet.BuildPipeline.MultiWorkspace
{
    using System;
    using System.Text.RegularExpressions;

    public readonly record struct InterpolatedString
    {
        private static readonly Regex _regex = new Regex(@"\$\{env\:([A-Za-z_]+)\}");

        public readonly string PatternValue { get; private init; }

        public InterpolatedString(string patternValue)
        {
            PatternValue = patternValue;
        }

        public string EvaluatedString
        {
            get
            {
                return _regex.Replace(
                    PatternValue,
                    x =>
                    {
                        return Environment.GetEnvironmentVariable(x.Groups[0].Value) ?? string.Empty;
                    });
            }
        }
    }
}
