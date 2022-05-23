using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.System.Diagnostics;

namespace SDLauncher_UWP.Helpers
{
    public class Util
    {
        public long? GetMemoryMb()
        {
            try
            {
                var diagnosticInfo = SystemDiagnosticInfo.GetForCurrentSystem();
                SystemMemoryUsageReport systemMemoryUsageReport = SystemDiagnosticInfo.GetForCurrentSystem().MemoryUsage.GetReport();
                //ulong totalMemory = diagnosticInfo.MemoryUsage.GetReport().TotalPhysicalSizeInBytes;
                ulong totalMemory = systemMemoryUsageReport.TotalPhysicalSizeInBytes;

                return (long?)(totalMemory / 1000000);
            }
            catch
            {
                return null;
            }
        }
        public bool CheckInternet()
        {
            bool result = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
            return result;
        }
    }
    
    public class IniFile   // revision 11
    {
        public string Path { get; set; }
        string EXE = Assembly.GetExecutingAssembly().GetName().Name;

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern long WritePrivateProfileString(string Section, string Key, string Value, string FilePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern int GetPrivateProfileString(string Section, string Key, string Default, StringBuilder RetVal, int Size, string FilePath);

        public IniFile(string IniPath = null)
        {
            Path = new FileInfo(IniPath ?? EXE + ".ini").FullName;
        }

        public string Read(string Key, string Section = null)
        {
            var RetVal = new StringBuilder(255);
            GetPrivateProfileString(Section ?? EXE, Key, "", RetVal, 255, Path);
            return RetVal.ToString();
        }

        public void Write(string Key, string Value, string Section = null)
        {
            WritePrivateProfileString(Section ?? EXE, Key, Value, Path);
        }

        public void DeleteKey(string Key, string Section = null)
        {
            Write(Key, null, Section ?? EXE);
        }

        public void DeleteSection(string Section = null)
        {
            Write(null, null, Section ?? EXE);
        }

        public bool KeyExists(string Key, string Section = null)
        {
            return Read(Key, Section).Length > 0;
        }
    }
    public class IniReader
    {
        Dictionary<string, Dictionary<string, string>> ini = new Dictionary<string, Dictionary<string, string>>(StringComparer.InvariantCultureIgnoreCase);

        public IniReader(string file)
        {
            var txt = File.ReadAllText(file);

            Dictionary<string, string> currentSection = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            ini[""] = currentSection;

            foreach (var line in txt.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                   .Where(t => !string.IsNullOrWhiteSpace(t))
                                   .Select(t => t.Trim()))
            {
                if (line.StartsWith(";"))
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                    ini[line.Substring(1, line.LastIndexOf("]") - 1)] = currentSection;
                    continue;
                }

                var idx = line.IndexOf("=");
                if (idx == -1)
                    currentSection[line] = "";
                else
                    currentSection[line.Substring(0, idx)] = line.Substring(idx + 1);
            }
        }

        public string GetValue(string key)
        {
            return GetValue(key, "", "");
        }

        public string GetValue(string key, string section)
        {
            return GetValue(key, section, "");
        }

        public string GetValue(string key, string section, string @default)
        {
            if (!ini.ContainsKey(section))
                return @default;

            if (!ini[section].ContainsKey(key))
                return @default;

            return ini[section][key];
        }

        public string[] GetKeys(string section)
        {
            if (!ini.ContainsKey(section))
                return new string[0];

            return ini[section].Keys.ToArray();
        }

        public string[] GetSections()
        {
            return ini.Keys.Where(t => t != "").ToArray();
        }
    }
    public class KeyCuts
    {

    }
}
