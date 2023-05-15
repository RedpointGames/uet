namespace Redpoint.ProcessExecution
{
    public static class CaptureSpecification
    {
        public static readonly ICaptureSpecification Passthrough = new PassthroughContentStream();

        public static ICaptureSpecification CreateFromDelegates(CaptureSpecificationDelegates captureSpecification)
        {
            return new DelegateCaptureSpecification(captureSpecification);
        }
    }
}
