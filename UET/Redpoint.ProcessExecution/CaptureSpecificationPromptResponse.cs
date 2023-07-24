namespace Redpoint.ProcessExecution
{
    using System.Text.RegularExpressions;


    public delegate Task CaptureSpecificationPromptResponseResponder(Stream standardInput);

    public class CaptureSpecificationPromptResponse
    {
        internal Dictionary<Regex, CaptureSpecificationPromptResponseResponder> _responses = new Dictionary<Regex, CaptureSpecificationPromptResponseResponder>();

        public void Add(Regex regex, CaptureSpecificationPromptResponseResponder responder)
        {
            _responses.Add(regex, responder);
        }
    }
}
