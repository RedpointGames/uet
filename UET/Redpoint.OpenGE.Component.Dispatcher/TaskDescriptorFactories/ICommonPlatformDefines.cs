namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories
{
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal interface ICommonPlatformDefines
    {
        void ApplyDefines(string platform, CompilerArchitype compilerArchitype);
    }
}
