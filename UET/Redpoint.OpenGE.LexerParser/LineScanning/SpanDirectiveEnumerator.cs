namespace Redpoint.OpenGE.LexerParser.LineScanning
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    public ref struct Directive
    {
        public required uint LineNumber { get; set; }
        public required ReadOnlySpan<char> DirectiveChars { get; set; }
        public required ReadOnlySpan<char> ArgumentsChars { get; set; }
    }

    public ref struct SpanDirectiveEnumerator
    {
        private enum EnumeratorState
        {
            StartOfLine,
            Done,
        }

        private ReadOnlySpan<char> _remaining;
        //private int _position;
        private Directive _current;
        private EnumeratorState _state;

        public SpanDirectiveEnumerator(ReadOnlySpan<char> source)
        {
            _remaining = source;
            //  _position = 0;
            _current = default;
            _state = EnumeratorState.StartOfLine;
        }

        public Directive Current => _current;

        //private const string _scanFromStartOfLine = "#/\\\n";
        //private const string _scanForWhitespaceOrComment = " \t/";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfFirstNonWhitespaceNonCommentCharacter(ReadOnlySpan<char> rangeToScan)
        {
            int skip = 0;
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

            // Is the first non-whitespace character the start of a comment, and
            // the remaining content has enough space to store a comment?
            ref readonly char firstChar = ref rangeToScan[firstNonWhitespace];
            if (firstChar != '/')
            {
                // Not the start of '/*'.
                return skip + firstNonWhitespace;
            }
            rangeToScan = rangeToScan.Slice(firstNonWhitespace);
            skip += firstNonWhitespace;
            if (rangeToScan.Length < 4)
            {
                // Not big enough to store '/**/'.
                return skip + rangeToScan.Length;
            }
            ref readonly char secondChar = ref rangeToScan[1];
            if (secondChar != '*')
            {
                // We're not starting a comment which means the slash is
                // non-whitespace content in the range.
                return skip;
            }
            rangeToScan = rangeToScan.Slice(2);
            skip += 2;
        SlashScan:
            var nextSlash = rangeToScan.IndexOf('/');
            if (nextSlash == -1)
            {
                // No concluding '/' found, which means no matter what
                // we can't have a '*' that would start the comment
                // terminator.
                return skip + rangeToScan.Length;
            }
            if (nextSlash == 0 || rangeToScan[nextSlash - 1] != '*')
            {
                // The slash was the first character. Skip to the first
                // star we find (instead of checking for the next slash;
                // this avoids worst case sequences like '/////').
                if (nextSlash != 0)
                {
                    rangeToScan = rangeToScan.Slice(nextSlash + 1);
                    skip += nextSlash + 1;
                }
                goto StarScan;
            }
            // We're concluding a multi-line comment. After the end of
            // the multi-line comment, we then need to continue checking
            // for whitespace or more multi-line comments.
            rangeToScan = rangeToScan.Slice(nextSlash + 1);
            skip += nextSlash + 1;
            goto StartScanning;
        StarScan:
            var nextStar = rangeToScan.IndexOf('*');
            if (nextStar == -1 || nextStar == rangeToScan.Length - 1)
            {
                // No concluding '*' found or it is the last character,
                // which means no matter what we can't have the slash
                // necessary to end the comment terminator.
                return skip + rangeToScan.Length;
            }
            if (rangeToScan[nextStar + 1] != '/')
            {
                // No slash after the star. Skip to the first slash we
                // find (instead of checking for the next star;
                // this avoids worst case sequences like '******/').
                rangeToScan = rangeToScan.Slice(nextStar + 1);
                skip += nextStar + 1;
                goto SlashScan;
            }
            // We're concluding a multi-line comment. After the end of
            // the multi-line comment, we then need to continue checking
            // for whitespace or more multi-line comments.
            rangeToScan = rangeToScan.Slice(nextStar + 2);
            skip += nextStar + 2;
            goto StartScanning;
        }

        internal ref struct DirectiveSpans
        {
            public DirectiveSpans(
                ReadOnlySpan<char> originalRange,
                ReadOnlySpan<char> directive,
                ReadOnlySpan<char> arguments)
            {
                Found = true;
                Directive = directive;
                DirectiveStart = (int)(Unsafe.ByteOffset(
                    ref Unsafe.AsRef(originalRange[0]),
                    ref Unsafe.AsRef(directive[0])) / sizeof(char));
                DirectiveLength = directive.Length;
                Arguments = arguments;
                ArgumentsStart = (int)(Unsafe.ByteOffset(
                    ref Unsafe.AsRef(originalRange[0]),
                    ref Unsafe.AsRef(arguments[0])) / sizeof(char));
                ArgumentsLength = arguments.Length;
            }

            public bool Found;
            public ReadOnlySpan<char> Directive;
            public int DirectiveStart;
            public int DirectiveLength;
            public ReadOnlySpan<char> Arguments;
            public int ArgumentsStart;
            public int ArgumentsLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static DirectiveSpans GetNextDirective(ReadOnlySpan<char> rangeToScan)
        {
            int skip = 0;
        StartScanning:
            // Find the start of a directive, the potential start of a comment, or a newline.
            var somethingInterestingIndex = rangeToScan.IndexOfAny("#/\r\n");
            if (somethingInterestingIndex == -1)
            {
                // We can't even find a '#', which means there must be no
                // more directives.
                return default;
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
                        return default;
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
                        return default;
                    }
                    somethingInterestingIndex += sizeOfCommentAndWhitespace;
                    // We got a (potentially multi-line) comment, but we haven't got a true
                    // newline terminator yet so we're still on the same virtual line and
                    // can potentially start looking for the start of a directive again.
                    goto LookAtSomethingInteresting;
                case '\r';
                    if (somethingInterestingIndex == rangeToScan.Length - 1)
                    {
                        // Can't have a \n after this as we're at the end of the range,
                        // which means no more directives.
                        return default;
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
                return default;
            }
            if ((nextNewlineIndex >= 1 && rangeToScan[nextNewlineIndex - 1] == '\\') ||
                (nextNewlineIndex >= 2 && rangeToScan[nextNewlineIndex - 1] == '\r' && rangeToScan[nextNewlineIndex - 2] == '\\'))
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
                return default;
            }
            ref readonly var directiveInitialCharacter = ref rangeToScan[0];
            if ((directiveInitialCharacter >= 'a' && directiveInitialCharacter <= 'z') ||
                (directiveInitialCharacter >= 'A' && directiveInitialCharacter <= 'Z'))
            {
                // We have the start of a directive name. Grab stuff until we have
                // something that can no longer be part of the directive name.
                var directiveRange = rangeToScan;
                var nextNonLowercase = directiveRange.IndexOfAnyExceptInRange('a', 'z');

                #$*($&%($&%$(*%&
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

            
        }

            public bool MoveNext()
        {
            // Based on the enumerator state, continue at the point where
            // we left off.
            switch (_state)
            {
                case EnumeratorState.StartOfLine:
                    goto HandleStartOfLine;
                case EnumeratorState.Done:
                default:
                    goto ReachedEnd;
            }

        // We're at the start of the content, or the start of a brand
        // new line.
        HandleStartOfLine:
            var index = _remaining.IndexOfAny("#/\\\n");
            if (index == -1)
            {
                goto ReachedEnd;
            }
        /*
        switch (_remaining[index])
        {
            case '#':
                // A potential directive, but we might have non-whitespace characters
                // before this.
                if (index > 0)
                {
                    _remaining.Slice(0, index - 1).IndexOfAnyExcept(_scanForWhitespaceOrComment)

                    // If we have non-whitespace between 
                }
            case '/':
            case '\\':
            case '\n':
        }
        */

        // When we're certain we've reached the end of the content
        // and can produce no more results.
        ReachedEnd:
            _state = EnumeratorState.Done;
            _current = default;
            return false;
        }
    }
}
