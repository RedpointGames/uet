namespace Redpoint.OpenGE.LexerParser
{
    using Redpoint.Lexer;
    using System;

    internal static partial class LexingHelpers
    {
#if !DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static DirectiveRange GetNextDirective(
            ref ReadOnlySpan<char> range,
            ref readonly ReadOnlySpan<char> originalContent,
            ref LexerCursor cursor)
        {
            ReadOnlySpan<char> localRange = range;
            LexerCursor localCursor = default;
        StartScanning:
            // Find the start of a directive, the potential start of a comment, or a newline.
            var somethingInterestingIndex = localRange.IndexOfAny("#/\r\n");
            if (somethingInterestingIndex == -1)
            {
                // We can't even find a '#', which means there must be no
                // more directives.
                return default;
            }

        LookAtSomethingInteresting:
            // What are we looking at?
            ref readonly var somethingInteresting = ref localRange[somethingInterestingIndex];
            switch (somethingInteresting)
            {
                case '#':
                    // A potential directive start. We must make sure that the range
                    // before this is entirely whitespace though.
                    var firstNonWhitespace = IndexOfFirstNonWhitespaceNonCommentCharacter(
                        localRange.Slice(0, somethingInterestingIndex));
                    if (firstNonWhitespace != -1)
                    {
                        // There's non-whitespace before this '#', which means we can't
                        // treat it as a directive. Consume more characters until we get
                        // a true newline terminator.
                        var contentJump = somethingInterestingIndex + 1;
                        localRange.Consume(contentJump, ref localCursor);
                        goto ConsumeUntilTrueNewline;
                    }
                    // We've got the hash required for a directive. Find the start of the
                    // directive name by skipping over whitespace.
                    var directiveRemainingRange = localRange.Slice(somethingInterestingIndex + 1);
                    var startOfDirectiveNameIndex = IndexOfFirstNonWhitespaceNonCommentCharacter(directiveRemainingRange);
                    if (startOfDirectiveNameIndex == -1 || startOfDirectiveNameIndex == directiveRemainingRange.Length)
                    {
                        // Content ended before we got the directive name.
                        return default;
                    }
                    ref readonly var startOfDirectiveName = ref directiveRemainingRange[startOfDirectiveNameIndex];
                    // Regardless of the next character, we're either skipping to the start
                    // of the directive name because it's the end of a line and we're going
                    // to restart scanning, or we're going to try and consume a directive name.
                    localRange.Consume(somethingInterestingIndex + 1 + startOfDirectiveNameIndex, ref localCursor);
                    if (startOfDirectiveName == '\r' || startOfDirectiveName == '\n')
                    {
                        // We got a newline (without a '\' for line continuation), so this
                        // is an empty comment. This is permitted (but ignored) by the
                        // preprocessor, and is not a directive we can be interested in.
                        goto StartScanning;
                    }
                    goto ConsumeDirectiveAndArguments;
                case '/':
                    // This might be the start of a comment. Try to skip past whitespace
                    // and comments from this position until we get to something interesting
                    // again.
                    var sizeOfCommentAndWhitespace = IndexOfFirstNonWhitespaceNonCommentCharacter(
                        localRange.Slice(somethingInterestingIndex));
                    if (sizeOfCommentAndWhitespace == -1 ||
                        sizeOfCommentAndWhitespace == localRange.Length - somethingInterestingIndex)
                    {
                        // There's no more non-whitespace, non-comment content between here
                        // and the end of the range (and the slash was the start of a comment
                        // block), therefore there can be no further directives.
                        return default;
                    }
                    somethingInterestingIndex += sizeOfCommentAndWhitespace;
                    // We got a (potentially multi-line) comment, but we haven't got a true
                    // newline terminator yet so we're still on the same virtual line and
                    // can potentially start looking for the start of a directive again.
                    goto LookAtSomethingInteresting;
                case '\r':
                    if (somethingInterestingIndex == localRange.Length - 1)
                    {
                        // Can't have a \n after this as we're at the end of the range,
                        // which means no more directives.
                        return default;
                    }
                    else if (localRange[somethingInterestingIndex + 1] == '\n')
                    {
                        // Shift position by one so we can fallthrough.
                        // @note: We don't update 'somethingInterestingIndex' so that
                        // the \n case can handle newline continuation from the
                        // reference position of the \r.
                        goto case '\n';
                    }
                    else
                    {
                        // Just a random carriage return that is not part of a newline?
                        // This makes it a non-preprocessor line so just consume until
                        // we get to a true newline again.
                        localRange.Consume(somethingInterestingIndex + 1, ref localCursor);
                        goto ConsumeUntilTrueNewline;
                    }
                case '\n':
                    // Is this a true newline terminator? If there is a preceding slash, then
                    // this continues a non-preprocessor line (a line that didn't contain a
                    // '#' or '/' so we never handled it above). If it is a newline continuation
                    // then we can't match a directive after this because it's still just a
                    // C++/C line of code.
                    if (somethingInterestingIndex != 0 &&
                        localRange[somethingInterestingIndex - 1] == '\\')
                    {
                        // Was everything between the start of the range and this
                        // line continuation whitespace? If it was, we can still find a
                        // directive after this.
                        if (IndexOfFirstNonWhitespaceNonCommentCharacter(
                            localRange.Slice(0, somethingInterestingIndex - 1)) == -1)
                        {
                            // A whitespace or comment line continuing into a
                            // potential directive. Restart scanning from the start
                            // of the new line.
                            localRange.Consume(
                                somethingInterestingIndex + (somethingInteresting == '\r' ? 2 : 1),
                                ref localCursor);
                            goto StartScanning;
                        }
                        else
                        {
                            // This line had non-preprocessor content on it, so we need
                            // to wait for a true newline terminator from the next line
                            // onwards.
                            localRange.Consume(
                                somethingInterestingIndex + (somethingInteresting == '\r' ? 2 : 1),
                                ref localCursor);
                            goto ConsumeUntilTrueNewline;
                        }
                    }
                    else
                    {
                        // This is a true newline terminator. Skip past it and restart
                        // scanning on the next line.
                        localRange.Consume(
                            somethingInterestingIndex + (somethingInteresting == '\r' ? 2 : 1),
                            ref localCursor);
                        goto StartScanning;
                    }
                default:
                    throw new InvalidOperationException("Handling character that we should not have stopped on for primary scan.");
            }

        ConsumeUntilTrueNewline:
            // Find the next newline character, then check if it is a continuation.
            var nextNewlineIndex = localRange.IndexOf('\n');
            if (nextNewlineIndex == -1)
            {
                // No more newlines before the end of the content, therefore there
                // can be no more directives either.
                return default;
            }
            if (nextNewlineIndex >= 1 && localRange[nextNewlineIndex - 1] == '\\' ||
                nextNewlineIndex >= 2 && localRange[nextNewlineIndex - 1] == '\r' && localRange[nextNewlineIndex - 2] == '\\')
            {
                // Newline continuation, so this isn't a true newline.
                localRange.Consume(nextNewlineIndex + 1, ref localCursor);
                goto ConsumeUntilTrueNewline;
            }
            // We've got a true newline. Jump over it and then restart scanning.
            localRange.Consume(nextNewlineIndex + 1, ref localCursor);
            goto StartScanning;

        ConsumeDirectiveAndArguments:
            // Consume alphabetical characters (lower or uppercase) until we don't have
            // any more. Note that jumping to here does not guarantee we're starting
            // with a valid character.
            if (localRange.IsEmpty)
            {
                // We can get here if we get a '#' and then the end of a file.
                return default;
            }
            var directiveName = ConsumeWord(ref localRange, ref localCursor);
            var startOfArguments = IndexOfFirstNonWhitespaceNonCommentCharacter(localRange);
            if (startOfArguments == -1)
            {
                // We got the directive, and then the file ended.
                localRange.Consume(localRange.Length, ref localCursor);
                range = localRange;
                cursor = localCursor;
                return new DirectiveRange
                {
                    Found = true,
                    Directive = directiveName.Span.RelativeRangeWithin(originalContent),
                    DirectiveHasNewlineContinuations = directiveName.ContainsNewlineContinuations,
                };
            }

            // Arguments end after the first non-continuation newline.
            localRange.Consume(startOfArguments, ref localCursor);
            var originalConsumed = localCursor.CharactersConsumed;
            var arguments = localRange;
        ConsumeMoreArguments:
            var nextNewline = localRange.IndexOf('\n');
            if (nextNewline == -1 || /* No newline character */
                nextNewline + 1 >= localRange.Length /* Newline character then end of file */)
            {
                // We got some arguments, and then the file ended.
                localRange.Consume(localRange.Length, ref localCursor);
                range = localRange;
                cursor = localCursor;
                return new DirectiveRange
                {
                    Found = true,
                    Directive = directiveName.Span.RelativeRangeWithin(originalContent),
                    DirectiveHasNewlineContinuations = directiveName.ContainsNewlineContinuations,
                    Arguments = arguments.RelativeRangeWithin(originalContent),
                };
            }
            if ((nextNewline > 0 && localRange[nextNewline - 1] == '\\') ||
                (nextNewline > 1 && localRange[nextNewline - 1] == '\r' && localRange[nextNewline - 2] == '\\'))
            {
                // This newline belonged to a newline continuation. Consume
                // that line and continue scanning from the start of this line.
                localRange.Consume(nextNewline + 1, ref localCursor);
                goto ConsumeMoreArguments;
            }
            // This newline terminates the directive.
            localRange.Consume(nextNewline + 1, ref localCursor);
            range = localRange;
            cursor = localCursor;
            return new DirectiveRange
            {
                Found = true,
                Directive = directiveName.Span.RelativeRangeWithin(originalContent),
                DirectiveHasNewlineContinuations = directiveName.ContainsNewlineContinuations,
                Arguments = arguments
                    .Slice(0, localCursor.CharactersConsumed - originalConsumed - 1)
                    .RelativeRangeWithin(originalContent),
            };
        }
    }
}
