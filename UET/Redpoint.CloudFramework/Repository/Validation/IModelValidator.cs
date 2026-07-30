namespace Redpoint.CloudFramework.Repository.Validation
{
    using Redpoint.CloudFramework.Models;

    /// <summary>
    /// An interface which can be used to validate that a model type has all of it's fields configured correctly.
    /// </summary>
    public interface IModelValidator
    {
        /// <summary>
        /// Validate that a particular model can be processed by the repository converters. This method will throw the original exception of a converter if conversion fails. If this method does not throw an exception, the model is valid.
        /// </summary>
        /// <typeparam name="T">The model type to validate.</typeparam>
        /// <returns>Nothing.</returns>
        void ValidateModelFields<T>() where T : Model<T>, new();
    }
}
