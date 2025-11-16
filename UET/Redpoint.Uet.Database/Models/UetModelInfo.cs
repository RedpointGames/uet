namespace Redpoint.Uet.Database.Models
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    internal sealed class UetModelInfo
    {
        public required string _kind;
        public required Dictionary<string, Type> _types;
        public required PropertyInfo[] _propertyInfos;
        public required Dictionary<string, PropertyInfo> _propertyInfoByName;
    }
}
