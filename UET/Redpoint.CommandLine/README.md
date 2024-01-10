# Redpoint.CommandLine

This library provides APIs for building dependency injected console applications, with both commands and option declarations being dependency injected.

To use this library, first define your command:

```csharp
internal class SomeAction
{
	public class Options
	{
		public Option<string> SomeOpt = new Option<string>("--opt", "My option description.");

		// Add more fields or properties that use Option<> or Argument<> and they'll be automatically
		// registered to the command line parser.
	}

	public static Command CreateCommand(ICommandBuilder builder)
	{
		return new Command("some-action", "The description of your some-action command.");
	}

	public class CommandInstance : ICommandInstance
	{
        private readonly ILogger<CommandInstance> _logger;
        private readonly Options _options;

        public CommandInstance(
            ILogger<CommandInstance> logger,
            Options options)
        {
            _logger = logger;
            _options = options;
        }

        public Task<int> ExecuteAsync(ICommandInvocationContext context)
        {
            // Example on how to access an option's value.
            var value = context.ParseResult.GetValueForOption(_options.SomeOpt);

            // ... implement your command here ...
            _logger.LogInformation("Hello world!");

            // Return the exit code for the application.
            return Task.FromResult(0);
        }
	}
}
```

To register and execute commands, in your Program.Main:

```csharp
// Use the command line builder APIs to build the root command.
var rootCommand = CommandLineBuilder.NewBuilder()
    .AddGlobalRuntimeServices((builder, services) =>
    {
        // Register global runtime services here, such as services.AddLogging(...)
    })
    .SetGlobalExecutionHandler(async (sp, executeCommand) =>
    {
        // Can be used to wrap execution of all commands, such as capturing
        // exceptions and logging them:

        var logger = sp.GetRequiredService<ILogger<Program>>();
        try
        {
            return await executeCommand().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Uncaught exception during command execution: {ex}");
            return 1;
        }
    })
    // Add each of your commands like this:
    .AddCommand<SomeAction.CommandInstance, SomeAction.Options>(SomeAction.CreateCommand)
    // Then build the root command to use below.
    .Build("My console application description.");

// Parse and execute the command.
var exitCode = await rootCommand.InvokeAsync(args).ConfigureAwait(false);
await Console.Out.FlushAsync().ConfigureAwait(false);
await Console.Error.FlushAsync().ConfigureAwait(false);
Environment.Exit(exitCode);
throw new BadImageFormatException();
```