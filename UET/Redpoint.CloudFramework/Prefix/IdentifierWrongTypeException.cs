namespace Redpoint.CloudFramework.Prefix
{
    using System;

    public class IdentifierWrongTypeException : Exception
    {
        public IdentifierWrongTypeException(string identifier, string expectedKind)
            : base("Identifier wrong type: " + identifier + ", expected " + expectedKind)
        {
            Identifier = identifier;
            ExpectedKind = expectedKind;
        }

        public string Identifier { get; }
        public string ExpectedKind { get; }
    }
}
