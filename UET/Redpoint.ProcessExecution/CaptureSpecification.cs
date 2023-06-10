namespace Redpoint.ProcessExecution
{
    using System.Text;

    public static class CaptureSpecification
    {
        /// <summary>
        /// Does not capture standard output, error or input and allows it to be passed through to the terminal directly.
        /// </summary>
        public static readonly ICaptureSpecification Passthrough = new PassthroughCaptureSpecification();

        /// <summary>
        /// Captures standard output and standard error and drops all content. Does not redirect standard input.
        /// </summary>
        public static readonly ICaptureSpecification Silence = new SilenceCaptureSpecification();

        /// <summary>
        /// Captures standard output and standard error and sanitizes it so that it's safe to emit to the terminal. Does not redirect standard input.
        /// </summary>
        public static readonly ICaptureSpecification Sanitized = new SanitizedCaptureSpecification();

        public static ICaptureSpecification CreateFromDelegates(CaptureSpecificationDelegates captureSpecification)
        {
            return new DelegateCaptureSpecification(captureSpecification);
        }

        public static ICaptureSpecification CreateFromStdoutStringBuilder(StringBuilder stdout)
        {
            return new DelegateCaptureSpecification(new CaptureSpecificationDelegates
            {
                ReceiveStdout = (line) =>
                {
                    stdout.AppendLine(line);
                    return false;
                }
            });
        }

        /// <summary>
        /// Captures standard output as-is and sends it to the string builder. Sanitizes standard error so that it's safe to emit to the terminal. Does not redirect standard input.
        /// </summary>
        public static ICaptureSpecification CreateFromSanitizedStdoutStringBuilder(StringBuilder stdout)
        {
            return new SanitizedStringBuilderCaptureSpecification(stdout);
        }
    }
}
