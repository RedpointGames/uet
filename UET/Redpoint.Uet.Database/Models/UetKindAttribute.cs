namespace Redpoint.Uet.Database.Models
{
    using System;

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class UetKindAttribute : Attribute
    {
        public UetKindAttribute(string kind)
        {
            Kind = kind;
        }

        public string Kind { get; }
    }
}
