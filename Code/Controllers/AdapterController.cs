using System;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Linq;

namespace Controllers
{
    public class AdapterController
    {
        public static int GetAdapterId(string adapterId)
        {
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();

            Console.WriteLine("IPv4 interface information for {0}.{1}",
                          properties.HostName, properties.DomainName);

            int id = 0;
            foreach (NetworkInterface adapter in nics)
            {
                //Console.WriteLine(adapter.Description);
                //Console.WriteLine(adapter.Name);
                if (adapter.Supports(NetworkInterfaceComponent.IPv4) == false)
                {
                    continue;
                }

                if (!adapter.Id.Equals(adapterId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                IPInterfaceProperties adapterProperties = adapter.GetIPProperties();
                IPv4InterfaceProperties p = adapterProperties.GetIPv4Properties();
                if (p == null)
                {
                    Console.WriteLine("No information is available for this interface.");
                    continue;
                }
                id = adapter.GetIPProperties().GetIPv4Properties().Index;
            }
            return id;
        }

        public static string GetGateway()
        {
            string gateway = "";
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();


            foreach (NetworkInterface f in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (f.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (GatewayIPAddressInformation d in f.GetIPProperties().GatewayAddresses)
                    {
                        if(d.Address.ToString().Length <= 15)
                        {
                            gateway = d.Address.ToString();
                        }
                    }
                }
            }
            return gateway;
        }
    }
}
