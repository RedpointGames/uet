namespace Redpoint.CommandLine
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    internal abstract class BuilderRequestedCommand<TGlobalContext> where TGlobalContext : class
    {
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        public abstract Type CommandType { get; }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
        public abstract Type OptionsType { get; }

        public required CommandDescriptorFactory<TGlobalContext> CommandDescriptorFactory { get; init; }

        public required CommandServiceRegistration<TGlobalContext>? AdditionalRuntimeServices { get; init; }

        public required CommandServiceRegistration<TGlobalContext>? AdditionalParsingServices { get; init; }
    }

    internal class BuilderRequestedCommand<
        TGlobalContext,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TOptions> : BuilderRequestedCommand<TGlobalContext> where TGlobalContext : class where TCommand : class, ICommandInstance where TOptions : class
    {
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        public override Type CommandType => typeof(TCommand);

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
        public override Type OptionsType => typeof(TOptions);
    }
}
