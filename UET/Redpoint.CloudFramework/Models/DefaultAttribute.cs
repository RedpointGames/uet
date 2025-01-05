namespace Redpoint.CloudFramework.Models
{
    using System;

    /// <summary>
    /// Specifies a default value for the field in Datastore.
    /// </summary>
    /// <remarks>
    /// If Datastore would load an entity, and the value is null or not set, then the
    /// framework returns the default value specified in the attribute.
    /// 
    /// If you set the value of a reference field to null, the framework will store
    /// the default value instead. It will still locally be null in C# until you next
    /// load the model in C#. If you need to prevent C# from storing nulls, you should
    /// enable the C# nullable feature.
    /// 
    /// If a value-based property has a [Default(...)] attribute, then you can use
    /// the non-nullable value type. For example, you can use "bool" instead of "bool?"
    /// when declaring the property in your model.
    /// 
    /// When you construct a model that inherits from AttributedModel that uses
    /// the [Default] attribute, the AttributeModel base constructor initializes all 
    /// of the properties to their default values. This ensures that even newly
    /// constructed models contain valid non-null values for defaulted properties
    /// in Datastore.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class DefaultAttribute : Attribute
    {
        public DefaultAttribute(object defaultValue)
        {
            ArgumentNullException.ThrowIfNull(defaultValue);

            DefaultValue = defaultValue;
        }

        public object DefaultValue { get; }
    }
}
