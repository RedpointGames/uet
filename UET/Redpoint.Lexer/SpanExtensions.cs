namespace Redpoint.Lexer
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Provides extension methods for <see cref="ReadOnlySpan{T}"/> that assist with lexing.
    /// </summary>
    public static class SpanExtensions
    {
        /// <summary>
        /// Consume the specified number of characters from the span, updating both the span
        /// and a "total consumed" count which is useful for lexing.
        /// </summary>
        /// <param name="span">The reference to the span, which will be updated to point at the new slice.</param>
        /// <param name="consume">The number of characters to consume from the span. The span must have at least this many characters left, or <see cref="ReadOnlySpan{T}.Slice(int)"/> will throw an exception.</param>
        /// <param name="cursor">The reference to the cursor that will be updated as part of consuming these characters.</param>
        /// <returns>The value of <paramref name="consume"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Consume(
            this ref ReadOnlySpan<char> span,
            int consume,
            ref LexerCursor cursor)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(consume, span.Length, nameof(consume));
            if (consume == 0)
            {
                return 0;
            }
            else if (consume == 1)
            {
                cursor.NewlinesConsumed += span[0] == '\n' ? 1 : 0;
            }
            else if (consume == 2)
            {
                cursor.NewlinesConsumed +=
                    (span[0] == '\n' ? 1 : 0) +
                    (span[1] == '\n' ? 1 : 0);
            }
            else
            {
                cursor.NewlinesConsumed += span
                    .Slice(0, consume)
                    .Count('\n');
            }
            span = span.Slice(consume);
            cursor.CharactersConsumed += consume;
            return consume;
        }

        /// <summary>
        /// Consume all of the "\{lf}" and "\{cr}{lf}" sequences until we've gone past them all. These are
        /// the only characters permitted; all other characters including whitespace will not be consumed.
        /// This method allows the lexer to quickly skip over line continuations without having to handle
        /// them explicitly everywhere.
        /// </summary>
        /// <param name="span">The reference to the span, which will be moved beyond the newline continuations. The span must start with the "\" character in order for sequences to be consumed.</param>
        /// <param name="cursor">The reference to the cursor that will be updated as part of consuming these characters.</param>
        /// <returns>The number of characters consumed by this call.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ConsumeNewlineContinuations(
            this ref ReadOnlySpan<char> span,
            ref LexerCursor cursor)
        {
            var consumed = 0;
        AttemptConsume:
            if (span.Length < 2 ||
                span[0] != '\\')
            {
                return consumed;
            }
            if (span[1] == '\n')
            {
                consumed += span.Consume(2, ref cursor);
                // Attempt to consume again in case there are multiple newline
                // continuations.
                goto AttemptConsume;
            }
            if (span.Length >= 3 &&
                span[1] == '\r' &&
                span[2] == '\n')
            {
                consumed += span.Consume(3, ref cursor);
                // Attempt to consume again in case there are multiple newline
                // continuations.
                goto AttemptConsume;
            }
            return consumed;
        }

        /// <summary>
        /// Given a position in a span, runs backwards over newline continuations to find the first
        /// character that isn't part of a newline continuation. That is, given a span that contains "abc\{lf}\{cr}{lf}XYZ" and a position pointing to "X", returns the position of "c".
        /// 
        /// This method should be rarely used as it is only required for reverse searching. Forward
        /// searches should use <see cref="ConsumeNewlineContinuations(ref ReadOnlySpan{char}, ref LexerCursor)"/> instead.
        /// </summary>
        /// <param name="span">The span to search inside.</param>
        /// <param name="startPosition">The start position to search at. The characters immediately prior to this must be newline continuations.</param>
        /// <returns>The index of the first character before potential newline continuations, or -1 if there is no non-newline-continuation character in the span prior to the provided index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOfAnyBeforeNewlineContinuations(
            this ref readonly ReadOnlySpan<char> span,
            int startPosition)
        {
            var position = startPosition - 1;
        CheckPosition:
            if (position < 0 || span[position] != '\n')
            {
                // The starting position doesn't have a newline continuation before it,
                // or the starting position is 0 in the span (the math of `position` means
                // we'll return -1 for that case).
                return position;
            }
            // We have a newline character. Check if we have a slash before this.
            if (position == 0)
            {
                // Can't have a slash or \r before this. This means the newline itself
                // is not a continuation.
                return position;
            }
            ref readonly var beforeChar = ref span[position - 1];
            if (beforeChar == '\\')
            {
                // We have a slash before this. It is a newline continuation.
                position -= 2;
                goto CheckPosition;
            }
            if (beforeChar == '\r' &&
                position >= 2 &&
                span[position - 2] == '\\')
            {
                // We have a slash and carriage return before this. It is a
                // newline continuation.
                position -= 3;
                goto CheckPosition;
            }
            // We have something else before this, which means we don't have a
            // newline continuation.
            return position;
        }

        /// <summary>
        /// Attempt to consume the specified sequence from the span, automatically handling newline
        /// continuations that might appear in the middle of the sequence. The reference to the span
        /// is only updated if the sequence is successfully consumed, and this method only attempts
        /// to consume the sequence once.
        /// </summary>
        /// <param name="span">The reference to the span that might have the sequence at the start of it.</param>
        /// <param name="sequence">The sequence to attempt to consume.</param>
        /// <param name="cursor">The reference to the cursor that will be updated as part of consuming these characters.</param>
        /// <param name="containsNewlineContinuations">This value will be set to true if the span contained newline continuations over the sequence.</param>
        /// <param name="definitelyNotStartingWithNewlineContinuation">If true, the call to <see cref="ConsumeNewlineContinuations(ref ReadOnlySpan{char}, ref LexerCursor)"/> will be skipped for the first character.</param>
        /// <returns>If true, <paramref name="span"/> has been updated to skip over the sequence and <paramref name="cursor"/> has been updated with the total number of characters (including newline continuations) skipped. If false, neither is modified.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryConsumeSequence(
            this ref ReadOnlySpan<char> span,
            ReadOnlySpan<char> sequence,
            ref LexerCursor cursor,
            ref bool containsNewlineContinuations,
            bool definitelyNotStartingWithNewlineContinuation = false)
        {
            if (sequence.IsEmpty)
            {
                // Nothing to consume anyway.
                return true;
            }
            if (span.Length < sequence.Length)
            {
                // The span is not long enough to hold even the shortest version of the sequence.
                return false;
            }
            if (span[0] == sequence[0] &&
                span.StartsWith(sequence, StringComparison.Ordinal))
            {
                // We did a quick "starts with" check to see if the literal sequence (without
                // newline continuations) was found
                span.Consume(sequence.Length, ref cursor);
                return true;
            }
            var workingSpan = span;
            LexerCursor workingSpanConsumed = default;
            var sequencePosition = 0;
        StepForward:
            if (!definitelyNotStartingWithNewlineContinuation || sequencePosition > 0)
            {
                if (workingSpan.ConsumeNewlineContinuations(ref workingSpanConsumed) > 0)
                {
                    containsNewlineContinuations = true;
                }
            }
            if (workingSpan[0] != sequence[sequencePosition])
            {
                // Character mismatch, can not consume sequence.
                return false;
            }
            sequencePosition++;
            workingSpan.Consume(1, ref workingSpanConsumed);
            if (sequencePosition == sequence.Length)
            {
                // We've matched the sequence.
                span = workingSpan;
                cursor.Add(ref workingSpanConsumed);
                return true;
            }
            goto StepForward;
        }

        /// <summary>
        /// Computes the range of the current span within another one. The child span
        /// must have originally been generated by calling <see cref="ReadOnlySpan{T}.Slice(int)"/>
        /// on the parent (or some intermediate span), since this method works off the
        /// relative memory addresses.
        /// </summary>
        /// <param name="child">The child span.</param>
        /// <param name="parent">The parent span that contains the child span.</param>
        /// <returns>The range of the child span within the parent span.</returns>
#if !DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static LexerRange RelativeRangeWithin(
            this ReadOnlySpan<char> child,
            ReadOnlySpan<char> parent)
        {
            if (child.IsEmpty)
            {
                return default;
            }
            ref char parentChar = ref Unsafe.AsRef(in parent[0]);
            ref char childChar = ref Unsafe.AsRef(in child[0]);
            var byteOffset = Unsafe.ByteOffset(in parentChar, in childChar);
            return new LexerRange
            {
                Start = (int)(byteOffset / sizeof(char)),
                Length = child.Length,
            };
        }
    }
}
