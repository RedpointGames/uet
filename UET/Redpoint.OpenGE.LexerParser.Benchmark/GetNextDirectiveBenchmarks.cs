namespace Redpoint.OpenGE.LexerParser.Benchmark
{
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Diagnostics.Windows.Configs;
    using Redpoint.Lexer;
    using Redpoint.OpenGE.Component.PreprocessorCache.DirectiveScanner;

    [MemoryDiagnoser]
    public class GetNextDirectiveBenchmarks
    {
        private readonly string _content;

        public GetNextDirectiveBenchmarks()
        {
            _content = File.ReadAllText(@"C:\ProgramData\UET\SDKs\Windows-10.0.18362-14.34.31933\Windows Kits\10\Include\10.0.18362.0\shared\bdamedia.h");
        }

        [Benchmark(Baseline = true)]
        public void PreviousLexerParser()
        {
            OnDiskPreprocessorScanner.ParseIncludes(@"C:\ProgramData\UET\SDKs\Windows-10.0.18362-14.34.31933\Windows Kits\10\Include\10.0.18362.0\shared\bdamedia.h");
        }

        [Benchmark]
        public void NewLexerParser()
        {
            var original = _content.AsSpan();
            var range = original;
            var cursor = default(LexerCursor);
            while (LexingHelpers.GetNextDirective(
                ref range,
                in original,
                ref cursor).Found) ;
        }
    }
}