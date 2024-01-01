namespace Redpoint.OpenGE.LexerParser.Tests
{
    using Redpoint.OpenGE.LexerParser.LineParsing;
    using Redpoint.OpenGE.LexerParser.LineScanning;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class ScannerTests
    {
        [Fact(Skip = "Lindex is not used.")]
        public void Test()
        {
            using var file = new ScannerOpenFile("");

            var lindexDocument = Scanner.GenerateLindexDocument(file);
            var lines = new StringBuilder();
            for (int i = 0; i < 300; i++)
            {
                ref var line = ref ScanningLindexDocument.Get(ref lindexDocument, i);
                if (line.Offset == 0 && i != 0)
                { 
                    break;
                }
                lines.AppendLine($"{line.Offset} - {line.Length}");
            }
            var result = lines.ToString();
            Assert.NotEmpty(result);
        }
    }
}
