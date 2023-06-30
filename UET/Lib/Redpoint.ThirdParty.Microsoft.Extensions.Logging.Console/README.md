# Redpoint.ThirdParty.Microsoft.Extensions.Logging.Console

The ConsoleLoggingProvider inside Microsoft.Logging.Extensions.Console forcibly strips ANSI color codes
on Windows if the program isn't directly connected to a console. Since we want the ANSI color codes to
be emitted when running under CI, we have to fork the infrastructure inside Microsoft.Logging.Extensions.Console
so we can override all that stuff.

This fork fixes this by allowing ANSI colors to be forced via the console options.
