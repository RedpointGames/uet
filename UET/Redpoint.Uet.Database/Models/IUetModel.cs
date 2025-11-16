namespace Redpoint.Uet.Database.Models
{
    using System.Reflection;

    public interface IUetModel
    {
        [UetField]
        string Key { get; set; }

        internal string GetKind();
        internal PropertyInfo[] GetPropertyInfos();
        internal PropertyInfo? GetPropertyInfo(string name);
    }
}
