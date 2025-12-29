using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace GitHub.JPMikkers.Dhcp;

public class Utils
{
    public static TimeSpan InfiniteTimeSpan = TimeSpan.FromSeconds(UInt32.MaxValue);

    public static bool IsInfiniteTimeSpan(TimeSpan timeSpan)
    {
        return (timeSpan < TimeSpan.FromSeconds(0.0) || timeSpan >= InfiniteTimeSpan);
    }

    public static TimeSpan SanitizeTimeSpan(TimeSpan timeSpan)
    {
        return IsInfiniteTimeSpan(timeSpan) ? InfiniteTimeSpan : timeSpan;
    }

    public static bool ByteArraysAreEqual(byte[] array1, byte[] array2)
    {
        if(array1.Length != array2.Length)
        {
            return false;
        }

        for(int i = 0; i < array1.Length; i++)
        {
            if(array1[i] != array2[i])
            {
                return false;
            }
        }

        return true;
    }

    public static string BytesToHexString(byte[] data, string separator)
    {
        return string.IsNullOrEmpty(separator) ? 
            Convert.ToHexString(data) : string.Join(separator, Convert.ToHexString(data).Chunk(2).Select(x => new string(x)));
    }

    public static byte[] HexStringToBytes(string data)
    {
        // strip separator chars, anything not 0-9/A-F
        var sb = new ReadOnlySpan<char>(data.Where(c => Char.IsAsciiHexDigit(c)).ToArray());
        // remove spurious trailing nibble before converting
        return Convert.FromHexString(sb[..(sb.Length & ~1)]);
    }

    public static IPAddress GetSubnetMask(IPAddress address)
    {
        foreach(NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            foreach(UnicastIPAddressInformation unicastIPAddressInformation in adapter.GetIPProperties().UnicastAddresses)
            {
                if(unicastIPAddressInformation.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    if(address.Equals(unicastIPAddressInformation.Address))
                    {
                        // the following mask can be null.. return 255.255.255.0 in that case
                        return unicastIPAddressInformation.IPv4Mask ?? new IPAddress(new byte[] { 255, 255, 255, 0 });
                    }
                }
            }
        }
        throw new ArgumentException($"Can't find subnetmask for IP address '{address}'");
    }

    public static IPAddress UInt32ToIPAddress(UInt32 address)
    {
        return new IPAddress([
            (byte)((address>>24) & 0xFF) ,
            (byte)((address>>16) & 0xFF) ,
            (byte)((address>>8)  & 0xFF) ,
            (byte)( address & 0xFF)]);
    }

    public static UInt32 IPAddressToUInt32(IPAddress address)
    {
        return
            (((UInt32)address.GetAddressBytes()[0]) << 24) |
            (((UInt32)address.GetAddressBytes()[1]) << 16) |
            (((UInt32)address.GetAddressBytes()[2]) << 8) |
            (((UInt32)address.GetAddressBytes()[3]));
    }

    public static string PrefixLines(string src, string prefix)
    {
        StringBuilder sb = new StringBuilder();
        MemoryStream ms = new MemoryStream();
        StreamWriter sw = new StreamWriter(ms);
        sw.Write(src);
        sw.Flush();
        ms.Position = 0;
        StreamReader sr = new StreamReader(ms);
        string? line;
        while((line = sr.ReadLine()) != null)
        {
            sb.Append(prefix);
            sb.AppendLine(line);
        }
        return sb.ToString();
    }
}
