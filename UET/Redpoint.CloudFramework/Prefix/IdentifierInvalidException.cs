namespace Redpoint.CloudFramework.Prefix
{
    using System;

    public class IdentifierInvalidException : Exception
    {
        public IdentifierInvalidException(string identifier, string reason)
            : base("Identifier invalid: " + identifier + ", " + reason)
        {
            Identifier = identifier;
            Reason = reason;
        }

        public string Identifier { get; }
        public string Reason { get; }
    }
}
