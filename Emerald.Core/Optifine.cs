using CmlLib.Core;
using Newtonsoft.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model.Optifine;
using ProjBobcat.DefaultComponent.Installer;
using System.Net;

namespace Emerald.Core
{
    public class Optifine
    {
        public event EventHandler<int>? ProgressChanged;

        public MinecraftPath MinecraftPath { get; set; }

        public Optifine(MinecraftPath mcPath)
        {
            MinecraftPath = mcPath;
        }

        public async Task<List<OptifineDownloadVersionModel>> GetOptifineVersions()
        {
            try
            {
                var c = new HttpClient();
                var json = await c.GetStringAsync("https://bmclapi2.bangbang93.com/optifine/versionList");

                c.Dispose();

                return JsonConvert.DeserializeObject<List<OptifineDownloadVersionModel>>(json);
            }
            catch
            {
                return new();
            }
        }

        public async Task<(bool, string)> Save(OptifineDownloadVersionModel model)
        {
            double makePrcent(double value, double CurrentMax, double NextMax) => value * NextMax / CurrentMax;

            try
            {
                var jl = SystemInfoHelper.FindJavaFull();
                var javaResult = new List<string>();

                await foreach (var java in jl)
                {
                    javaResult.Add(java);
                }

                if (!javaResult.Any())
                    return (false, "NoJRE");

                ProgressChanged(this, 0);

                var c = new WebClient();
                await c.DownloadFileTaskAsync("https://optifine.net/download?f=" + model.FileName, MinecraftPath.BasePath + "\\Optifine.jar");

                ProgressChanged(this, 25);

                var ins = new OptifineInstaller()
                {
                    CustomId = model.ToFullVersion(),
                    OptifineDownloadVersion = model,
                    OptifineJarPath = MinecraftPath.BasePath + "\\Optifine.jar",
                    JavaExecutablePath = javaResult.FirstOrDefault(),
                    RootPath = MinecraftPath.BasePath,
                    InheritsFrom = model.McVersion
                };

                void ProgChange(object? sender, ProjBobcat.Event.StageChangedEventArgs e)
                {
                    ProgressChanged?.Invoke(this, 25 + (int)Math.Round(makePrcent(e.Progress, 100, 75)));
                }

                ins.StageChangedEventDelegate += ProgChange;

                await ins.InstallTaskAsync();

                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
