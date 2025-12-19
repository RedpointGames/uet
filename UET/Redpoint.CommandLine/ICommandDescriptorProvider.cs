namespace Redpoint.CommandLine
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

#pragma warning disable CS8618

    public interface ICommandDescriptorProvider
    {
        public static abstract CommandDescriptor Descriptor { get; }
    }

    public interface ICommandDescriptorProvider<TGlobalContext> where TGlobalContext : class
    {
        public static abstract CommandDescriptor<TGlobalContext> Descriptor { get; }
    }

#pragma warning restore CS8618
}
