using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MsiKb
{
    // Reverse-engineered protocol for the MSI Mystic Light keyboard MCU
    // (USB VID_1462 / PID_1601, feature reports, report ID 2, 64 bytes).
    // Reference: gist natanalt/06f1d5854230c788b9b9e7e33ab90b9f (MSI Katana 15 B12V)
    [StructLayout(LayoutKind.Sequential)]
    internal struct SP_DEVICE_INTERFACE_DATA
    {
        public int cbSize;
        public Guid InterfaceClassGuid;
        public uint flags;
        public IntPtr reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct SP_DEVICE_INTERFACE_DETAIL_DATA
    {
        public int cbSize;
        public char DevicePath;
    }

    public static class MsiKeyboard
    {
        private static readonly Guid HidGuid = new Guid("4d1e55b2-f16f-11cf-88cb-001111000030");
        private const uint DIGCF_PRESENT = 0x2;
        private const uint DIGCF_DEVICEINTERFACE = 0x10;
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x1;
        private const uint FILE_SHARE_WRITE = 0x2;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        [DllImport("hid.dll")]
        private static extern bool HidD_SetFeature(IntPtr h, byte[] buf, int len);

        [DllImport("hid.dll")]
        private static extern bool HidD_GetFeature(IntPtr h, byte[] buf, int len);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid cls, string enm, IntPtr parent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr devInfo, ref Guid cls, uint idx, ref SP_DEVICE_INTERFACE_DATA data);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detail, uint len, out uint needed, IntPtr devInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr sa, uint create, uint flags, IntPtr tmpl);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr h);

        public static List<string> FindDevicePaths(string vidPidFilter)
        {
            var paths = new List<string>();
            Guid g = HidGuid;
            IntPtr set = SetupDiGetClassDevs(ref g, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (set == new IntPtr(-1)) return paths;
            try
            {
                uint idx = 0;
                while (true)
                {
                    var data = new SP_DEVICE_INTERFACE_DATA();
                    data.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref g, idx, ref data)) break;
                    idx++;
                    uint needed;
                    SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out needed, IntPtr.Zero);
                    if (needed <= 0) continue;
                    IntPtr detail = Marshal.AllocHGlobal((int)needed);
                    try
                    {
                        Marshal.WriteInt32(detail, Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DETAIL_DATA)));
                        if (SetupDiGetDeviceInterfaceDetail(set, ref data, detail, needed, out needed, IntPtr.Zero))
                        {
                            IntPtr strPtr = new IntPtr(detail.ToInt64() + Marshal.OffsetOf(typeof(SP_DEVICE_INTERFACE_DETAIL_DATA), "DevicePath").ToInt64());
                            string path = Marshal.PtrToStringAuto(strPtr);
                            if (!string.IsNullOrEmpty(path) && (string.IsNullOrEmpty(vidPidFilter) || path.ToLower().Contains(vidPidFilter)))
                                paths.Add(path);
                        }
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            return paths;
        }

        private static IntPtr Open(string path)
        {
            return CreateFile(path, GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, IntPtr.Zero);
        }

        // Send a 64-byte feature report; does the optional read-back after writing.
        public static bool Send(string path, byte[] report)
        {
            byte[] buf = new byte[64];
            Array.Copy(report, buf, Math.Min(report.Length, 64));
            IntPtr h = Open(path);
            if (h == new IntPtr(-1)) return false;
            try
            {
                bool ok = HidD_SetFeature(h, buf, 64);
                // read-back (report ID 1) - sometimes required by the MCU
                byte[] rb = new byte[64];
                rb[0] = 1;
                HidD_GetFeature(h, rb, 64);
                return ok;
            }
            finally { CloseHandle(h); }
        }

        public static byte[] ZoneSelectAllReport()
        {
            byte[] b = new byte[64];
            b[0] = 2;      // report ID
            b[1] = 1;      // packet ID: select zones
            b[2] = 0x0F;   // all 4 zones
            return b;
        }

        public static byte[] DisableEffectReport()
        {
            byte[] b = new byte[64];
            b[0] = 2;      // report ID
            b[1] = 2;      // packet ID: configure effect
            b[2] = 0;      // animation type = Disable
            return b;
        }

        public static byte[] LoadFromFlashReport()
        {
            byte[] b = new byte[64];
            b[0] = 2;      // report ID
            b[1] = 176;    // packet ID: load effects from flash
            return b;
        }

        // Tries every matching device path; returns true if any accepted the write.
        public static bool TryTurnOff()
        {
            var paths = FindDevicePaths("vid_1462&pid_1601");
            foreach (var p in paths)
            {
                if (Send(p, ZoneSelectAllReport()) && Send(p, DisableEffectReport()))
                    return true;
            }
            return false;
        }

        public static bool TryTurnOn()
        {
            var paths = FindDevicePaths("vid_1462&pid_1601");
            foreach (var p in paths)
            {
                if (Send(p, LoadFromFlashReport()))
                    return true;
            }
            return false;
        }
    }
}
