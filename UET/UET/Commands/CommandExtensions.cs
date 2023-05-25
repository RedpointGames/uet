namespace UET.Commands
{
    using System.CommandLine;
    using System.Linq;
    using System.Reflection;

    internal static class CommandExtensions
    {
        internal static void AddAllOptions(this Command command, object options)
        {
            foreach (var option in options.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.PropertyType.IsAssignableTo(typeof(Option)))
                .Select(x => (Option)x.GetValue(options)!))
            {
                command.AddOption(option);
            }

            foreach (var option in options.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.FieldType.IsAssignableTo(typeof(Option)))
                .Select(x => (Option)x.GetValue(options)!))
            {
                command.AddOption(option);
            }
        }
    }
}
