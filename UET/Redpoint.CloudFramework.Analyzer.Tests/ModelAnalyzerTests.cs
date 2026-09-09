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

        [Fact]
        public async Task TestKey()
        {
            var test =
                """
                #nullable enable

                namespace System.Collections.Generic
                {
                    public interface IReadOnlyList<T>
                    {
                    }

                    public class List<T>
                    {
                    }
                }

                namespace Google.Cloud.Datastore.V1
                {
                    public class Key
                    {
                    }
                }

                namespace Redpoint.CloudFramework.Models
                {
                    public sealed class TypeAttribute : System.Attribute
                    {
                    }

                    public class Model<T> where T : Model<T>
                    {
                    }
                }

                sealed class Model1 : Redpoint.CloudFramework.Models.Model<Model1>
                {
                    [Redpoint.CloudFramework.Models.Type]
                    public Google.Cloud.Datastore.V1.Key? {|#0:Field|} { get; set; }

                    [Redpoint.CloudFramework.Models.Type]
                    public Google.Cloud.Datastore.V1.Key[]? {|#1:FieldArray|} { get; set; }
                
                    [Redpoint.CloudFramework.Models.Type]
                    public System.Collections.Generic.IReadOnlyList<Google.Cloud.Datastore.V1.Key>? {|#2:FieldReadOnlyList|} { get; set; }
                
                    [Redpoint.CloudFramework.Models.Type]
                    public System.Collections.Generic.List<Google.Cloud.Datastore.V1.Key>[]? {|#3:FieldList|} { get; set; }
                }
                """;

            var expected1 = VerifyDiag
                .Diagnostic(CloudFrameworkModelAnalyzer.KeyDiagnosticId)
                .WithLocation(0)
                .WithArguments("Field");
            var expected2 = VerifyDiag
                .Diagnostic(CloudFrameworkModelAnalyzer.KeyDiagnosticId)
                .WithLocation(1)
                .WithArguments("FieldArray");
            var expected3 = VerifyDiag
                .Diagnostic(CloudFrameworkModelAnalyzer.KeyDiagnosticId)
                .WithLocation(2)
                .WithArguments("FieldReadOnlyList");
            var expected4 = VerifyDiag
                .Diagnostic(CloudFrameworkModelAnalyzer.KeyDiagnosticId)
                .WithLocation(3)
                .WithArguments("FieldList");

            await VerifyDiag.VerifyAnalyzerAsync(
                test,
                expected1,
                expected2,
                expected3,
                expected4);
        }
    }
}
