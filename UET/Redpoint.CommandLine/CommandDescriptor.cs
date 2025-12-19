namespace Redpoint.CommandLine
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class CommandDescriptor
    {
        internal CommandDescriptor()
        {
        }

        public static CommandDescriptorBuilder NewBuilder()
        {
            return new CommandDescriptorBuilder();
        }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
        internal Type? OptionsType { get; set; }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        internal Type? InstanceType { get; set; }

        internal CommandFactory? CommandFactory { get; init; }

        internal CommandRuntimeServiceRegistration? RuntimeServices { get; init; }

        internal CommandParsingServiceRegistration? ParsingServices { get; init; }
    }

    public class CommandDescriptor<TGlobalContext> where TGlobalContext : class
    {
        internal CommandDescriptor()
        {
        }

        public static CommandDescriptorBuilder<TGlobalContext> NewBuilder()
        {
            return new CommandDescriptorBuilder<TGlobalContext>();
        }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
        internal Type? OptionsType { get; set; }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        internal Type? InstanceType { get; set; }

        internal CommandFactory<TGlobalContext>? CommandFactory { get; init; }

        internal CommandRuntimeServiceRegistration<TGlobalContext>? RuntimeServices { get; init; }

        internal CommandParsingServiceRegistration<TGlobalContext>? ParsingServices { get; init; }
    }
}
