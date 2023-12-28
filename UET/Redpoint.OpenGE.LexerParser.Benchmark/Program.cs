namespace Redpoint.OpenGE.LexerParser.Benchmark
{
    using BenchmarkDotNet.Running;

    internal class Program
    {
        private static void Main(string[] args)
        {
            //new LexerBenchmarks().ParseFile();
            //BenchmarkRunner.Run<LexerBenchmarks>();
            //BenchmarkRunner.Run<GetNextDirectiveBenchmarks>();
            BenchmarkRunner.Run<SequenceDatabaseBenchmarks>();
        }
    }
}