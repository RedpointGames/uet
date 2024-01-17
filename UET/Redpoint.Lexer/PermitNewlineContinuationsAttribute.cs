namespace Redpoint.Lexer
{
    /// <summary>
    /// Some languages like C and C++ permit newline continuations
    /// in the middle of identifiers and keywords, such that 
    /// "def\{lf}\{cr}{lf}ine" is the equivalent of "define". When 
    /// this attribute is applied to a method, the generated lexer
    /// function will handle newline continuations anywhere in the
    /// scanned content. In this case, the method must return
    /// <see cref="LexerFragment"/> instead of <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class PermitNewlineContinuationsAttribute : Attribute
    {
    }
}
