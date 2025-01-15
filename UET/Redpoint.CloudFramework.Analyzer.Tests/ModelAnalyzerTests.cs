namespace Redpoint.CloudFramework.Analyzer.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    using VerifyDiag = Verifiers.CSharpAnalyzerVerifier<
        CloudFrameworkModelAnalyzer>;

    //using VerifyCodeFix = Verifiers.CSharpCodeFixVerifier<
    //    CloudFrameworkModelAnalyzer,
    //    CloudFrameworkModelAnalyzerCodeFixProvider>;

    public class ModelAnalyzerTests
    {
        [Fact]
        public async Task TestEmpty()
        {
            var test = string.Empty;

            await VerifyDiag.VerifyAnalyzerAsync(test);
            // await VerifyCodeFix.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task TestInherit()
        {
            var test =
                """
                namespace Redpoint.CloudFramework.Models
                {
                    public class Model<T> where T : Model<T>
                    {
                    }
                }

                sealed class Model1 : Redpoint.CloudFramework.Models.Model<Model1>
                {
                }
                
                sealed class {|#0:Model2|} : Redpoint.CloudFramework.Models.Model<Model1>
                {
                }
                """;

            var expected = VerifyDiag
                .Diagnostic(CloudFrameworkModelAnalyzer.InheritDiagnosticId)
                .WithLocation(0)
                .WithArguments("Model2");

            await VerifyDiag.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task TestSealed()
        {
            var test =
                """
                namespace Redpoint.CloudFramework.Models
                {
                    public class Model<T> where T : Model<T>
                    {
                    }
                }

                sealed class Model1 : Redpoint.CloudFramework.Models.Model<Model1>
                {
                }
                
                class {|#0:Model2|} : Redpoint.CloudFramework.Models.Model<Model2>
                {
                }
                """;

            var expected = VerifyDiag
                .Diagnostic(CloudFrameworkModelAnalyzer.SealedDiagnosticId)
                .WithLocation(0)
                .WithArguments("Model2");

            await VerifyDiag.VerifyAnalyzerAsync(test, expected);
        }
    }
}
