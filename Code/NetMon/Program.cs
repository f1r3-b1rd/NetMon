using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using PcapDotNet.Core;
using PcapDotNet.Packets;
using PcapDotNet.Packets.Arp;
using Controllers;
using System.Threading;

namespace NetMon
{
    class Program
    {
        // Some global lists.
        static List<string> hwList = new List<string>();
        static List<int> hwCount = new List<int>();
        static List<string> ipList = new List<string>();

        private static string adapterId = "";

        static bool hasAlerted = false;

        static void Main(string[] args)
        {
            // New cache controller.
            byte[] router = CacheController.GetCachedAddress("10.0.0.1");

            Console.WriteLine(AdapterController.GetGateway());

            // Make sure we got something.
            // Byte array length should be the same as a mac address length in bytes.
            if (router != null)
            {
                Console.WriteLine(BytesToString(router));

            }
            else
            {
                Console.WriteLine("Router has no cached address?");
            }

            // Get device list from local machine
            IList<LivePacketDevice> allDevices;
            try
            {
                allDevices = LivePacketDevice.AllLocalMachine;
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine(e.Message);
                allDevices = new List<LivePacketDevice>();
            }

            if (allDevices.Count == 0)
            {
                Console.WriteLine("No interfaces found. Make sure libpcap/WinPcap is properly installed on the local machine.");
                return;
            }

            // Print the list
            Console.WriteLine("\nInterface Device List:");
            for (int i = 0; i != allDevices.Count; i++)
            {
                LivePacketDevice device = allDevices[i];
                Console.Write((i + 1) + ". " + device.Name + "  " + string.Join(", ", device.Addresses));
                if (args.Length > 0)
                {
                    if (args[0] == "-d")
                    {
                        DevicePrint(allDevices[i]);
                    }
                }
                if (device.Description != null)
                    Console.WriteLine(" (" + device.Description + ")");
                else
                    Console.WriteLine(" (No description available)");
            }

            int deviceIndex = 0;
            do
            {
                Console.WriteLine("Enter the interface number (1-" + allDevices.Count + "):");
                string deviceIndexString = Console.ReadLine();
                if (!int.TryParse(deviceIndexString, out deviceIndex) ||
                    deviceIndex < 1 || deviceIndex > allDevices.Count)
                {
                    deviceIndex = 0;
                }
            } while (deviceIndex == 0);

            ThreadStart childref = new ThreadStart(PacketCounter);
            Console.WriteLine("In Main: Creating the Child thread");
            Thread childThread = new Thread(childref);
            childThread.Start();

            // Take the selected adapter
            PacketDevice selectedDevice = allDevices[deviceIndex - 1];
            adapterId = selectedDevice.Name.Split('_')[1];

            // open the device
            using (PacketCommunicator communicator =
                selectedDevice.Open(65536,
                                    PacketDeviceOpenAttributes.Promiscuous,
                                    1000))
            {
                Console.WriteLine("Listening on " + selectedDevice.Description + "...");

                // Start the capture
                communicator.SetFilter("arp");
                communicator.ReceivePackets(0, PacketHandlerArp);
            }
        }

        public static void PacketCounter()
        {
            Console.WriteLine("Counter started...");
            while (true)
            {
                Thread.Sleep(100);
                foreach (int count in hwCount.ToList())
                {
                    if (count > 20)
                    {
                        if (!hasAlerted)
                        {
                            Console.WriteLine("\nWARNING");
                            Console.WriteLine("\t" + hwList[hwCount.IndexOf(count)] + " seems to be attempting to wipe the network cache.");
                            Console.WriteLine("\tHas " + count + " packets sent with same hardware address");

                            Console.WriteLine("Flushing and Freezing...");

                            int id = AdapterController.GetAdapterId(adapterId);
                            string gateway = AdapterController.GetGateway();

                            CacheController.FlushFreezeArp(id, AdapterController.GetGateway());

                            hasAlerted = true;
                            Console.WriteLine();
                        }
                    }
                }
            }
        }

        // Packet handler
        private static void PacketHandlerArp(Packet packet)
        {
            // Console.WriteLine(packet.Timestamp.ToString("yyyy-MM-dd hh:mm:ss.fff") + " length:" + packet.Length);

            ArpDatagram arp = packet.Ethernet.Arp;

            // Populate local hardware address cache
            string senderHWAddr = BytesToString(arp.SenderHardwareAddress.ToArray());
            if (hwList.IndexOf(senderHWAddr) != -1)
            {
                hwCount[hwList.IndexOf(senderHWAddr)]++;
            }
            else
            {
                Console.WriteLine("New Address: " + senderHWAddr);
                hwList.Add(senderHWAddr);
                hwCount.Add(1);
            }
        }

        // Print device information
        private static void DevicePrint(IPacketDevice device)
        {
            // Name
            Console.WriteLine(device.Name);

            // Description
            if (device.Description != null)
                Console.WriteLine("\tDescription: " + device.Description);
            else
                Console.WriteLine("\tDescription: NONE AVAILABLE");

            // Loopback Address
            Console.WriteLine("\tLoopback: " +
                              (((device.Attributes & DeviceAttributes.Loopback) == DeviceAttributes.Loopback)
                                   ? "yes"
                                   : "no"));

            // IP Addresses
            foreach (DeviceAddress address in device.Addresses)
            {
                Console.WriteLine("\tAddress Family: " + address.Address.Family);

                if (address.Address != null)
                    Console.WriteLine("\tAddress: " + address.Address);
                if (address.Netmask != null)
                    Console.WriteLine("\tNetmask: " + address.Netmask);
                if (address.Broadcast != null)
                    Console.WriteLine("\tBroadcast Address: " + address.Broadcast);
                if (address.Destination != null)
                    Console.WriteLine("\tDestination Address: " + address.Destination);
            }
            Console.WriteLine();
        }

        // Convert Bytes to String.
        // For an array.
        private static string BytesToString(byte[] collection, String separator = ":")
        {
            return String.Join(separator, (from lonebyte in collection select lonebyte.ToString("X2")).ToArray());
        }

        private static string IpV4ToString(byte[] collection)
        {
            return String.Join(".", (from lonebyte in collection select (int)lonebyte).ToArray());
        }
    }
}
