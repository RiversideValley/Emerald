using Emerald.CoreX.Models;
using Xunit;

namespace Emerald.CoreX.Tests.Models;

public sealed class GameSettingsJavaTests
{
    [Fact]
    public void ToMLaunchOption_AutoManagedJava_DoesNotSetJavaPath()
    {
        var settings = new GameSettings
        {
            UseCustomJava = false,
            JavaPath = "/custom/java"
        };

        var option = settings.ToMLaunchOption();

        Assert.Null(option.JavaPath);
    }

    [Fact]
    public void ToMLaunchOption_CustomJava_SetsJavaPath()
    {
        var settings = new GameSettings
        {
            UseCustomJava = true,
            JavaPath = "/custom/java"
        };

        var option = settings.ToMLaunchOption();

        Assert.Equal("/custom/java", option.JavaPath);
    }

    [Fact]
    public void Clone_PreservesJavaSettings()
    {
        var settings = new GameSettings
        {
            UseCustomJava = true,
            JavaPath = "/custom/java"
        };

        var clone = settings.Clone();

        Assert.True(clone.UseCustomJava);
        Assert.Equal("/custom/java", clone.JavaPath);
    }
}
