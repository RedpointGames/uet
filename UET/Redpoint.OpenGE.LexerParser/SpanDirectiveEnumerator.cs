namespace Redpoint.OpenGE.LexerParser
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
