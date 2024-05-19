namespace Redpoint.CppPreprocessor.Lexing
{
    using Redpoint.Lexer;
    using System;
    using System.Runtime.CompilerServices;

    public static partial class LexingHelpers
    {
        /// <summary>
        /// Find the index within <paramref name="rangeToScan"/> of the first non-whitespace, non-comment character.
        /// </summary>
        /// <param name="rangeToScan">The span to scan.</param>
        /// <returns>The position within the span of the first non-whitespace, non-comment character, or -1 if no character was found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOfFirstNonWhitespaceNonCommentCharacter(ReadOnlySpan<char> rangeToScan)
        {
            LexerCursor skip = default;
        StartScanning:
            // If it's empty, then it can't contain anything not allowed.
            if (rangeToScan.IsEmpty)
            {
                return -1;
            }

            var firstNonWhitespace = rangeToScan.IndexOfAnyExcept(' ', '\t');
            if (firstNonWhitespace == -1)
            {
                // We only contain whitespace (no C++ comment either).
                return -1;
            }
            rangeToScan.ConsumeUtf16(firstNonWhitespace, ref skip);

            if (rangeToScan.ConsumeNewlineContinuationsUtf16(ref skip) != 0)
            {
                // We skipped over at least one newline continuation, go back
                // to whitespace scanning.
                goto StartScanning;
            }

            // We've got something other than whitespace or a newline continuation.
            // The only thing permitted is the start of a multi-line comment, so try
            // to consume it.
            var startSequenceContainsNewlines = false;
            if (rangeToScan.TryConsumeSequenceUtf16("/*", ref skip, ref startSequenceContainsNewlines, true))
            {
                // Start trying to find the end of the multi-line comment.
                goto SlashScan;
            }
            if (rangeToScan.TryConsumeSequenceUtf16("//", ref skip, ref startSequenceContainsNewlines, true))
            {
                // Go past the end of this line. It can't be continued.
                var endOfLine = rangeToScan.IndexOf('\n');
                return skip.CharactersConsumed + Math.Min(endOfLine + 1, rangeToScan.Length);
            }

            // Not the start of a multi-line comment, which means this is the end.
            return skip.CharactersConsumed;
        SlashScan:
            var nextSlash = rangeToScan.IndexOf('/');
            if (nextSlash == -1)
            {
                // No concluding '/' found, which means no matter what
                // we can't have a '*' that would start the comment
                // terminator.
                return skip.CharactersConsumed + rangeToScan.Length;
            }
            if (nextSlash == 0)
            {
                // The slash was the first character. Skip to the first
                // star we find (instead of checking for the next slash;
                // this avoids worst case sequences like '/////').
                goto StarScan;
            }

            var previousChar = rangeToScan.IndexOfAnyBeforeNewlineContinuationsUtf16(nextSlash);
            if (previousChar == -1 || rangeToScan[previousChar] != '*')
            {
                // The character before the slash was not a '*'. Skip to
                // the first star we find (instead of checking for the
                // next slash; this avoids worst case sequences like '/////').
                rangeToScan.ConsumeUtf16(nextSlash + 1, ref skip);
                goto StarScan;
            }

            // We're concluding a multi-line comment. After the end of
            // the multi-line comment, we then need to continue checking
            // for whitespace or more multi-line comments.
            rangeToScan.ConsumeUtf16(nextSlash + 1, ref skip);
            goto StartScanning;
        StarScan:
            var nextStar = rangeToScan.IndexOf('*');
            if (nextStar == -1 || nextStar == rangeToScan.Length - 1)
            {
                // No concluding '*' found or it is the last character,
                // which means no matter what we can't have the slash
                // necessary to end the comment terminator.
                return skip.CharactersConsumed + rangeToScan.Length;
            }
            var afterStarSpan = rangeToScan.Slice(nextStar + 1);
            LexerCursor afterStarNewlineContinuationsConsumed = default;
            afterStarSpan.ConsumeNewlineContinuationsUtf16(ref afterStarNewlineContinuationsConsumed);
            if (afterStarSpan[0] != '/')
            {
                // No slash after the star. Skip to the first slash we
                // find (instead of checking for the next star;
                // this avoids worst case sequences like '******/').
                rangeToScan.ConsumeUtf16(nextStar + 1, ref skip);
                goto SlashScan;
            }

            // We're concluding a multi-line comment. After the end of
            // the multi-line comment, we then need to continue checking
            // for whitespace or more multi-line comments.
            rangeToScan.ConsumeUtf16(
                nextStar + 1 + afterStarNewlineContinuationsConsumed.CharactersConsumed + 1,
                ref skip);
            goto StartScanning;
        }
    }
}
