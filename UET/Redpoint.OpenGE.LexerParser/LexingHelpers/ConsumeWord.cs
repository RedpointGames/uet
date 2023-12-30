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
        internal static ReadOnlySpan<char> ConsumeWord(ref ReadOnlySpan<char> currentRange)
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
                return originalRange.Slice(0, characterCount);
            }
        ConsumeUppercase:
            {
                var afterUppercase = currentRange.IndexOfAnyExceptInRange('A', 'Z');
                if (afterUppercase == -1)
                {
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
                return originalRange.Slice(0, characterCount);
            }
        ConsumeNumeric:
            {
                var afterNumeric = currentRange.IndexOfAnyExceptInRange('0', '9');
                if (afterNumeric == -1)
                {
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
                return originalRange.Slice(0, characterCount);
            }
        ConsumeUnderscore:
            {
                var afterUnderscore = currentRange.IndexOfAnyExcept('_');
                if (afterUnderscore == -1)
                {
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
                return originalRange.Slice(0, characterCount);
            }
        ConsumeNewlineContinuations:
            currentRange.ConsumeNewlineContinuations(ref characterCount);
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
            if (character == '_')
            {
                goto ConsumeUnderscore;
            }
            return originalRange.Slice(0, characterCount);
        }
    }
}
