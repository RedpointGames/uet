namespace Redpoint.OpenGE.LexerParser.Benchmark
{
    using BenchmarkDotNet.Attributes;
    using Redpoint.Lexer;
    using System.Text.RegularExpressions;

    [MemoryDiagnoser]
    public partial class LexerBenchmarks
    {
        private readonly Regex _regexCompiled;

        public LexerBenchmarks()
        {
            _regexCompiled = new Regex("^[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.Compiled);
        }

        private const string _benchmarkString = "someIdent_ifier3Hello";

        [GeneratedRegex("^[a-zA-Z_][a-zA-Z0-9_]*")]
        private static partial Regex RegenGen();

        [GeneratedRegex("^[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.NonBacktracking)]
        private static partial Regex RegenGenNoBacktracking();

        [LexerTokenizer("[a-zA-Z_][a-zA-Z0-9_]*")]
        public static partial ReadOnlySpan<char> ConsumeWord(
            ref ReadOnlySpan<char> span,
            ref LexerCursor cursor);

        [Benchmark]
        public void UseLex()
        {
            var word = _benchmarkString.AsSpan();
            var cursor = default(LexerCursor);
            ConsumeWord(ref word, ref cursor);
        }

        [Benchmark]
        public Match UseRegex()
        {
            return Regex.Match(_benchmarkString, "^[a-zA-Z_][a-zA-Z0-9_]*");
        }

        [Benchmark]
        public Match UseRegexCompiled()
        {
            return _regexCompiled.Match(_benchmarkString);
        }

        [Benchmark]
        public Match UseRegexGen()
        {
            return RegenGen().Match(_benchmarkString);
        }

        [Benchmark]
        public Match UseRegenGenNoBacktracking()
        {
            return RegenGenNoBacktracking().Match(_benchmarkString);
        }
    }
}