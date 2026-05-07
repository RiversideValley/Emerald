using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CmlLib.Core;
using Emerald.CoreX;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Modpacks;
using Emerald.CoreX.Models;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services;
using Emerald.CoreX.Store.Modrinth.JSON;
using Emerald.CoreX.Tests.Support;
using Emerald.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using LauncherCore = Emerald.CoreX.Core;
using GameVersionType = Emerald.CoreX.Versions.Type;

namespace Emerald.CoreX.Tests.Modpacks;

[Collection(IocCollection.Name)]
public sealed class MrPackTests
{
    [Theory]
    [InlineData("fabric-loader", GameVersionType.Fabric, "Fabric")]
    [InlineData("forge", GameVersionType.Forge, "Forge")]
    [InlineData("quilt-loader", GameVersionType.Quilt, "Quilt")]
    [InlineData("neoforge", GameVersionType.NeoForge, "NeoForge")]
    public async Task ReadAsync_MapsSupportedLoaderDependencies(
        string dependencyId,
        GameVersionType expectedType,
        string expectedDisplayName)
    {
        using var temp = new TemporaryDirectory();
        var manifest = CreateManifest(new Dictionary<string, string>
        {
            ["minecraft"] = "1.21.4",
            [dependencyId] = "loader-version"
        });
        var path = WriteMrPack(temp.Path, CreateMrPackBytes(manifest));

        var read = await new MrPackReader().ReadAsync(path);

        Assert.Equal("1.21.4", read.GetMinecraftVersion());
        var loader = read.GetLoaderDependency();
        Assert.Equal(expectedType, loader.Type);
        Assert.Equal("loader-version", loader.Version);
        Assert.Equal(expectedDisplayName, loader.DisplayName);
    }

    [Fact]
    public async Task ReadAsync_UsesVanilla_WhenNoLoaderDependencyExists()
    {
        using var temp = new TemporaryDirectory();
        var path = WriteMrPack(temp.Path, CreateMrPackBytes(CreateManifest()));

        var read = await new MrPackReader().ReadAsync(path);

        var loader = read.GetLoaderDependency();
        Assert.Equal(GameVersionType.Vanilla, loader.Type);
        Assert.Null(loader.Version);
    }

    [Fact]
    public async Task ReadAsync_Throws_WhenMinecraftDependencyIsMissing()
    {
        using var temp = new TemporaryDirectory();
        var manifest = CreateManifest(new Dictionary<string, string>
        {
            ["fabric-loader"] = "0.16.10"
        });
        var path = WriteMrPack(temp.Path, CreateMrPackBytes(manifest));

        await Assert.ThrowsAsync<InvalidOperationException>(() => new MrPackReader().ReadAsync(path));
    }

    [Theory]
    [InlineData("../evil.jar")]
    [InlineData("mods/../evil.jar")]
    [InlineData("/tmp/evil.jar")]
    [InlineData("\\evil.jar")]
    [InlineData("C:/evil.jar")]
    [InlineData("C:\\evil.jar")]
    [InlineData("C:evil.jar")]
    public void PathGuard_RejectsUnsafePaths(string path)
    {
        using var temp = new TemporaryDirectory();

        Assert.Throws<InvalidOperationException>(() =>
            MrPackPathGuard.GetSafeDestinationPath(temp.Path, path));
    }

    [Fact]
    public async Task InstallAsync_InstallsClientFiles_AndAppliesClientOverridesLast()
    {
        using var temp = new TemporaryDirectory();
        var requiredBytes = Encoding.UTF8.GetBytes("required");
        var optionalBytes = Encoding.UTF8.GetBytes("optional");
        var serverBytes = Encoding.UTF8.GetBytes("server");
        var manifest = CreateManifest(files:
        [
            CreateFile("mods/required.jar", "https://example.test/required.jar", requiredBytes, "required"),
            CreateFile("mods/optional.jar", "https://example.test/optional.jar", optionalBytes, "optional"),
            CreateFile("mods/server.jar", "https://example.test/server.jar", serverBytes, "unsupported")
        ]);
        var path = WriteMrPack(temp.Path, CreateMrPackBytes(manifest, new Dictionary<string, string>
        {
            ["overrides/config/app.cfg"] = "base",
            ["client-overrides/config/app.cfg"] = "client",
            ["server-overrides/config/server.cfg"] = "server"
        }));
        var handler = new TestHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.EndsWith("required.jar", StringComparison.Ordinal))
            {
                return Bytes(requiredBytes);
            }

            if (url.EndsWith("optional.jar", StringComparison.Ordinal))
            {
                return Bytes(optionalBytes);
            }

