# Redpoint.Pdu.CyberPower

This library provides an implementation of `Redpoint.Pdu.CyberPower` for the CyberPower PDU81404.

To use it, create a CyberPower PDU factory and then try to connect to the PDU:

```csharp
var factory = new CyberPowerPduFactory();
var pdu = await factory.TryGetAsync(
	new IPAddress("192.168.1.1"),
	"public");
if (pdu == null)
{
	// Target is not responding, or is not a supported CyberPower PDU.
	return;
}
```
