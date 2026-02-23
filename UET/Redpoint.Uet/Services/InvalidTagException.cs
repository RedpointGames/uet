namespace Redpoint.Uet.Services
{
    using System;

    public class InvalidTagException : Exception
    {
        public InvalidTagException(string message)
            : base(message)
        {
        }
    }
}
