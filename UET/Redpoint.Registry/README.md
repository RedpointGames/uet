# Redpoint.Registry

This library provides an easy way to access nested registry keys without having to individually open each component on the registry path.

## Example

You can open an arbitrary registry key in read-only mode as shown below.

```csharp
using (var stack = RegistryStack.OpenPath("HKCU:\SOFTWARE\MyCompany\SomeKey"))
{
    if (stack.Exists)
    {
        // Access the key in read-only mode via Key.
        var key = stack.Key;
    }
}
```

If you want to open the key in read-write mode, pass `writable: true`.

```csharp
using (var stack = RegistryStack.OpenPath("HKCU:\SOFTWARE\MyCompany\SomeKey", writable: true))
{
    if (stack.Exists)
    {
        // Access the key in read-write mode via Key.
        var key = stack.Key;
    }
}
```

If you want to create the key if it does not exist, pass `create: true`. This infers `writable: true`.

```csharp
using (var stack = RegistryStack.OpenPath("HKCU:\SOFTWARE\MyCompany\SomeKey", create: true))
{
    // Access the key in read-write mode via Key.
    var key = stack.Key;
}
```
