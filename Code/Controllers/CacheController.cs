using System;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Net;

namespace Controllers
{
    public class CacheController
    {
        // The max number of physical addresses.
        const int MAXLEN_PHYSADDR = 8;

        // Define the MIB_IPNETROW structure.
        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_IPNETROW
        {
            [MarshalAs(UnmanagedType.U4)]
            public int dwIndex;
            [MarshalAs(UnmanagedType.U4)]
            public int dwPhysAddrLen;
            [MarshalAs(UnmanagedType.U1)]
            public byte macByte0;
            [MarshalAs(UnmanagedType.U1)]
            public byte macByte1;
            [MarshalAs(UnmanagedType.U1)]
            public byte macByte2;
            [MarshalAs(UnmanagedType.U1)]
            public byte macByte3;
            [MarshalAs(UnmanagedType.U1)]
            public byte macByte4;
            [MarshalAs(UnmanagedType.U1)]
            public byte macByte5;
            [MarshalAs(UnmanagedType.U1)]
            public byte macByte6;
            [MarshalAs(UnmanagedType.U1)]
            public byte macByte7;
            [MarshalAs(UnmanagedType.U4)]
            public int dwAddr;
            [MarshalAs(UnmanagedType.U4)]
            public int dwType;
        }

        // START
        // Declaring functions by platform invoking them from the IP Helper Library.
        [DllImport("IpHlpApi.dll")]
        [return: MarshalAs(UnmanagedType.U4)]
        static extern int GetIpNetTable(
           IntPtr pIpNetTable,
           [MarshalAs(UnmanagedType.U4)]
         ref int pdwSize,
           bool bOrder);

        [DllImport("IpHlpApi.dll", CharSet = CharSet.Auto)]
        internal static extern int CreateIpNetEntry(MIB_IPNETROW pArpEntry);
        [DllImport("IpHlpApi.dll", CharSet = CharSet.Auto)]
        internal static extern int DeleteIpNetEntry(MIB_IPNETROW pArpEntry);
        [DllImport("IpHlpApi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern int FlushIpNetTable(int dwIfIndex);
        [DllImport("IpHlpApi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern int FreeMibTable(IntPtr plpNetTable);

        // The insufficient buffer error.
        const int ERROR_INSUFFICIENT_BUFFER = 122;

        // Global private variables.
        private static int bytesNeeded = 0;
        private static IntPtr buffer;
        private static int numberOfEntries = 0;

        // Global private cache.
        private static MIB_IPNETROW[] cache;

        // Refresh the local arp cache.
        private static void RefreshCache()
        {
            // Integer to store the results.
            int result = GetIpNetTable(IntPtr.Zero, ref bytesNeeded, false);

            if (result != ERROR_INSUFFICIENT_BUFFER)
            {
                throw new Win32Exception(result);
            }

            buffer = IntPtr.Zero;

            try
            {
                buffer = Marshal.AllocCoTaskMem(bytesNeeded);

                result = GetIpNetTable(buffer, ref bytesNeeded, false);

                if (result != 0)
                {
                    throw new Win32Exception(result);
                }

                // Get the length of the buffer from the first 4 bytes.
                // Increment current pointer by SizeOf(int).
                // Allocate an array for the entries.
                IntPtr currentBuffer = new IntPtr(buffer.ToInt64() + Marshal.SizeOf(typeof(int)));
                numberOfEntries = Marshal.ReadInt32(buffer);
                cache = new MIB_IPNETROW[numberOfEntries];

                // Cycle through the entries.
                for (int i = 0; i < numberOfEntries; i++)
                {
                    // Call PtrToStructure, getting the structure information.
                    cache[i] = (MIB_IPNETROW)Marshal.PtrToStructure(new
                       IntPtr(currentBuffer.ToInt64() + (i *
                       Marshal.SizeOf(typeof(MIB_IPNETROW)))), typeof(MIB_IPNETROW));
                }
            }
            finally
            {
                // Free the buffer memory for re-use.
                FreeMibTable(buffer);
            }
        }

        public static void FlushFreezeArp(int adapterIndex, string adapterGateway)
        {
            byte[] gateway = GetCachedAddress(adapterGateway);
            MIB_IPNETROW staticRouter = new MIB_IPNETROW();
            staticRouter.dwIndex = adapterIndex;
            staticRouter.dwPhysAddrLen = 6;
            staticRouter.macByte0 = gateway[0];
            staticRouter.macByte1 = gateway[1];
            staticRouter.macByte2 = gateway[2];
            staticRouter.macByte3 = gateway[3];
            staticRouter.macByte4 = gateway[4];
            staticRouter.macByte5 = gateway[5];
            staticRouter.macByte6 = 0;
            staticRouter.macByte7 = 0;
            staticRouter.dwAddr = BitConverter.ToInt32(IPAddress.Parse(adapterGateway).GetAddressBytes(), 0);
            staticRouter.dwType = 4;

            FlushIpNetTable(adapterIndex);
            CreateIpNetEntry(staticRouter);
        }

        public static byte[] GetCachedAddress(string address)
        {
            RefreshCache();

            byte[] cachedAddress = new byte[] { };

            // Finally, the try block
            // Choose to access the cache array outside of the above loop for ease of access.
            // There should be no issue in doing all of this in one loop.
            for (int i = 0; i < numberOfEntries; i++)
            {
                MIB_IPNETROW row = cache[i];
                IPAddress ip = new IPAddress(BitConverter.GetBytes(row.dwAddr));

                if (ip.ToString() == address)
                {
                    cachedAddress = new byte[] { row.macByte0, row.macByte1, row.macByte2, row.macByte3, row.macByte4, row.macByte5 };
                }
            }
            return cachedAddress;
        }

        private void Reset()
        {
            FreeMibTable(buffer);
            bytesNeeded = 0;
            buffer = IntPtr.Zero;
        }
    }
}
