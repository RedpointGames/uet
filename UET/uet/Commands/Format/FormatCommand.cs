namespace UET.Commands.Format
{
    using B2Net.Models;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.IO;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.BuildPipeline.BuildGraph.Patching;
    using Redpoint.Uet.CommonPaths;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using Redpoint.Uet.SdkManagement;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using UET.BuildConfig;
    using UET.Commands.EngineSpec;

    internal sealed class FormatCommand
    {
        internal sealed class Options
        {
            public Option<EngineSpec> Engine;
            public Option<PathSpec> Path;
            public Option<bool> DryRun;

            public Options()
            {
                Path = new Option<PathSpec>(
                    "--path",
                    description: "The directory path that contains a .uproject file, a .uplugin file, or a BuildConfig.json file. If this parameter isn't provided, defaults to the current working directory.",
                    parseArgument: PathSpec.ParsePathSpec,
                    isDefault: true);
                Path.AddAlias("-p");
                Path.Arity = ArgumentArity.ExactlyOne;

                Engine = new Option<EngineSpec>(
                    "--engine",
                    description: "The engine to use to detect the relevant compiler toolchain (which is used to locate clang-format).",
                    parseArgument: EngineSpec.ParseEngineSpec(Path, null),
                    isDefault: true);
                Engine.AddAlias("-e");
                Engine.Arity = ArgumentArity.ExactlyOne;

                DryRun = new Option<bool>(
                    "--dry-run",
                    description: "Show a list of files that would be processed, but don't actually make formatting changes.");
            }
        }

        public static Command CreateFormatCommand()
        {
            var options = new Options();
            var command = new Command("format", "Format C++ source code in an Unreal Engine plugin or project.")
            {
                FullDescription = """
                This command formats source code in an Unreal Engine plugin or project using clang-format. If you have a BuildConfig.json file, it'll format source code for all projects across all distributions.

                If you don't have a .clang-format file in the file hierarchy, it'll add one that matches the Unreal Engine code conventions.
                """
            };
            command.AddAllOptions(options);
            command.AddCommonHandler<FormatCommandInstance>(options);
            return command;
        }

        private sealed class FormatCommandInstance : ICommandInstance
        {
            private readonly ILogger<FormatCommandInstance> _logger;
            private readonly IProcessExecutor _processExecutor;
            private readonly ILocalSdkManager _localSdkManager;
            private readonly IServiceProvider _serviceProvider;
            private readonly IPathResolver _pathResolver;
            private readonly IDotnetLocator _dotnetLocator;
            private readonly Options _options;

            public FormatCommandInstance(
                ILogger<FormatCommandInstance> logger,
                IProcessExecutor processExecutor,
                ILocalSdkManager localSdkManager,
                IServiceProvider serviceProvider,
                IPathResolver pathResolver,
                IDotnetLocator dotnetLocator,
                Options options)
            {
                _logger = logger;
                _processExecutor = processExecutor;
                _localSdkManager = localSdkManager;
                _serviceProvider = serviceProvider;
                _pathResolver = pathResolver;
                _dotnetLocator = dotnetLocator;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                if (!OperatingSystem.IsWindows())
                {
                    _logger.LogWarning("'uet format' is not currently supported on this platform as it relies on using clang-format from the installed Windows SDK.");
                    return 0;
                }

                var path = context.ParseResult.GetValueForOption(_options.Path)!;
                var engine = context.ParseResult.GetValueForOption(_options.Engine)!;
                var dryRun = context.ParseResult.GetValueForOption(_options.DryRun)!;

                if (dryRun)
                {
                    _logger.LogInformation("--dry-run specified; no changes to source code files will be made (but the relevant SDKs might be installed to locate clang-format).");
                }

                // Detect which directories we should run code formatting for.
                var directories = new List<string>();
                switch (path.Type)
                {
                    case PathSpecType.UPlugin:
                    case PathSpecType.UProject:
                        directories.Add(Path.Combine(path.DirectoryPath, "Source"));
                        break;
                    case PathSpecType.BuildConfig:
                        var loadResult = BuildConfigLoader.TryLoad(
                            _serviceProvider,
                            Path.Combine(path.DirectoryPath, "BuildConfig.json"));
                        switch (loadResult.BuildConfig)
                        {
                            case BuildConfigProject buildConfigProject:
                                foreach (var distribution in buildConfigProject.Distributions)
                                {
                                    directories.Add(Path.Combine(path.DirectoryPath, distribution.FolderName, "Source"));
                                }
                                break;
                            case BuildConfigPlugin buildConfigPlugin:
                                directories.Add(Path.Combine(path.DirectoryPath, buildConfigPlugin.PluginName, "Source"));
                                break;
                        }
                        break;
                }
                if (directories.Count == 0)
                {
                    _logger.LogInformation("No directories for code formatting were detected from the provided --path (or BuildConfig.json).");
                    return 0;
                }

                // Install the relevant Windows SDK if needed, and locate clang-format.exe.
                var packagePath = UetPaths.UetDefaultWindowsSdkStoragePath;
                Directory.CreateDirectory(packagePath);
                var envVars = await _localSdkManager.SetupEnvironmentForSdkSetups(
                    engine.Path!,
                    packagePath,
                    _serviceProvider.GetServices<ISdkSetup>().ToHashSet(),
                    context.GetCancellationToken()).ConfigureAwait(false);
                var clangFormatPath = Path.Combine(envVars["UE_SDKS_ROOT"], "HostWin64", "Win64", "VS2022", "VC", "Tools", "Llvm", "x64", "bin", "clang-format.exe");
                if (!File.Exists(clangFormatPath))
                {
                    _logger.LogError($"Expected clang-format to exist at the following path, but it was not found: {clangFormatPath}");
                    return 1;
                }

                // Now run clang-format on all of the paths, creating .clang-format if it doesn't exist.
                if (directories.Count == 1)
                {
                    _logger.LogInformation("There is 1 directory to run code formatting on:");
                }
                else
                {
                    _logger.LogInformation($"There are {directories.Count} directories to run code formatting on:");
                }
                foreach (var directory in directories)
                {
                    _logger.LogInformation($"- {directory}");
                }
                foreach (var directory in directories)
                {
                    // Ensure .clang-format exists.
                    var clangFormatFilePath = directory;
                    while (clangFormatFilePath != null && !File.Exists(Path.Combine(clangFormatFilePath, ".clang-format")))
                    {
                        clangFormatFilePath = Path.GetDirectoryName(clangFormatFilePath);
                    }
                    if (clangFormatFilePath == null)
                    {
                        if (!dryRun)
                        {
                            _logger.LogInformation($"Creating required .clang-format file: {Path.Combine(directory, ".clang-format")}");
                            File.WriteAllText(Path.Combine(directory, ".clang-format"), @"
---
BasedOnStyle: Microsoft
---
Language: Cpp
AllowShortLambdasOnASingleLine: None
AccessModifierOffset: -4
BinPackArguments: false
BinPackParameters: false
AllowAllArgumentsOnNextLine: false
AllowAllConstructorInitializersOnNextLine: false
AllowAllParametersOfDeclarationOnNextLine: false
AlignAfterOpenBracket: AlwaysBreak
BreakConstructorInitializers: BeforeComma
FixNamespaceComments: false
---
");
                        }
                        else
                        {
                            _logger.LogInformation($"Would create required .clang-format file: {Path.Combine(directory, ".clang-format")}");
                        }
                    }

                    // Ensure .editorconfig exists.
                    var editorConfigFilePath = directory;
                    while (editorConfigFilePath != null && !File.Exists(Path.Combine(editorConfigFilePath, ".editorconfig")))
                    {
                        editorConfigFilePath = Path.GetDirectoryName(editorConfigFilePath);
                    }
                    if (editorConfigFilePath == null)
                    {
                        if (!dryRun)
                        {
                            _logger.LogInformation($"Creating required .editorconfig file: {Path.Combine(directory, ".editorconfig")}");
                            File.WriteAllText(Path.Combine(directory, ".editorconfig"), @"
root = true

[*.uplugin]
charset = utf-8
indent_style = space
indent_size = 2

[*.cs]
indent_size = 4
indent_style = space
tab_width = 4
end_of_line = lf
insert_final_newline = false
dotnet_separate_import_directive_groups = false
dotnet_sort_system_directives_first = false
file_header_template = unset
dotnet_style_qualification_for_event = false:silent
dotnet_style_qualification_for_field = false:silent
dotnet_style_qualification_for_method = false:silent
dotnet_style_qualification_for_property = false:silent
dotnet_style_predefined_type_for_locals_parameters_members = true:silent
dotnet_style_predefined_type_for_member_access = true:silent
dotnet_style_parentheses_in_arithmetic_binary_operators = always_for_clarity:silent
dotnet_style_parentheses_in_other_binary_operators = always_for_clarity:silent
dotnet_style_parentheses_in_other_operators = never_if_unnecessary:silent
dotnet_style_parentheses_in_relational_binary_operators = always_for_clarity:silent
dotnet_style_require_accessibility_modifiers = for_non_interface_members:silent
dotnet_style_coalesce_expression = true:suggestion
dotnet_style_collection_initializer = true:suggestion
dotnet_style_explicit_tuple_names = true:suggestion
dotnet_style_null_propagation = true:suggestion
dotnet_style_object_initializer = true:suggestion
dotnet_style_operator_placement_when_wrapping = beginning_of_line
dotnet_style_prefer_auto_properties = true:silent
dotnet_style_prefer_compound_assignment = true:suggestion
dotnet_style_prefer_conditional_expression_over_assignment = true:silent
dotnet_style_prefer_conditional_expression_over_return = true:silent
dotnet_style_prefer_inferred_anonymous_type_member_names = true:suggestion
dotnet_style_prefer_inferred_tuple_names = true:suggestion
dotnet_style_prefer_is_null_check_over_reference_equality_method = true:suggestion
dotnet_style_prefer_simplified_boolean_expressions = true:suggestion
dotnet_style_prefer_simplified_interpolation = true:suggestion
dotnet_style_readonly_field = true:suggestion
dotnet_code_quality_unused_parameters = all:suggestion
dotnet_remove_unnecessary_suppression_exclusions = none
csharp_style_var_elsewhere = true:silent
csharp_style_var_for_built_in_types = true:silent
csharp_style_var_when_type_is_apparent = true:silent
csharp_style_expression_bodied_accessors = true:silent
csharp_style_expression_bodied_constructors = false:silent
csharp_style_expression_bodied_indexers = true:silent
csharp_style_expression_bodied_lambdas = true:silent
csharp_style_expression_bodied_local_functions = false:silent
csharp_style_expression_bodied_methods = false:silent
csharp_style_expression_bodied_operators = false:silent
csharp_style_expression_bodied_properties = true:silent
csharp_style_pattern_matching_over_as_with_null_check = true:suggestion
csharp_style_pattern_matching_over_is_with_cast_check = true:suggestion
csharp_style_prefer_not_pattern = true:suggestion
csharp_style_prefer_pattern_matching = true:silent
csharp_style_prefer_switch_expression = true:suggestion
csharp_style_conditional_delegate_call = true:suggestion
csharp_prefer_static_local_function = true:suggestion
csharp_preferred_modifier_order = public,private,protected,internal,static,extern,new,virtual,abstract,sealed,override,readonly,unsafe,volatile,async:silent
csharp_prefer_braces = true:silent
csharp_prefer_simple_using_statement = true:suggestion
csharp_prefer_simple_default_expression = true:suggestion
csharp_style_deconstructed_variable_declaration = true:suggestion
csharp_style_inlined_variable_declaration = true:suggestion
csharp_style_pattern_local_over_anonymous_function = true:suggestion
csharp_style_prefer_index_operator = true:suggestion
csharp_style_prefer_range_operator = true:suggestion
csharp_style_throw_expression = true:suggestion
csharp_style_unused_value_assignment_preference = discard_variable:suggestion
csharp_style_unused_value_expression_statement_preference = discard_variable:silent
csharp_using_directive_placement = inside_namespace:silent
csharp_new_line_before_catch = true
csharp_new_line_before_else = true
csharp_new_line_before_finally = true
csharp_new_line_before_members_in_anonymous_types = true
csharp_new_line_before_members_in_object_initializers = true
csharp_new_line_before_open_brace = all
csharp_new_line_between_query_expression_clauses = true
csharp_indent_block_contents = true
csharp_indent_braces = false
csharp_indent_case_contents = true
csharp_indent_case_contents_when_block = true
csharp_indent_labels = one_less_than_current
csharp_indent_switch_labels = true
csharp_space_after_cast = false
csharp_space_after_colon_in_inheritance_clause = true
csharp_space_after_comma = true
csharp_space_after_dot = false
csharp_space_after_keywords_in_control_flow_statements = true
csharp_space_after_semicolon_in_for_statement = true
csharp_space_around_binary_operators = before_and_after
csharp_space_around_declaration_statements = false
csharp_space_before_colon_in_inheritance_clause = true
csharp_space_before_comma = false
csharp_space_before_dot = false
csharp_space_before_open_square_brackets = false
csharp_space_before_semicolon_in_for_statement = false
csharp_space_between_empty_square_brackets = false
csharp_space_between_method_call_empty_parameter_list_parentheses = false
csharp_space_between_method_call_name_and_opening_parenthesis = false
csharp_space_between_method_call_parameter_list_parentheses = false
csharp_space_between_method_declaration_empty_parameter_list_parentheses = false
csharp_space_between_method_declaration_name_and_open_parenthesis = false
csharp_space_between_method_declaration_parameter_list_parentheses = false
csharp_space_between_parentheses = false
csharp_space_between_square_brackets = false
csharp_preserve_single_line_blocks = true
csharp_preserve_single_line_statements = true
dotnet_naming_rule.interface_should_be_begins_with_i.severity = error
dotnet_naming_rule.interface_should_be_begins_with_i.symbols = interface
dotnet_naming_rule.interface_should_be_begins_with_i.style = begins_with_i
dotnet_naming_rule.types_should_be_pascal_case.severity = error
dotnet_naming_rule.types_should_be_pascal_case.symbols = types
dotnet_naming_rule.types_should_be_pascal_case.style = pascal_case
dotnet_naming_rule.non_field_members_should_be_pascal_case.severity = error
dotnet_naming_rule.non_field_members_should_be_pascal_case.symbols = non_field_members
dotnet_naming_rule.non_field_members_should_be_pascal_case.style = pascal_case
dotnet_naming_rule.field_members_should_be_camel_case.severity = error
dotnet_naming_rule.field_members_should_be_camel_case.symbols = field_members
dotnet_naming_rule.field_members_should_be_camel_case.style = begins_with_underscore
dotnet_naming_symbols.interface.applicable_kinds = interface
dotnet_naming_symbols.interface.applicable_accessibilities = public, internal, private, protected, protected_internal, private_protected
dotnet_naming_symbols.interface.required_modifiers =
dotnet_naming_symbols.types.applicable_kinds = class, struct, interface, enum
dotnet_naming_symbols.types.applicable_accessibilities = public, internal, private, protected, protected_internal, private_protected
dotnet_naming_symbols.types.required_modifiers =
dotnet_naming_symbols.non_field_members.applicable_kinds = event, method
dotnet_naming_symbols.non_field_members.applicable_accessibilities = public, internal, private, protected, protected_internal, private_protected
dotnet_naming_symbols.non_field_members.required_modifiers =
dotnet_naming_symbols.field_members.applicable_kinds = field
dotnet_naming_symbols.field_members.applicable_accessibilities = internal, private, protected, protected_internal, private_protected
dotnet_naming_symbols.field_members.required_modifiers =
dotnet_naming_style.pascal_case.required_prefix =
dotnet_naming_style.pascal_case.required_suffix =
dotnet_naming_style.pascal_case.word_separator =
dotnet_naming_style.pascal_case.capitalization = pascal_case
dotnet_naming_style.begins_with_i.required_prefix = I
dotnet_naming_style.begins_with_i.required_suffix =
dotnet_naming_style.begins_with_i.word_separator =
dotnet_naming_style.begins_with_i.capitalization = pascal_case
dotnet_naming_style.begins_with_underscore.required_prefix = _
dotnet_naming_style.begins_with_underscore.required_suffix =
dotnet_naming_style.begins_with_underscore.word_separator =
dotnet_naming_style.begins_with_underscore.capitalization = camel_case
csharp_style_namespace_declarations = block_scoped:silent
csharp_style_prefer_method_group_conversion = true:silent
csharp_style_prefer_top_level_statements = true:silent

[*.{cs,vb}]
dotnet_style_operator_placement_when_wrapping = beginning_of_line
tab_width = 4
indent_size = 4
end_of_line = lf
dotnet_style_coalesce_expression = true:suggestion
dotnet_style_null_propagation = true:suggestion
dotnet_style_prefer_is_null_check_over_reference_equality_method = true:suggestion
dotnet_style_prefer_auto_properties = true:silent
dotnet_style_object_initializer = true:suggestion
dotnet_style_collection_initializer = true:suggestion
dotnet_style_prefer_simplified_boolean_expressions = true:suggestion
dotnet_style_prefer_conditional_expression_over_assignment = true:silent
dotnet_style_prefer_conditional_expression_over_return = true:silent
dotnet_style_explicit_tuple_names = true:suggestion
dotnet_style_prefer_inferred_tuple_names = true:suggestion
dotnet_style_prefer_inferred_anonymous_type_member_names = true:suggestion
dotnet_style_prefer_compound_assignment = true:suggestion
dotnet_style_prefer_simplified_interpolation = true:suggestion
dotnet_style_namespace_match_folder = true:suggestion
csharp_style_implicit_object_creation_when_type_is_apparent = true:suggestion
csharp_style_prefer_primary_constructors = false:suggestion
dotnet_style_prefer_collection_expression = true:suggestion
dotnet_diagnostic.IDE0004.severity = suggestion
dotnet_diagnostic.IDE0005.severity = suggestion
dotnet_diagnostic.IDE0007.severity = suggestion
dotnet_diagnostic.IDE0010.severity = suggestion
dotnet_diagnostic.IDE0028.severity = suggestion
dotnet_diagnostic.IDE0039.severity = suggestion
dotnet_diagnostic.IDE0044.severity = suggestion
dotnet_diagnostic.IDE0051.severity = suggestion
dotnet_diagnostic.IDE0052.severity = suggestion
dotnet_diagnostic.IDE0055.severity = suggestion
dotnet_diagnostic.IDE0058.severity = suggestion
dotnet_diagnostic.IDE0072.severity = suggestion
dotnet_diagnostic.IDE0100.severity = suggestion
dotnet_diagnostic.IDE0180.severity = suggestion
dotnet_diagnostic.IDE0230.severity = suggestion
dotnet_diagnostic.IDE0250.severity = suggestion
dotnet_diagnostic.IDE0251.severity = suggestion
dotnet_diagnostic.IDE0305.severity = suggestion
dotnet_diagnostic.CA1514.severity = suggestion
dotnet_diagnostic.CA1515.severity = suggestion
dotnet_diagnostic.CA1859.severity = suggestion
dotnet_diagnostic.CA2007.severity = suggestion
dotnet_diagnostic.CA2022.severity = suggestion
dotnet_diagnostic.CA2263.severity = suggestion
dotnet_diagnostic.RS1035.severity = none
");
                        }
                        else
                        {
                            _logger.LogInformation($"Would create required .editorconfig file: {Path.Combine(directory, ".editorconfig")}");
                        }
                    }

                    // Find all .cpp and .h files recursively in the target directory, and generate a file list to
                    // execute clang-format.exe with.
                    var cppFileList = new HashSet<string>();
                    foreach (var file in Directory.EnumerateFiles(directory, "*.cpp", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true }))
                    {
                        cppFileList.Add(file);
                    }
                    foreach (var file in Directory.EnumerateFiles(directory, "*.h", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true }))
                    {
                        cppFileList.Add(file);
                    }
                    var tempFileList = Path.GetTempFileName();
                    await File.WriteAllLinesAsync(tempFileList, cppFileList, context.GetCancellationToken()).ConfigureAwait(false);

                    // Run clang-format.exe.
                    if (cppFileList.Count > 0)
                    {
                        _logger.LogInformation($"Executing 'clang-format' on {cppFileList.Count} files in '{directory}'...");
                        await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = clangFormatPath,
                                Arguments = dryRun ? ["-i", $"--files={tempFileList}", "--verbose", "--dry-run"] : ["-i", $"--files={tempFileList}", "--verbose"],
                            },
                            CaptureSpecification.Passthrough,
                            context.GetCancellationToken()).ConfigureAwait(false);
                    }

                    // Try to format C# files.
                    var dotnetPath = await _dotnetLocator.TryLocateDotNetWithinEngine(engine.Path!).ConfigureAwait(false);
                    if (dotnetPath != null)
                    {
                        // Find all .cs files recursively in the target directory, and generate a .NET project file that references all of them.
                        var csDocument = new XmlDocument();
                        var project = csDocument.CreateElement("Project");
                        project.SetAttribute("Sdk", "Microsoft.NET.Sdk");
                        csDocument.AppendChild(project);
                        var propertyGroup = csDocument.CreateElement("PropertyGroup");
                        var targetFramework = csDocument.CreateElement("TargetFramework");
                        targetFramework.InnerText = "net6.0";
                        propertyGroup.AppendChild(targetFramework);
                        var langVersion = csDocument.CreateElement("LangVersion");
                        langVersion.InnerText = "10.0";
                        propertyGroup.AppendChild(langVersion);
                        project.AppendChild(propertyGroup);
                        var itemGroup = csDocument.CreateElement("ItemGroup");
                        var csFileCount = 0;
                        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true }))
                        {
                            var item = csDocument.CreateElement("Compile");
                            item.SetAttribute("Include", file);
                            var link = csDocument.CreateElement("Link");
                            link.InnerText = Path.GetFileName(file);
                            item.AppendChild(link);
                            itemGroup.AppendChild(item);
                            csFileCount++;
                        }
                        project.AppendChild(itemGroup);

                        var tempDirectory = Path.GetTempFileName();
                        if (File.Exists(tempDirectory))
                        {
                            File.Delete(tempDirectory);
                        }
                        if (Directory.Exists(tempDirectory))
                        {
                            await DirectoryAsync.DeleteAsync(tempDirectory, true);
                        }
                        Directory.CreateDirectory(tempDirectory);

                        var tempMsBuildProject = Path.Combine(tempDirectory, "Project.csproj");
                        csDocument.Save(tempMsBuildProject);

                        // Run dotnet format.
                        if (csFileCount > 0)
                        {
                            _logger.LogInformation($"Executing 'dotnet format' on {csFileCount} files in '{directory}'...");
                            await _processExecutor.ExecuteAsync(
                                new ProcessSpecification
                                {
                                    FilePath = dotnetPath,
                                    Arguments = [
                                        "format",
                                        "--severity", "info",
                                        "--exclude-diagnostics", "IDE0005",
                                        "--exclude-diagnostics", "IDE1006",
                                        "--exclude-diagnostics", "IDE0060",
                                        "--exclude-diagnostics", "CA1866",
                                        "--exclude-diagnostics", "CA1050",
                                        "-v", "diag",
                                        tempMsBuildProject
                                    ],
                                    WorkingDirectory = tempDirectory,
                                },
                                CaptureSpecification.Passthrough,
                                context.GetCancellationToken()).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        var csFileCount = 0;
                        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true }))
                        {
                            csFileCount++;
                        }

                        _logger.LogWarning($"Skipping 'dotnet format' for {csFileCount} files in '{directory}', because 'dotnet' could not be located in the engine or on the PATH.");
                    }
                }

                {
                    // Try to format .uplugin and .uproject files.
                    var jsonFileList = new HashSet<string>();
                    foreach (var file in Directory.EnumerateFiles(path.DirectoryPath, "*.uplugin", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true }))
                    {
                        jsonFileList.Add(file);
                    }
                    foreach (var file in Directory.EnumerateFiles(path.DirectoryPath, "*.uproject", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true }))
                    {
                        jsonFileList.Add(file);
                    }
                    string? yarnPath = null;
                    try
                    {
                        yarnPath = await _pathResolver.ResolveBinaryPath("yarn").ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                    if (yarnPath != null)
                    {
                        // Run yarn dlx.
                        if (jsonFileList.Count > 0)
                        {
                            _logger.LogInformation($"Executing 'yarn dlx prettier' on {jsonFileList.Count} files via Yarn in '{path.DirectoryPath}'...");
                            await _processExecutor.ExecuteAsync(
                                new ProcessSpecification
                                {
                                    FilePath = yarnPath,
                                    Arguments = new LogicalProcessArgument[] {
                                        "dlx",
                                        "prettier",
                                        "-w",
                                        "--parser",
                                        "json",
                                    }.Concat(jsonFileList.Select(x => new LogicalProcessArgument(x))).ToArray(),
                                    EnvironmentVariables = new Dictionary<string, string>
                                    {
                                        { "COREPACK_ENABLE_DOWNLOAD_PROMPT", "0" }
                                    }
                                },
                                CaptureSpecification.Passthrough,
                                context.GetCancellationToken()).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"Skipping 'yarn dlx prettier' for {jsonFileList.Count} files in '{path.DirectoryPath}', because 'yarn' could not be located in the engine or on the PATH.");
                    }
                }

                return 0;
            }
        }
    }
}
