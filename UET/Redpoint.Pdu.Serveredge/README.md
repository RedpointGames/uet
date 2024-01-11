# Redpoint.Pdu.Serveredge

This library provides an implementation of `Redpoint.Pdu.Abstractions` for the Serveredge 8 Port Switched PDU (SEDG-8PSW-C13).

To use it, create a Serveredge PDU factory and then try to connect to the PDU:

```csharp
var factory = new ServeredgePduFactory();
var pdu = await factory.TryGetAsync(
	new IPAddress("192.168.1.1"),
	"public");
if (pdu == null)
{
	// Target is not responding, or is not a supported Serveredge PDU.
	return;
}
```



This library provides abstractions for working with power distribution units. You need to use it with another library such as Redpoint.Pdu.Serveredge which provides an implementation of `IPduFactory`.