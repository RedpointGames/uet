namespace Redpoint.OpenGE.LexerParser.Benchmark
{
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Diagnostics.Windows.Configs;
    using BenchmarkDotNet.Running;
    using Redpoint.Lexer;

    [MemoryDiagnoser]
    [EtwProfiler]
    public class LexerBenchmarks
    {
        private readonly string _content;

        public LexerBenchmarks()
        {
            _content = File.ReadAllText(@"C:\ProgramData\UET\SDKs\Windows-10.0.18362-14.34.31933\Windows Kits\10\Include\10.0.18362.0\shared\bdamedia.h");
        }

        [Benchmark]
        public void ParseFile()
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

    internal class Program
    {
        private static void Main(string[] args)
        {
            BenchmarkRunner.Run<LexerBenchmarks>();
        }
    }
}