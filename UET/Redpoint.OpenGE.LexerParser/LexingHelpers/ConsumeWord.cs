namespace Redpoint.OpenGE.LexerParser
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal static partial class LexingHelpers
    {
#if !DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static ReadOnlySpan<char> ConsumeWord(
            ref ReadOnlySpan<char> currentRange, 
            ref int totalCharactersConsumed,
            bool permitNumericStart = false)
        {
            if (currentRange.IsEmpty)
            {
                return default;
            }
            var originalRange = currentRange;
            var characterCount = 0;
            ref readonly var character = ref currentRange[0];
            if (character >= 'a' && character <= 'z')
            {
                goto ConsumeLowercase;
            }
            if (character >= 'A' && character <= 'Z')
            {
                goto ConsumeUppercase;
            }
            if (permitNumericStart && character >= '0' && character <= '9')
            {
                goto ConsumeNumeric;
            }
            if (character == '_')
            {
                goto ConsumeUnderscore;
            }
            if (character == '\\')
            {
                goto ConsumeNewlineContinuations;
            }
            // This isn't the start of a word, so we can't consume anything.
            return default;
        ConsumeLowercase:
            {
                var afterLowercase = currentRange.IndexOfAnyExceptInRange('a', 'z');
                if (afterLowercase == -1)
                {
                    totalCharactersConsumed += characterCount + currentRange.Length;
                    return originalRange.Slice(0, characterCount + currentRange.Length);
                }
                character = ref currentRange[afterLowercase];
                characterCount += afterLowercase;
                currentRange = currentRange.Slice(afterLowercase);
                if (character >= 'A' && character <= 'Z')
                {
                    goto ConsumeUppercase;
                }
                if (character >= '0' && character <= '9')
                {
                    goto ConsumeNumeric;
                }
                if (character == '_')
                {
                    goto ConsumeUnderscore;
                }
                if (character == '\\')
                {
                    goto ConsumeNewlineContinuations;
                }
                totalCharactersConsumed += characterCount;
                return originalRange.Slice(0, characterCount);
            }
        ConsumeUppercase:
            {
                var afterUppercase = currentRange.IndexOfAnyExceptInRange('A', 'Z');
                if (afterUppercase == -1)
                {
                    totalCharactersConsumed += characterCount + currentRange.Length;
                    return originalRange.Slice(0, characterCount + currentRange.Length);
                }
                character = ref currentRange[afterUppercase];
                characterCount += afterUppercase;
                currentRange = currentRange.Slice(afterUppercase);
                if (character >= 'a' && character <= 'z')
                {
                    goto ConsumeLowercase;
                }
                if (character >= '0' && character <= '9')
                {
                    goto ConsumeNumeric;
                }
                if (character == '_')
                {
                    goto ConsumeUnderscore;
                }
                if (character == '\\')
                {
                    goto ConsumeNewlineContinuations;
                }
                totalCharactersConsumed += characterCount;
                return originalRange.Slice(0, characterCount);
            }
        ConsumeNumeric:
            {
                var afterNumeric = currentRange.IndexOfAnyExceptInRange('0', '9');
                if (afterNumeric == -1)
                {
                    totalCharactersConsumed += characterCount + currentRange.Length;
                    return originalRange.Slice(0, characterCount + currentRange.Length);
                }
                character = ref currentRange[afterNumeric];
                characterCount += afterNumeric;
                currentRange = currentRange.Slice(afterNumeric);
                if (character >= 'a' && character <= 'z')
                {
                    goto ConsumeLowercase;
                }
                if (character >= 'A' && character <= 'Z')
                {
                    goto ConsumeUppercase;
                }
                if (character == '_')
                {
                    goto ConsumeUnderscore;
                }
                if (character == '\\')
                {
                    goto ConsumeNewlineContinuations;
                }
                totalCharactersConsumed += characterCount;
                return originalRange.Slice(0, characterCount);
            }
        ConsumeUnderscore:
            {
                var afterUnderscore = currentRange.IndexOfAnyExcept('_');
                if (afterUnderscore == -1)
                {
                    totalCharactersConsumed += characterCount + currentRange.Length;
                    return originalRange.Slice(0, characterCount + currentRange.Length);
                }
                character = ref currentRange[afterUnderscore];
                characterCount += afterUnderscore;
                currentRange = currentRange.Slice(afterUnderscore);
                if (character >= 'a' && character <= 'z')
                {
                    goto ConsumeLowercase;
                }
                if (character >= 'A' && character <= 'Z')
                {
                    goto ConsumeUppercase;
                }
                if (character >= '0' && character <= '9')
                {
                    goto ConsumeNumeric;
                }
                if (character == '\\')
                {
                    goto ConsumeNewlineContinuations;
                }
                totalCharactersConsumed += characterCount;
                return originalRange.Slice(0, characterCount);
            }
        ConsumeNewlineContinuations:
            {
                // @note: We can't avoid an allocation here; we have to strip the
                // newline continuations out of the word and then return a span
                // without the newline continuations.
                //
                // However, the solution is that people shouldn't be using newline
                // continuations mid-word in 2023 because it's really only there for
                // legacy C support.
                var charactersUntilAfterContinuations = 0;
                currentRange.ConsumeNewlineContinuations(ref charactersUntilAfterContinuations);
                if (charactersUntilAfterContinuations == 0)
                {
                    // There were no newline continuations (which is the case if
                    // this slash is for some other kind of escape). Treat this as
                    // a normal termination.
                    totalCharactersConsumed += characterCount;
                    return originalRange.Slice(0, characterCount);
                }
                // Get the part of the word after the newline continuations.
                var contentAfterContinuations = originalRange.Slice(
                    characterCount + charactersUntilAfterContinuations);
                totalCharactersConsumed += characterCount;
                totalCharactersConsumed += charactersUntilAfterContinuations;
                var wordAfterContinuations = ConsumeWord(ref contentAfterContinuations, ref totalCharactersConsumed, true);
                return string.Concat(
                    originalRange.Slice(0, characterCount),
                    wordAfterContinuations);
            }
        }
    }
}
