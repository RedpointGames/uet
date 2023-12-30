namespace Redpoint.OpenGE.LexerParser
{
    using System;
    using System.Runtime.CompilerServices;

    internal static partial class LexingHelpers
    {
        internal ref struct DirectiveSpans
        {
            public static void Assign(
                ref DirectiveSpans result,
                scoped in ReadOnlySpan<char> originalRange,
                scoped in ReadOnlySpan<char> directive)
            {
                result.Found = true;
                result.Directive = directive;
                result.DirectiveStart = (int)(Unsafe.ByteOffset(
                    ref Unsafe.AsRef(in originalRange[0]),
                    ref Unsafe.AsRef(in directive[0])) / sizeof(char));
                result.DirectiveLength = directive.Length;
                result.Arguments = default;
                result.ArgumentsStart = 0;
                result.ArgumentsLength = 0;
            }

            public static void Assign(
                ref DirectiveSpans result,
                scoped in ReadOnlySpan<char> originalRange,
                scoped in ReadOnlySpan<char> directive,
                scoped in ReadOnlySpan<char> arguments)
            {
                result.Found = true;
                result.Directive = directive;
                result.DirectiveStart = (int)(Unsafe.ByteOffset(
                    ref Unsafe.AsRef(in originalRange[0]),
                    ref Unsafe.AsRef(in directive[0])) / sizeof(char));
                result.DirectiveLength = directive.Length;
                result.Arguments = arguments;
                result.ArgumentsStart = (int)(Unsafe.ByteOffset(
                    ref Unsafe.AsRef(in originalRange[0]),
                    ref Unsafe.AsRef(in arguments[0])) / sizeof(char));
                result.ArgumentsLength = arguments.Length;
            }

            public bool Found;
            public ReadOnlySpan<char> Directive;
            public int DirectiveStart;
            public int DirectiveLength;
            public ReadOnlySpan<char> Arguments;
            public int ArgumentsStart;
            public int ArgumentsLength;
        }

#if !DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static void GetNextDirective(
            ReadOnlySpan<char> rangeToScan,
            ReadOnlySpan<char> originalContent,
            ref DirectiveSpans result)
        {
            int skip = 0;
        StartScanning:
            // Find the start of a directive, the potential start of a comment, or a newline.
            var somethingInterestingIndex = rangeToScan.IndexOfAny("#/\r\n");
            if (somethingInterestingIndex == -1)
            {
                // We can't even find a '#', which means there must be no
                // more directives.
                result = default;
                return;
            }

        LookAtSomethingInteresting:
            // What are we looking at?
            ref readonly var somethingInteresting = ref rangeToScan[somethingInterestingIndex];
            switch (somethingInteresting)
            {
                case '#':
                    // A potential directive start. We must make sure that the range
                    // before this is entirely whitespace though.
                    var firstNonWhitespace = IndexOfFirstNonWhitespaceNonCommentCharacter(
                        rangeToScan.Slice(0, somethingInterestingIndex));
                    if (firstNonWhitespace != -1)
                    {
                        // There's non-whitespace before this '#', which means we can't
                        // treat it as a directive. Consume more characters until we get
                        // a true newline terminator.
                        var contentJump = somethingInterestingIndex + 1;
                        rangeToScan = rangeToScan.Slice(contentJump);
                        skip += contentJump;
                        goto ConsumeUntilTrueNewline;
                    }
                    // We've got the hash required for a directive. Find the start of the
                    // directive name by skipping over whitespace.
                    var directiveRemainingRange = rangeToScan.Slice(somethingInterestingIndex + 1);
                    var startOfDirectiveNameIndex = IndexOfFirstNonWhitespaceNonCommentCharacter(directiveRemainingRange);
                    if (startOfDirectiveNameIndex == -1 || startOfDirectiveNameIndex == directiveRemainingRange.Length)
                    {
                        // Content ended before we got the directive name.
                        result = default;
                        return;
                    }
                    ref readonly var startOfDirectiveName = ref directiveRemainingRange[startOfDirectiveNameIndex];
                    // Regardless of the next character, we're either skipping to the start
                    // of the directive name because it's the end of a line and we're going
                    // to restart scanning, or we're going to try and consume a directive name.
                    rangeToScan = directiveRemainingRange.Slice(startOfDirectiveNameIndex + 1);
                    skip += somethingInterestingIndex + 1 + startOfDirectiveNameIndex + 1;
                    if (startOfDirectiveName == '\r' || startOfDirectiveName == '\n')
                    {
                        // We got a newline (without a '\' for line continuation), so this
                        // is an empty comment. This is permitted (but ignored) by the
                        // preprocessor, and is not a directive we can be interested in.
                        goto StartScanning;
                    }
                    goto ConsumeDirectiveName;
                case '/':
                    // This might be the start of a comment. Try to skip past whitespace
                    // and comments from this position until we get to something interesting
                    // again.
                    var sizeOfCommentAndWhitespace = IndexOfFirstNonWhitespaceNonCommentCharacter(
                        rangeToScan.Slice(somethingInterestingIndex));
                    if (sizeOfCommentAndWhitespace == -1 ||
                        sizeOfCommentAndWhitespace == rangeToScan.Length - somethingInterestingIndex)
                    {
                        // There's no more non-whitespace, non-comment content between here
                        // and the end of the range (and the slash was the start of a comment
                        // block), therefore there can be no further directives.
                        result = default;
                        return;
                    }
                    somethingInterestingIndex += sizeOfCommentAndWhitespace;
                    // We got a (potentially multi-line) comment, but we haven't got a true
                    // newline terminator yet so we're still on the same virtual line and
                    // can potentially start looking for the start of a directive again.
                    goto LookAtSomethingInteresting;
                case '\r':
                    if (somethingInterestingIndex == rangeToScan.Length - 1)
                    {
                        // Can't have a \n after this as we're at the end of the range,
                        // which means no more directives.
                        result = default;
                        return;
                    }
                    else if (rangeToScan[somethingInterestingIndex + 1] == '\n')
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
                        var contentJump = somethingInterestingIndex + 1;
                        rangeToScan = rangeToScan.Slice(contentJump);
                        skip += contentJump;
                        goto ConsumeUntilTrueNewline;
                    }
                case '\n':
                    // Is this a true newline terminator? If there is a preceding slash, then
                    // this continues a non-preprocessor line (a line that didn't contain a
                    // '#' or '/' so we never handled it above). If it is a newline continuation
                    // then we can't match a directive after this because it's still just a
                    // C++/C line of code.
                    if (somethingInterestingIndex != 0 &&
                        rangeToScan[somethingInterestingIndex - 1] == '\\')
                    {
                        // Was everything between the start of the range and this
                        // line continuation whitespace? If it was, we can still find a
                        // directive after this.
                        if (IndexOfFirstNonWhitespaceNonCommentCharacter(
                            rangeToScan.Slice(0, somethingInterestingIndex - 1)) == -1)
                        {
                            // A whitespace or comment line continuing into a
                            // potential directive. Restart scanning from the start
                            // of the new line.
                            var newlineJump = somethingInterestingIndex + (somethingInteresting == '\r' ? 2 : 1);
                            rangeToScan = rangeToScan.Slice(newlineJump);
                            skip += newlineJump;
                            goto StartScanning;
                        }
                        else
                        {
                            // This line had non-preprocessor content on it, so we need
                            // to wait for a true newline terminator from the next line
                            // onwards.
                            var newlineJump = somethingInterestingIndex + (somethingInteresting == '\r' ? 2 : 1);
                            rangeToScan = rangeToScan.Slice(newlineJump);
                            skip += newlineJump;
                            goto ConsumeUntilTrueNewline;
                        }
                    }
                    else
                    {
                        // This is a true newline terminator. Skip past it and restart
                        // scanning on the next line.
                        var newlineJump = somethingInterestingIndex + (somethingInteresting == '\r' ? 2 : 1);
                        rangeToScan = rangeToScan.Slice(newlineJump);
                        skip += newlineJump;
                        goto StartScanning;
                    }
                default:
                    throw new InvalidOperationException("Handling character that we should not have stopped on for primary scan.");
            }

        ConsumeUntilTrueNewline:
            // Find the next newline character, then check if it is a continuation.
            var nextNewlineIndex = rangeToScan.IndexOf('\n');
            if (nextNewlineIndex == -1)
            {
                // No more newlines before the end of the content, therefore there
                // can be no more directives either.
                result = default;
                return;
            }
            if (nextNewlineIndex >= 1 && rangeToScan[nextNewlineIndex - 1] == '\\' ||
                nextNewlineIndex >= 2 && rangeToScan[nextNewlineIndex - 1] == '\r' && rangeToScan[nextNewlineIndex - 2] == '\\')
            {
                // Newline continuation, so this isn't a true newline.
                rangeToScan = rangeToScan.Slice(nextNewlineIndex + 1);
                skip += nextNewlineIndex + 1;
                goto ConsumeUntilTrueNewline;
            }
            // We've got a true newline. Jump over it and then restart scanning.
            rangeToScan = rangeToScan.Slice(nextNewlineIndex + 1);
            skip += nextNewlineIndex + 1;
            goto StartScanning;

        ConsumeDirectiveName:
            // Consume alphabetical characters (lower or uppercase) until we don't have
            // any more. Note that jumping to here does not guarantee we're starting
            // with a valid character.
            if (rangeToScan.IsEmpty)
            {
                // We can get here if we get a '#' and then the end of a file.
                result = default;
                return;
            }
            var directiveName = ConsumeWord(ref rangeToScan, ref skip);
            var startOfArguments = IndexOfFirstNonWhitespaceNonCommentCharacter(rangeToScan);
            if (startOfArguments == -1)
            {
                // We got the directive, and then the file ended.

                // @todo: Ok, this is a legit problem. Let's say that we have a
                // directive name that is expressed as "def\{lf}ine" and we've
                // elided the line continuation to make "define" in ConsumeWord.
                // Even if we know that directiveName in the normal case would be safe
                // to escape (because originalContent would still be on the stack and
                // that is what it's lifetime is tied to), the generated directive
                // name from newline continuation eliding would not be.
                //
                // Therefore, I think we have to make ConsumeWord *not* skip
                // newline continuations (we will report them with the continuations
                // in them). When we want to compare those words against various
                // types of preprocessor directives, we'll need to do something like
                // TryConsumeSequence to see if it matches while skipping over
                // the newline continuations.
                //
                // We should then write a helper method that does the logic inside
                // DirectiveSpan:
                //
                // (int)(Unsafe.ByteOffset(
                //    ref Unsafe.AsRef(in originalRange[0]),
                //    ref Unsafe.AsRef(in directive[0])) / sizeof(char));
                //
                // And make sure that DirectiveSpans only returns the position and
                // length for directives and arguments, without encapsulating
                // ReadOnlySpan<char> and without being a `ref struct` itself.
                DirectiveSpans.Assign(
                    ref result,
                    in originalContent,
                    in directiveName);
            }


            throw new NotImplementedException();
#if FALSE
            ref readonly var directiveInitialCharacter = ref rangeToScan[0];
            if ((directiveInitialCharacter >= 'a' && directiveInitialCharacter <= 'z') ||
                (directiveInitialCharacter >= 'A' && directiveInitialCharacter <= 'Z'))
            {
                // We have the start of a directive name. Grab stuff until we have
                // something that can no longer be part of the directive name.
                var directiveRange = rangeToScan;
                var nextNonLowercase = directiveRange.IndexOfAnyExceptInRange('a', 'z');

                //#$*($&%($&%$(*%&
                // @todo: Once we have a non-lowercase, check if it's uppercase, then jump to doing:
                //   directiveRange.IndexOfAnyExceptInRange('A', 'Z');
                // or if it's numeric, jump to doing:
                //   directiveRange.IndexOfAnyExceptInRange('0', '9');
                // or if it's none, then we're done. Each of those calls above
                // then also need to do their own checks to see if we should return
                // to lowercase handling!
            }


            // Skip over any whitespace before a potential '#'.
            int nonWhitespaceIndex = IndexOfFirstNonWhitespaceNonCommentCharacter(rangeToScan);
            if (nonWhitespaceIndex == -1 || nonWhitespaceIndex == rangeToScan.Length)
            {
                // We've got nothing but whitespace.
                return -1;
            }

            // What are we looking at?

            // We have the start of a directive name. Grab stuff until we have
            // something that can no longer be part of the directive name.
            var directiveRange = rangeToScan;
                var nextNonLowercase = directiveRange.IndexOfAnyExceptInRange('a', 'z');

//#$*($&%($&%$(*%&
                // @todo: Once we have a non-lowercase, check if it's uppercase, then jump to doing:
                //   directiveRange.IndexOfAnyExceptInRange('A', 'Z');
                // or if it's numeric, jump to doing:
                //   directiveRange.IndexOfAnyExceptInRange('0', '9');
                // or if it's none, then we're done. Each of those calls above
                // then also need to do their own checks to see if we should return
                // to lowercase handling!
            }
#endif
        }
    }
}
