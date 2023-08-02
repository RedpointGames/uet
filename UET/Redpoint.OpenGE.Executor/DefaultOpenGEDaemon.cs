namespace Redpoint.OpenGE.Executor
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using OpenGEAPI;
    using Redpoint.GrpcPipes;
    using System;
    using System.Diagnostics;
    using System.Text;
    using System.Threading.Tasks;
    using static Crayon.Output;

    internal class DefaultOpenGEDaemon : OpenGE.OpenGEBase, IOpenGEDaemon, IDisposable
    {
    }
}
