using System;
using System.IO;
using System.Xml;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ClientLauncher
{
    class Program
    {
        static void Main(string[] args)
        {

            if (args.Length > 2)
            {
                if (args[2] == "/admin")
                {
                    string aliasPath =
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory +
                @"\ClientLauncher.exe");

                    ProcessStartInfo Restartinfo = new ProcessStartInfo();
                    Restartinfo.Verb = "runas";
                    Restartinfo.UseShellExecute = true;
                    Restartinfo.FileName = aliasPath;
                    Process.Start(Restartinfo);
                    return;
                }
            }
            Console.Title = "SDLauncher Loging System";
            var l = GetEnviromentVar("LocalAppData");
            var path = Path.Combine(l, @"Packages\SeaDevs.Launcher.UWP_0dk3ndwmrga1t\LocalState");
            string arguements = "";
            string fileName = "";
            string workdir = "";
            using (StreamReader sr = File.OpenText(Path.Combine(path, "StartInfo.xml")))
            {
                var s = sr.BaseStream;

                XmlReaderSettings settings = new XmlReaderSettings();
                settings.Async = true;
                using (XmlReader reader = XmlReader.Create(s, settings))
                {
                    reader.Read();
                    reader.ReadStartElement("Process");
                    reader.ReadToFollowing("StartInfo");
                    arguements = reader.GetAttribute("Arguments");
                    fileName = reader.GetAttribute("FileName");
                    workdir = reader.GetAttribute("WorkingDirectory");
                    reader.Close();
                }
                try
                {
                    s.Close();
                    sr.Close();
                }
                catch
                {

                }
            }
            //Console.WriteLine(arguements);
            //Console.WriteLine(fileName);
            //Console.WriteLine(workdir);
            var info = new ProcessStartInfo { FileName = fileName, Arguments = arguements, WorkingDirectory = workdir };
            var proc = new Process { StartInfo = info };
            var processUtil = new ProcessUtil(proc);
            processUtil.OutputReceived += (s, e) => Console.WriteLine(e);
            processUtil.StartWithEvents();
            proc.WaitForExit();
        }
        static string GetEnviromentVar(string variableName)
        {
            return Environment.GetEnvironmentVariable(variableName);
        }
    }
    public class ProcessUtil
    {
        public event EventHandler<string>? OutputReceived;
        public event EventHandler? Exited;

        public Process Process { get; private set; }

        public ProcessUtil(Process process)
        {
            this.Process = process;
        }

        public void StartWithEvents()
        {
            Process.StartInfo.CreateNoWindow = true;
            Process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            Process.StartInfo.UseShellExecute = false;
            Process.StartInfo.RedirectStandardError = true;
            Process.StartInfo.RedirectStandardOutput = true;
            Process.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
            Process.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
            Process.EnableRaisingEvents = true;
            Process.ErrorDataReceived += (s, e) => OutputReceived?.Invoke(this, e.Data ?? "");
            Process.OutputDataReceived += (s, e) => OutputReceived?.Invoke(this, e.Data ?? "");
            Process.Exited += (s, e) => Exited?.Invoke(this, new EventArgs());

            Process.Start();
            Process.BeginErrorReadLine();
            Process.BeginOutputReadLine();
        }

        public Task WaitForExitTaskAsync()
        {
            return Task.Run(() =>
            {
                Process.WaitForExit();
            });
        }
    }
}
