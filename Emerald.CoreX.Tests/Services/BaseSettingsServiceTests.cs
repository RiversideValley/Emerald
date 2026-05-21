using Emerald.CoreX;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Services;
using Emerald.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emerald.CoreX.Tests.Services;

public sealed class BaseSettingsServiceTests
{
    [Fact]
    public void Set_WritesHeaderComment_AndReadsCommentedJson()
    {
        using var temp = new TemporaryDirectory();
        var service = CreateService(temp.Path, "Do not edit");

        service.Set("Example", new Dictionary<string, string> { ["Name"] = "Emerald" });

        var filePath = Path.Combine(temp.Path, "Example.json");
        var json = File.ReadAllText(filePath);
        Assert.StartsWith("// Do not edit", json, StringComparison.Ordinal);
        Assert.Equal("Emerald", service.Get("Example", new Dictionary<string, string>())["Name"]);
    }

    [Fact]
    public void Get_SkipsJsonComments()
    {
        using var temp = new TemporaryDirectory();
        var service = CreateService(temp.Path);
        File.WriteAllText(
            Path.Combine(temp.Path, "Example.json"),
            """
            // Do not edit
            {
              "Name": "Commented"
            }
            """);

        var value = service.Get("Example", new Dictionary<string, string>());

        Assert.Equal("Commented", value["Name"]);
    }

    [Fact]
    public void Delete_RemovesSettingsKey()
    {
        using var temp = new TemporaryDirectory();
        var service = CreateService(temp.Path);
        service.Set("Example", new Dictionary<string, string> { ["Name"] = "Emerald" });

        Assert.True(service.Exists("Example"));

        service.Delete("Example");

        Assert.False(service.Exists("Example"));
        Assert.False(File.Exists(Path.Combine(temp.Path, "Example.json")));
    }

    [Fact]
    public void MinecraftBaseSettings_WritesUnderEmeraldData_WithWarningHeader()
    {
        using var temp = new TemporaryDirectory();
        var service = new MinecraftBaseSettingsService(NullLogger<BaseSettingsService>.Instance);

        service.UseBasePath(temp.Path);
        service.Set(SettingsKeys.SavedGames, Array.Empty<SavedGame>());

        var filePath = Path.Combine(
            temp.Path,
            IMinecraftBaseSettingsService.EmeraldDataFolderName,
            $"{SettingsKeys.SavedGames}.json");
        var json = File.ReadAllText(filePath);
        Assert.StartsWith(IMinecraftBaseSettingsService.HeaderComment, json, StringComparison.Ordinal);
        Assert.True(service.Exists(SettingsKeys.SavedGames));
    }

    private static BaseSettingsService CreateService(string path, string? header = null)
        => new(NullLogger<BaseSettingsService>.Instance, path, header);

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"emerald-tests-{Guid.NewGuid():N}");

        public TemporaryDirectory()
            => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
