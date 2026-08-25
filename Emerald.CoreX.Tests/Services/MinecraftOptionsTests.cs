using System.Text;
using Emerald.CoreX.GameOptions;
using Xunit;

namespace Emerald.CoreX.Tests.Services;

public sealed class MinecraftOptionsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "emerald-options-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Catalog_UsesVerifiedShapes_AndPreservesEnumTokens()
    {
        var language = new Dictionary<string, string>
        {
            ["options.fov"] = "Field of View",
            ["key.forward"] = "Forward",
            ["key.keyboard.w"] = "W"
        };

        var fov = MinecraftOptionCatalog.Create("fov", "0.5", language)!;
        Assert.Equal(MinecraftOptionType.IntSlider, fov.Type);
        Assert.Equal(90, fov.SliderValue);
        fov.SliderValue = 110;
        Assert.Equal("1", fov.RawValue);

        var graphics = MinecraftOptionCatalog.Create("graphicsMode", "2", language)!;
        Assert.Equal(MinecraftOptionType.Enum, graphics.Type);

        var mainHand = MinecraftOptionCatalog.Create("mainHand", "\"right\"", language)!;
        Assert.Equal(MinecraftOptionType.Enum, mainHand.Type);
        Assert.Equal("\"right\"", mainHand.SelectedEnumOption!.RawValue);

        var binding = MinecraftOptionCatalog.Create("key_key.forward", "key.keyboard.w", language)!;
        Assert.Equal(MinecraftOptionType.KeyBind, binding.Type);
        Assert.Contains("W", binding.DisplayName);
        Assert.Equal("key.keyboard.w", binding.DisplayValueLabel);

        var future = MinecraftOptionCatalog.Create("newFutureSetting", "surprise", language)!;
        Assert.Equal(MinecraftOptionType.ReadOnly, future.Type);
    }

    [Fact]
    public async Task Document_PatchesOnlyLastOccurrence_AndRetainsFormatting()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "options.txt");
        var source = "# keep\r\nfov:0.0\nmalformed\r\nfov:0.5\r\nserver:host:25565";
        await File.WriteAllBytesAsync(path, Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(source)).ToArray());

        var document = await MinecraftOptionsDocument.ReadAsync(path, CancellationToken.None);
        var patched = document.PatchLastOccurrences(new Dictionary<string, string> { ["fov"] = "1" });
        await patched.WriteReplacingAsync(path, CancellationToken.None);

        var actual = await File.ReadAllTextAsync(path, new UTF8Encoding(true));
        Assert.Equal("# keep\r\nfov:0.0\nmalformed\r\nfov:1\r\nserver:host:25565", actual);
    }

    [Fact]
    public async Task Document_PreservesBytes_WhenNotPatched()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "options.txt");
        var source = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("a:one\r\n\r\nb:two")).ToArray();
        await File.WriteAllBytesAsync(path, source);

        var document = await MinecraftOptionsDocument.ReadAsync(path, CancellationToken.None);
        await document.WriteReplacingAsync(path, CancellationToken.None);

        Assert.Equal(source, await File.ReadAllBytesAsync(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
