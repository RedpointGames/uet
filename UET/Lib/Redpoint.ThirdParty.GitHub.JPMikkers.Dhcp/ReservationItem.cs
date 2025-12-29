using System;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace GitHub.JPMikkers.Dhcp;

public class ReservationItem
{
    private static readonly Regex s_regex = new Regex(@"^(?<mac>([0-9a-fA-F][0-9a-fA-F][:\-\.]?)+)(?<netmask>/[0-9]+)?", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
    private string _macTaste = string.Empty;
    private byte[]? _prefix;
    private int _prefixBits;

    public string MacTaste
    {
        get { return _macTaste; }

        set
        {
            _macTaste = value;
            _prefix = null;
            _prefixBits = 0;

            if (!string.IsNullOrWhiteSpace(MacTaste))
            {
                try
                {
                    Match match = s_regex.Match(_macTaste);
                    if (match.Success && match.Groups["mac"].Success)
                    {
                        _prefix = Utils.HexStringToBytes(match.Groups["mac"].Value);
                        _prefixBits = _prefix.Length * 8;

                        if (match.Groups["netmask"].Success)
                        {
                            _prefixBits = Int32.Parse(match.Groups["netmask"].Value.Substring(1));
                        }
                    }
                }
                catch
                {
                }
            }
        }
    }

    public string HostName { get; set; } = string.Empty;
    public IPAddress PoolStart { get; set; } = IPAddress.None;
    public IPAddress PoolEnd { get; set; } = IPAddress.None;
    public bool Preempt { get; set; }

    private static bool MacMatch(byte[] mac, byte[] prefix, int bits)
    {
        // prefix should have more bits than masklength
        if (((bits + 7) >> 3) > prefix.Length) return false;
        // prefix should be shorter or equal to mac address
        if (prefix.Length > mac.Length) return false;
        for (int t = 0; t < (bits - 7); t += 8)
        {
            if (mac[t >> 3] != prefix[t >> 3]) return false;
        }

        if ((bits & 7) > 0)
        {
            byte bitMask = (byte)(0xFF00 >> (bits & 7));
            if ((mac[bits >> 3] & bitMask) != (prefix[bits >> 3] & bitMask)) return false;
        }
        return true;
    }

    public bool Match(DhcpMessage message)
    {
        var client = DhcpClient.CreateFromMessage(message);

        if (!string.IsNullOrWhiteSpace(MacTaste) && _prefix != null)
        {
            return MacMatch(client.HardwareAddress, _prefix, _prefixBits);
        }
        else if (!string.IsNullOrWhiteSpace(HostName))
        {
            if (!string.IsNullOrWhiteSpace(client.HostName))
            {
                if (client.HostName.StartsWith(HostName, true, CultureInfo.InvariantCulture))
                {
                    return true;
                }
            }
        }
        return false;
    }
}