            if (url.EndsWith("server.jar", StringComparison.Ordinal))
            {
                return Bytes(serverBytes);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var installer = CreateFileInstaller(handler);
        var instancePath = Path.Combine(temp.Path, "instance");

        await installer.InstallAsync(path, instancePath);

        Assert.Equal("required", await File.ReadAllTextAsync(Path.Combine(instancePath, "mods", "required.jar")));
        Assert.Equal("optional", await File.ReadAllTextAsync(Path.Combine(instancePath, "mods", "optional.jar")));
        Assert.False(File.Exists(Path.Combine(instancePath, "mods", "server.jar")));
        Assert.Equal("client", await File.ReadAllTextAsync(Path.Combine(instancePath, "config", "app.cfg")));
        Assert.False(File.Exists(Path.Combine(instancePath, "config", "server.cfg")));
        Assert.DoesNotContain(handler.Requests, uri => uri.ToString().EndsWith("server.jar", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InstallAsync_Fails_WhenAllDownloadsFail()
    {
        using var temp = new TemporaryDirectory();
        var bytes = Encoding.UTF8.GetBytes("file");
        var manifest = CreateManifest(files:
        [
            CreateFile(
                "mods/file.jar",
                "https://example.test/one.jar",
                bytes,
                "required",
                "https://example.test/two.jar")
        ]);
        var path = WriteMrPack(temp.Path, CreateMrPackBytes(manifest));
        var installer = CreateFileInstaller(new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var instancePath = Path.Combine(temp.Path, "instance");

        await Assert.ThrowsAsync<InvalidOperationException>(() => installer.InstallAsync(path, instancePath));

        Assert.False(File.Exists(Path.Combine(instancePath, "mods", "file.jar")));
    }

    [Fact]
    public async Task InstallAsync_Fails_WhenHashDoesNotMatch()
    {
        using var temp = new TemporaryDirectory();
        var expectedBytes = Encoding.UTF8.GetBytes("expected");
        var manifest = CreateManifest(files:
        [
            CreateFile("mods/file.jar", "https://example.test/file.jar", expectedBytes, "required")
        ]);
        var path = WriteMrPack(temp.Path, CreateMrPackBytes(manifest));
        var installer = CreateFileInstaller(new TestHttpMessageHandler(_ =>
            Bytes(Encoding.UTF8.GetBytes("actual"))));
        var instancePath = Path.Combine(temp.Path, "instance");

        await Assert.ThrowsAsync<InvalidOperationException>(() => installer.InstallAsync(path, instancePath));

        Assert.False(File.Exists(Path.Combine(instancePath, "mods", "file.jar")));
    }

    [Fact]
    public async Task InstallAsync_RejectsOverrideEntriesEscapingInstanceRoot()
    {
        using var temp = new TemporaryDirectory();
        var path = WriteMrPack(temp.Path, CreateMrPackBytes(CreateManifest(files: []), new Dictionary<string, string>
        {
            ["overrides/../evil.txt"] = "bad"
        }));
        var installer = CreateFileInstaller(new TestHttpMessageHandler(_ => Bytes([])));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            installer.InstallAsync(path, Path.Combine(temp.Path, "instance")));

        Assert.False(File.Exists(Path.Combine(temp.Path, "evil.txt")));
    }

    [Fact]
    public async Task ProbeAsync_UsesPrimaryMrPackFile_AndVerifiesHash()
    {
        using var temp = new TemporaryDirectory();
        var mrPackBytes = CreateMrPackBytes(CreateManifest());
        var handler = new TestHttpMessageHandler(request =>
            request.RequestUri!.ToString().EndsWith("primary.mrpack", StringComparison.Ordinal)
                ? Bytes(mrPackBytes)
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        var service = CreateCreationService(temp.Path, httpClient: new HttpClient(handler));
        var version = CreateItemVersion(
            new ItemFile
            {
                Filename = "fallback.mrpack",
                Url = "https://example.test/fallback.mrpack",
                Primary = false,
                Hashes = CreateItemHashes([])
            },
            new ItemFile
            {
                Filename = "primary.mrpack",
                Url = "https://example.test/primary.mrpack",
                Primary = true,
                Hashes = CreateItemHashes(mrPackBytes)
            });

        var probe = await service.ProbeAsync(version);

        Assert.Equal("1.21.4", probe.MinecraftVersion);
        Assert.Contains(handler.Requests, uri => uri.ToString().EndsWith("primary.mrpack", StringComparison.Ordinal));
        File.Delete(probe.MrPackPath);
    }

    [Fact]
    public async Task CreateAsync_SavesOneGame_OnlyAfterSuccessfulModpackFileInstall()
    {
        using var temp = new TemporaryDirectory();
        var mrPackPath = WriteMrPack(temp.Path, CreateMrPackBytes(CreateManifest(new Dictionary<string, string>
        {
            ["minecraft"] = "1.20.1",
            ["neoforge"] = "21.1.1"
        })));
        var settings = new InMemoryBaseSettingsService();
        var fileInstaller = new FakeMrPackFileInstaller();
        var service = CreateCreationService(temp.Path, settings, fileInstaller);

        var game = await service.CreateAsync(CreateRequest(mrPackPath, "Modded Pack", "modded-pack"));

        Assert.Equal("Modded Pack", game.Version.DisplayName);
        Assert.Equal(GameVersionType.NeoForge, game.Version.Type);
        Assert.Equal("21.1.1", game.Version.ModVersion);
        Assert.Single(service.Core.Games);
        Assert.True(File.Exists(Path.Combine(temp.Path, LauncherCore.GamesFolderName, "modded-pack", "modpack.ok")));
        var saved = settings.Peek<SavedGameCollection[]>(SettingsKeys.SavedGames);
        Assert.NotNull(saved);
        Assert.Single(saved![0].Games);
    }

    [Fact]
    public async Task CreateAsync_RemovesStagingAndSavesNoGame_WhenFileInstallFails()
    {
        using var temp = new TemporaryDirectory();
        var mrPackPath = WriteMrPack(temp.Path, CreateMrPackBytes(CreateManifest()));
        var settings = new InMemoryBaseSettingsService();
        var fileInstaller = new FakeMrPackFileInstaller { ShouldFail = true };
        var service = CreateCreationService(temp.Path, settings, fileInstaller);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(CreateRequest(mrPackPath, "Broken Pack", "broken-pack")));

        Assert.Empty(service.Core.Games);
        Assert.False(Directory.Exists(Path.Combine(temp.Path, LauncherCore.GamesFolderName, "broken-pack")));
        Assert.False(Directory.Exists(Path.Combine(temp.Path, LauncherCore.GamesFolderName, ".emerald-modpack-staging")));
        Assert.Null(settings.Peek<SavedGameCollection[]>(SettingsKeys.SavedGames));
    }

    [Fact]
    public async Task CreateAsync_RejectsExistingTargetFolder()
    {
        using var temp = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, LauncherCore.GamesFolderName, "existing-pack"));
        var mrPackPath = WriteMrPack(temp.Path, CreateMrPackBytes(CreateManifest()));
        var service = CreateCreationService(temp.Path);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(CreateRequest(mrPackPath, "Existing Pack", "existing-pack")));

        Assert.Empty(service.Core.Games);
    }

