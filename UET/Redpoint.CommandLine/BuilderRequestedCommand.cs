namespace Redpoint.CommandLine
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    internal class BuilderRequestedCommand<TGlobalContext> where TGlobalContext : class
    {
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        public required Type? InstanceType { get; init; }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
        public required Type? OptionsType { get; init; }

        public required CommandFactory<TGlobalContext> CommandFactory { get; init; }

        public required CommandRuntimeServiceRegistration<TGlobalContext>? AdditionalRuntimeServices { get; init; }

        public required CommandParsingServiceRegistration<TGlobalContext>? AdditionalParsingServices { get; init; }
    }
}
