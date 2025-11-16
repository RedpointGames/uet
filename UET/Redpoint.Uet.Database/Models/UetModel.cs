namespace Redpoint.Uet.Database.Models
{
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    public class UetModel<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T> : IUetModel where T : UetModel<T>
    {
        private readonly UetModelInfo _modelInfo;

        [UetField]
        public string Key { get; set; }

#pragma warning disable CS8618
        public UetModel()
#pragma warning restore CS8618
        {
            _modelInfo = UetModelInfoRegistry.InitModel(this);
        }

        [SuppressMessage("Design", "CA1033:Interface methods should be callable by child types", Justification = "This method will not be called by child classes.")]
        string IUetModel.GetKind() => _modelInfo._kind;

        [SuppressMessage("Design", "CA1033:Interface methods should be callable by child types", Justification = "This method will not be called by child classes.")]
        PropertyInfo[] IUetModel.GetPropertyInfos() => _modelInfo._propertyInfos;

        [SuppressMessage("Design", "CA1033:Interface methods should be callable by child types", Justification = "This method will not be called by child classes.")]
        PropertyInfo? IUetModel.GetPropertyInfo(string name)
        {
            if (_modelInfo._propertyInfoByName.TryGetValue(name, out var propertyInfo))
            {
                return propertyInfo;
            }
            return null;
        }
    }
}
