using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmlLib.Core;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Emerald.CoreX.Installers;

public enum ModLoaderResolutionMode { LocalOnly, Online }
public enum ModLoaderResolutionFailure { None, MissingLocalVersion, UnsupportedLoader, InstallerFailed, Cancelled }
public sealed record ModLoaderResolutionResult(bool Success, string? ResolvedVersion, ModLoaderResolutionFailure Failure, string? Message = null);

public class ModLoaderRouter
{
    public readonly IEnumerable<IModLoaderInstaller> Installers;

    public ModLoaderRouter(IEnumerable<IModLoaderInstaller>? installers = null)
    {
       Installers = installers ?? Ioc.Default.GetServices<IModLoaderInstaller>();
    }

    public async Task<string?> RouteAndInitializeAsync(
        MinecraftPath path,
        Versions.Version version,
        bool online = true,
        string? installedVersion = null)
    {
        if (!online && !string.IsNullOrWhiteSpace(installedVersion))
        {
            return installedVersion;
        }

        if (version.Type == Versions.Type.Vanilla)
            return version.BasedOn;

       return await Installers.First(x=> x.Type == version.Type).InstallAsync(path, version.BasedOn, version.ModVersion, online);
    }

    public async Task<ModLoaderResolutionResult> ResolveAsync(
        MinecraftPath path,
        Versions.Version version,
        ModLoaderResolutionMode mode,
        string? installedVersion = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(installedVersion))
        {
            var json = Path.Combine(path.Versions, installedVersion, installedVersion + ".json");
            if (File.Exists(json)) return new(true, installedVersion, ModLoaderResolutionFailure.None);
        }

        if (version.Type == Versions.Type.Vanilla)
        {
            if (mode == ModLoaderResolutionMode.Online || File.Exists(Path.Combine(path.Versions, version.BasedOn, version.BasedOn + ".json")))
                return new(true, version.BasedOn, ModLoaderResolutionFailure.None);
            return new(false, null, ModLoaderResolutionFailure.MissingLocalVersion, "Vanilla version metadata is not available locally.");
        }

        if (mode == ModLoaderResolutionMode.LocalOnly)
            return new(false, null, ModLoaderResolutionFailure.MissingLocalVersion, "The loader is not installed locally; network installers were not invoked.");

        var installer = Installers.FirstOrDefault(x => x.Type == version.Type);
        if (installer == null)
            return new(false, null, ModLoaderResolutionFailure.UnsupportedLoader, $"No installer supports {version.Type}.");

        try
        {
            var resolved = await installer.InstallAsync(path, version.BasedOn, version.ModVersion, true);
            return string.IsNullOrWhiteSpace(resolved)
                ? new(false, null, ModLoaderResolutionFailure.InstallerFailed, "The loader installer did not return a version.")
                : new(true, resolved, ModLoaderResolutionFailure.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(false, null, ModLoaderResolutionFailure.Cancelled, "Loader resolution was cancelled.");
        }
        catch (Exception ex)
        {
            return new(false, null, ModLoaderResolutionFailure.InstallerFailed, ex.Message);
        }
    }
}
