namespace Redpoint.UET.BuildPipeline.Executors
{
    using Microsoft.Extensions.Logging;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    public class LoggerBasedBuildExecutionEvents : IBuildExecutionEvents
    {
        private readonly ILogger _logger;
        private static readonly Regex _warningRegex = new Regex("([^a-zA-Z0-9]|^)([Ww]arning)([^a-zA-Z0-9]|$)");
        private static readonly Regex _errorRegex = new Regex("([^a-zA-Z0-9]|^)([Ee]rror)([^a-zA-Z0-9]|$)");
        private static readonly Regex _successfulRegex = new Regex("([^a-zA-Z0-9]|^)([Ss][Uu][Cc][Cc][Ee][Ss][Ss][Ff]?[Uu]?[Ll]?)([^a-zA-Z0-9]|$)");
        private static readonly Regex _uetInfoRegex = new Regex("([0-9][0-9]:[0-9][0-9]:[0-9][0-9] \\[)(info?)(\\])");
        private static readonly Regex _uetWarnRegex = new Regex("([0-9][0-9]:[0-9][0-9]:[0-9][0-9] \\[)(warn?)(\\])");
        private static readonly Regex _uetFailRegex = new Regex("([0-9][0-9]:[0-9][0-9]:[0-9][0-9] \\[)(fail?)(\\])");

        public LoggerBasedBuildExecutionEvents(ILogger logger)
        {
            _logger = logger;
        }

        public Task OnNodeFinished(string nodeName, BuildResultStatus resultStatus)
        {
            switch (resultStatus)
            {
                case BuildResultStatus.Success:
                    _logger.LogInformation($"[{nodeName}] \x001B[32mPassed\x001B[0m");
                    break;
                case BuildResultStatus.Failed:
                    _logger.LogInformation($"[{nodeName}] \x001B[31mFailed\x001B[0m");
                    break;
                case BuildResultStatus.NotRun:
                    _logger.LogInformation($"[{nodeName}] \x001B[36mNot Run\x001B[0m");
                    break;
            }
            return Task.CompletedTask;
        }

        public Task OnNodeOutputReceived(string nodeName, string[] lines)
        {
            foreach (var line in lines)
            {
                var highlightedLine = _warningRegex.Replace(line, m => $"{m.Groups[1].Value}\u001b[33m{m.Groups[2].Value}\u001b[0m{m.Groups[3].Value}");
                highlightedLine = _errorRegex.Replace(highlightedLine, m => $"{m.Groups[1].Value}\u001b[31m{m.Groups[2].Value}\u001b[0m{m.Groups[3].Value}");
                highlightedLine = _successfulRegex.Replace(highlightedLine, m => $"{m.Groups[1].Value}\u001b[32m{m.Groups[2].Value}\u001b[0m{m.Groups[3].Value}");
                highlightedLine = _uetInfoRegex.Replace(highlightedLine, m => $"{m.Groups[1].Value}\u001b[32m{m.Groups[2].Value}\u001b[0m{m.Groups[3].Value}");
                highlightedLine = _uetWarnRegex.Replace(highlightedLine, m => $"{m.Groups[1].Value}\u001b[33m{m.Groups[2].Value}\u001b[0m{m.Groups[3].Value}");
                highlightedLine = _uetFailRegex.Replace(highlightedLine, m => $"{m.Groups[1].Value}\u001b[31m{m.Groups[2].Value}\u001b[0m{m.Groups[3].Value}");
                _logger.LogInformation($"[{nodeName}] {highlightedLine}");
            }
            return Task.CompletedTask;
        }

        public Task OnNodeStarted(string nodeName)
        {
            _logger.LogInformation($"[{nodeName}] \x001B[35mStarting...\x001B[0m");
            return Task.CompletedTask;
        }
    }
}
