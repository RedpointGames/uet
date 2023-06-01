namespace Redpoint.OpenGE.Executor.BuildSetData
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;

    internal static class BuildSetReader
    {
        internal static BuildSet ParseBuildSet(Stream stream)
        {
            var document = new XmlDocument();
            document.Load(stream);

            var environments = new Dictionary<string, BuildSetEnvironment>();
            foreach (var env in document.DocumentElement!.SelectNodes("./Environments/Environment")!.OfType<XmlNode>())
            {
                var tools = new Dictionary<string, BuildSetTool>();
                foreach (var tool in env.SelectNodes("./Tools/Tool")!.OfType<XmlNode>())
                {
                    tools.Add(
                        tool.Attributes!["Name"]!.Value,
                        new BuildSetTool
                        {
                            Name = tool.Attributes!["Name"]!.Value,
                            AllowRemote = tool.Attributes!["AllowRemote"]!.Value.Equals("True", StringComparison.InvariantCultureIgnoreCase),
                            GroupPrefix = tool.Attributes!["GroupPrefix"]!.Value,
                            OutputPrefix = tool.Attributes!["OutputPrefix"]?.Value ?? string.Empty,
                            Params = tool.Attributes!["Params"]!.Value,
                            Path = tool.Attributes!["Path"]!.Value,
                            SkipIfProjectFailed = tool.Attributes!["SkipIfProjectFailed"]!.Value.Equals("true", StringComparison.InvariantCultureIgnoreCase),
                            AutoReserveMemory = tool.Attributes!["AutoReserveMemory"]!.Value,
                            OutputFileMasks = tool.Attributes!["OutputFileMasks"]!.Value,
                            AutoRecover = tool.Attributes!["AutoRecover"]?.Value ?? string.Empty,
                        });
                }

                var variables = new Dictionary<string, string>();
                foreach (var variable in env.SelectNodes("./Variables/Variable")!.OfType<XmlNode>())
                {
                    variables.Add(
                        variable.Attributes!["Name"]!.Value,
                        variable.Attributes!["Value"]!.Value);
                }

                environments.Add(
                    env.Attributes!["Name"]!.Value,
                    new BuildSetEnvironment
                    {
                        Name = env.Attributes!["Name"]!.Value,
                        Tools = tools,
                        Variables = variables,
                    });
            }

            var projects = new Dictionary<string, BuildSetProject>();
            foreach (var project in document.DocumentElement!.SelectNodes("./Project")!.OfType<XmlNode>())
            {
                var tasks = new Dictionary<string, BuildSetTask>();
                foreach (var task in project.SelectNodes("./Task")!.OfType<XmlNode>())
                {
                    tasks.Add(
                        task.Attributes!["Name"]!.Value,
                        new BuildSetTask
                        {
                            Name = task.Attributes!["Name"]!.Value,
                            Caption = task.Attributes!["Caption"]!.Value,
                            SourceFile = task.Attributes!["SourceFile"]!.Value,
                            Tool = task.Attributes!["Tool"]!.Value,
                            WorkingDir = task.Attributes!["WorkingDir"]!.Value,
                            SkipIfProjectFailed = task.Attributes!["SkipIfProjectFailed"]!.Value.Equals("true", StringComparison.InvariantCultureIgnoreCase),
                            DependsOn = task.Attributes!["DependsOn"]?.Value,
                        });
                }

                projects.Add(
                    project.Attributes!["Name"]!.Value,
                    new BuildSetProject
                    {
                        Name = project.Attributes!["Name"]!.Value,
                        Env = project.Attributes!["Env"]!.Value,
                        Tasks = tasks,
                    });
            }

            return new BuildSet
            {
                Environments = environments,
                Projects = projects,
            };
        }
    }
}