    [Fact]
    public async Task CreateAsync_RejectsUnsafeTargetFolderName()
    {
        using var temp = new TemporaryDirectory();
        var mrPackPath = WriteMrPack(temp.Path, CreateMrPackBytes(CreateManifest()));
        var service = CreateCreationService(temp.Path);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(CreateRequest(mrPackPath, "Unsafe Pack", "../unsafe-pack")));

        Assert.Empty(service.Core.Games);
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "unsafe-pack")));
    }

    private static MrPackFileInstaller CreateFileInstaller(TestHttpMessageHandler handler)
        => new(
            new MrPackReader(),
            NullLogger<MrPackFileInstaller>.Instance,
            new HttpClient(handler));

    private static TestCreationService CreateCreationService(
        string basePath,
        InMemoryBaseSettingsService? settings = null,
        IMrPackFileInstaller? fileInstaller = null,
        HttpClient? httpClient = null)
    {
        var core = CreateCore(basePath, settings ?? new InMemoryBaseSettingsService());
        var service = new ModpackInstanceCreationService(
            core,
            new MrPackReader(),
            fileInstaller ?? new FakeMrPackFileInstaller(),
            new FakeNotificationService(),
            NullLogger<ModpackInstanceCreationService>.Instance,
            httpClient ?? new HttpClient(new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))));

        return new TestCreationService(core, service);
    }

    private static LauncherCore CreateCore(string basePath, InMemoryBaseSettingsService settings)
    {
        var core = new LauncherCore(
            NullLogger<LauncherCore>.Instance,
            new FakeNotificationService(),
            settings,
            new TestGameRuntimeService(),
            new TestGlobalGameSettingsService());

        typeof(LauncherCore)
            .GetProperty("BasePath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(core, new MinecraftPath(basePath));

        return core;
    }

    private static ModpackInstanceCreationRequest CreateRequest(
        string mrPackPath,
        string instanceName,
        string folderName)
        => new()
        {
            InstanceName = instanceName,
            FolderName = folderName,
            MrPackPath = mrPackPath,
            Project = new StoreItem
            {
                ID = "project",
                Title = instanceName,
                Categories = [],
                Versions = []
            },
            Version = CreateItemVersion(new ItemFile
            {
                Filename = "pack.mrpack",
                Url = "https://example.test/pack.mrpack",
                Primary = true,
                Hashes = CreateItemHashes([])
            })
        };

    private static ItemVersion CreateItemVersion(params ItemFile[] files)
        => new()
        {
            ID = "version",
            Name = "Version",
            VersionNumber = "1.0.0",
            VersionType = "release",
            GameVersions = ["1.21.4"],
            Loaders = ["fabric"],
            Files = files,
            Dependencies = []
        };

    private static MrPackManifest CreateManifest(
        Dictionary<string, string>? dependencies = null,
        List<MrPackFile>? files = null)
        => new()
        {
            FormatVersion = 1,
            Game = "minecraft",
            VersionId = "1.0.0",
            Name = "Test Pack",
            Files = files ??
            [
                CreateFile("mods/default.jar", "https://example.test/default.jar", Encoding.UTF8.GetBytes("default"))
            ],
            Dependencies = dependencies ?? new Dictionary<string, string>
            {
                ["minecraft"] = "1.21.4"
            }
        };

    private static MrPackFile CreateFile(
        string path,
        string url,
        byte[] bytes,
        string client = "required",
        string? fallbackUrl = null)
        => new()
        {
            Path = path,
            Downloads = fallbackUrl == null ? [url] : [url, fallbackUrl],
            FileSize = bytes.Length,
            Hashes = CreateHashes(bytes),
            Environment = new MrPackFileEnvironment
            {
                Client = client,
                Server = client.Equals("unsupported", StringComparison.OrdinalIgnoreCase)
                    ? "required"
                    : "unsupported"
            }
        };

    private static byte[] CreateMrPackBytes(MrPackManifest manifest, IDictionary<string, string>? entries = null)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var index = archive.CreateEntry("modrinth.index.json");
            using (var indexStream = index.Open())
            {
                JsonSerializer.Serialize(indexStream, manifest);
            }

            foreach (var entry in entries ?? new Dictionary<string, string>())
            {
                var zipEntry = archive.CreateEntry(entry.Key);
                using var entryStream = zipEntry.Open();
                var bytes = Encoding.UTF8.GetBytes(entry.Value);
                entryStream.Write(bytes);
            }
        }

        return stream.ToArray();
    }

    private static string WriteMrPack(string directory, byte[] bytes)
    {
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}.mrpack");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static Dictionary<string, string> CreateHashes(byte[] bytes)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["sha1"] = Convert.ToHexString(SHA1.HashData(bytes)).ToLowerInvariant(),
            ["sha512"] = Convert.ToHexString(SHA512.HashData(bytes)).ToLowerInvariant()
        };

    private static Hashes CreateItemHashes(byte[] bytes)
        => new()
        {
            Sha1 = Convert.ToHexString(SHA1.HashData(bytes)).ToLowerInvariant(),
            Sha512 = Convert.ToHexString(SHA512.HashData(bytes)).ToLowerInvariant()
        };

    private static HttpResponseMessage Bytes(byte[] bytes)
        => new(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        };

    private sealed record TestCreationService(LauncherCore Core, ModpackInstanceCreationService Service)
    {
        public Task<ModpackProbeResult> ProbeAsync(ItemVersion version)
            => Service.ProbeAsync(version);

        public Task<Game> CreateAsync(ModpackInstanceCreationRequest request)
            => Service.CreateAsync(request);
    }

    private sealed class FakeMrPackFileInstaller : IMrPackFileInstaller
    {
        public bool ShouldFail { get; init; }

        public async Task InstallAsync(
            string mrPackPath,
            string instancePath,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(instancePath);
            await File.WriteAllTextAsync(Path.Combine(instancePath, "modpack.ok"), "ok", cancellationToken);
            progress?.Report(100);

            if (ShouldFail)
            {
                throw new InvalidOperationException("file install failed");
            }
        }
    }

    private sealed class TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class TestGlobalGameSettingsService : IGlobalGameSettingsService
    {
        public GameSettings Settings { get; } = new();

        public GameSettings CloneCurrent()
            => Settings.Clone();

        public void Save()
        {
        }
    }

    private sealed class TestGameRuntimeService : IGameRuntimeService
    {
        public ObservableCollection<GameSession> Sessions { get; } = [];

        public GameSession? FindLatestSession(string gamePath)
            => null;

        public Task<GameSession?> LaunchAsync(Game game, EAccount? account = null)
            => Task.FromResult<GameSession?>(null);

        public Task StopAsync(Game game, GameStopMode mode)
            => Task.CompletedTask;

        public GameSession? TryGetActiveSession(Game game)
            => null;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"emerald-modpack-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
