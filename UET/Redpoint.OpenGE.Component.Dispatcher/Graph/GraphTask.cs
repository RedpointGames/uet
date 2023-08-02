namespace Redpoint.OpenGE.Component.Dispatcher.Graph
{
    using Redpoint.OpenGE.Protocol;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal class GraphTask
    {
        public required GraphTaskSpec GraphTaskSpec { get; init; }
        public required TaskDescriptor TaskDescriptor { get; init; }
    }
}
