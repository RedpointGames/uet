# Redpoint.ProcessTree

This library provides a cross-platform API to read the parent process of any other process on the system.

## Example

After injecting the `IProcessTree` service, you can get the parent process of any other process on the system like so:

```csharp
// Inject this...
IProcessTree processTree;

// The parent process of the current process.
_ = processTree.GetParentProcess();

// The parent process of process with ID 100.
_ = processTree.GetParentProcess(100);

// The parent process of process by Process object.
Process process;
_ = processTree.GetParentProcess(process);
```
