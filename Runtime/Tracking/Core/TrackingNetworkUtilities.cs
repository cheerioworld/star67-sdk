using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Star67.Tracking
{
    public static class TrackingNetworkUtilities
    {
        public static string[] GetLocalIPv4Addresses()
        {
            var addresses = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
                for (int i = 0; i < interfaces.Length; i++)
                {
                    NetworkInterface networkInterface = interfaces[i];
                    if (networkInterface == null || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    IPInterfaceProperties properties = networkInterface.GetIPProperties();
                    if (properties == null)
                    {
                        continue;
                    }

                    UnicastIPAddressInformationCollection unicastAddresses = properties.UnicastAddresses;
                    for (int j = 0; j < unicastAddresses.Count; j++)
                    {
                        IPAddress address = unicastAddresses[j].Address;
                        if (address == null
                            || address.AddressFamily != AddressFamily.InterNetwork
                            || IPAddress.IsLoopback(address))
                        {
                            continue;
                        }

                        addresses.Add(address.ToString());
                    }
                }
            }
            catch
            {
            }

            if (addresses.Count == 0)
            {
                try
                {
                    IPAddress[] hostAddresses = Dns.GetHostAddresses(Dns.GetHostName());
                    for (int i = 0; i < hostAddresses.Length; i++)
                    {
                        IPAddress address = hostAddresses[i];
                        if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
                        {
                            continue;
                        }

                        addresses.Add(address.ToString());
                    }
                }
                catch
                {
                }
            }

            var results = new string[addresses.Count];
            addresses.CopyTo(results);
            Array.Sort(results, StringComparer.Ordinal);
            return results;
        }
    }
}
