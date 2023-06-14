namespace Docker.Registry.DotNet.QueryParameters
{
    using System;

    [AttributeUsage(AttributeTargets.Property)]
    internal class QueryParameterAttribute : Attribute
    {
        public QueryParameterAttribute(string key)
        {
            this.Key = key;
        }

        public string Key { get; }
    }
}