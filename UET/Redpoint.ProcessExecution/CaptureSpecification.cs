namespace Redpoint.ProcessExecution
{
    using System.Text;

    public static class CaptureSpecification
    {
        public static readonly ICaptureSpecification Passthrough = new PassthroughContentStream();

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
    }
}
