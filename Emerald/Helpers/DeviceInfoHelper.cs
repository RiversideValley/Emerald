
using System;
using System.IO;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Emerald.Helpers;


public static class DeviceInfoHelper
{
    // --- Windows P/Invoke Definitions ---
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    // --- macOS P/Invoke Definitions ---
    [DllImport("libc", SetLastError = true)]
    private static extern int sysctlbyname(string name, out long oldp, ref IntPtr oldlenp, IntPtr newp, IntPtr newlen);

    public static int? GetMemoryGB()
    {
        // Adjust IoC call to match your actual setup
        var _logger = Ioc.Default.GetService<ILogger>();
        
        try
        {
            _logger.LogDebug("Getting device memory");
            long totalBytes = 0;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    totalBytes = (long)memStatus.ullTotalPhys;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                IntPtr len = (IntPtr)sizeof(long);
                // hw.memsize returns the physical memory in bytes
                if (sysctlbyname("hw.memsize", out long memSize, ref len, IntPtr.Zero, IntPtr.Zero) == 0)
                {
                    totalBytes = memSize;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                const string path = "/proc/meminfo";
                if (File.Exists(path))
                {
                    using var reader = new StreamReader(path);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.StartsWith("MemTotal:"))
                        {
                            // Format is usually: "MemTotal:       16384000 kB"
                            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2 && long.TryParse(parts[1], out long kb))
                            {
                                totalBytes = kb * 1024; // Convert KB to Bytes
                            }
                            break;
                        }
                    }
                }
            }

            if (totalBytes > 0)
            {
                // Convert to GB and round to the nearest whole number (e.g., 7.9GB becomes 8GB)
                double gb = totalBytes / Math.Pow(1024, 3);
                int memGb = (int)Math.Round(gb);
                
                _logger.LogDebug("Memory: {memGb} GB", memGb);
                return memGb;
            }

            _logger.LogWarning("Failed to determine memory: OS not supported or query failed.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get device memory");
            return null;
        }
    }
}
