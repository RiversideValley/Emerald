using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8603 // Possible null reference return.
namespace ClientHandler
{
    class Program
    {
        public static ProcessUtil? ProcessUtil { get; set; }
        static void Main(string[] args)
        {
            var l = GetEnviromentVar("LocalAppData");
            var path = Path.Combine(l, @"Packages\SeaDevs.Launcher.UWP_0dk3ndwmrga1t\LocalState");
            string arguements = "";
            string fileName = "";
            string workdir = "";
            string logs = "";
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
                    logs = reader.GetAttribute("GameLogs");
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

            if (args.Length > 2)
            {
                if (args[2] == "/admin")
                {
                    string appPath =
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory +
                @"\ClientHandler.exe");

                    ProcessStartInfo Restartinfo = new ProcessStartInfo();
                    Restartinfo.Verb = "runas";
                    if (logs != "True")
                    {
                        Restartinfo.WindowStyle = ProcessWindowStyle.Hidden;
                        Restartinfo.CreateNoWindow = true;
                    }
                    Restartinfo.UseShellExecute = true;
                    Restartinfo.FileName = appPath;
                    try
                    {
                        Process.Start(Restartinfo);
                    }
                    catch
                    {
                        Console.WriteLine("User Cancelled the request...");
                        Console.ReadLine();
                    }
                    return;
                }
            }
            //Console.WriteLine(arguements);
            //Console.WriteLine(fileName);
            //Console.WriteLine(workdir);
            var info = new ProcessStartInfo { FileName = fileName, Arguments = arguements, WorkingDirectory = workdir };
            if (logs == "True")
            {
                ProcessUtil = new ProcessUtil(new Process { StartInfo = info });
                // create a thread  
                Thread newWindowThread = new Thread(new ThreadStart(() =>
                {
                    var app = new App();
                    app.InitializeComponent();
                    app.Dispatcher.Invoke(delegate
                    {
                        app.Run();
                    });
                }));

                // set the apartment state  
                newWindowThread.SetApartmentState(ApartmentState.STA);


                // start the thread  
                newWindowThread.Start();
            }
            else
            {
                Process.Start(info);
            }
        }
        static string GetEnviromentVar(string variableName)
        {
            return Environment.GetEnvironmentVariable(variableName);
        }
    }
    public class StartApp
    {
        public StartApp()
        {
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
