using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Notifications;
using Emerald.Helpers;
using Emerald.Models;
using Emerald.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Uno.Logging;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using LocalMessageBoxButtons = Emerald.Helpers.Enums.MessageBoxButtons;
using LocalMessageBoxResults = Emerald.Helpers.Enums.MessageBoxResults;

namespace Emerald.Views.Settings;

public sealed partial class AboutPage : Page
{
    private const string NightlyArtifactsFallbackUrl = "https://github.com/RiversideValley/Emerald/actions/workflows/ci.yml?query=branch%3Amain";

    private static readonly IReadOnlyList<DependencyCreditGroup> DependencyCredits =
    [
        new(
            "App platform",
            [
                new(
                    "Microsoft .NET",
                    ".NET target frameworks, host abstractions, dependency injection, and shared runtime libraries.",
                    "https://github.com/dotnet/dotnet",
                    "MIT",
                    "https://licenses.nuget.org/MIT",
                    "net10.0, net10.0-windows10.0.26100, net10.0-desktop"),
                new(
                    "Uno Platform",
                    "Cross-platform WinUI app framework, desktop host, Skia renderer, localization, themes, and Lottie support.",
                    "https://github.com/unoplatform/uno",
                    "Apache-2.0",
                    "https://licenses.nuget.org/Apache-2.0",
                    "Uno.Sdk, Uno.WinUI, Uno.WinUI.Runtime.Skia.*, Uno.WinUI.Lottie"),
                new(
                    "Uno Extensions",
                    "Application builder, hosting, localization, storage, configuration, and logging integration.",
                    "https://github.com/unoplatform/uno.extensions",
                    "Apache-2.0",
                    "https://licenses.nuget.org/Apache-2.0",
                    "Uno.Extensions.*"),
                new(
                    "Uno Toolkit UI",
                    "Toolkit resources and WinUI-styled controls used by the Uno shell.",
                    "https://github.com/unoplatform/uno.toolkit.ui",
                    "Apache-2.0",
                    "https://licenses.nuget.org/Apache-2.0",
                    "Uno.Toolkit, Uno.Toolkit.WinUI"),
                new(
                    "SkiaSharp",
                    "Native graphics, Skia-backed rendering, and Skottie animation support pulled in by the desktop renderer.",
                    "https://github.com/mono/SkiaSharp",
                    "MIT",
                    "https://licenses.nuget.org/MIT",
                    "SkiaSharp, SkiaSharp.Views.Uno.WinUI, SkiaSharp.Skottie")
            ]),
        new(
            "Launcher core",
            [
                new(
                    "CmlLib.Core",
                    "Minecraft version metadata, file checks, downloads, Java resolution, and launch process construction.",
                    "https://github.com/CmlLib/CmlLib.Core",
                    "MIT",
                    "https://licenses.nuget.org/MIT",
                    "CmlLib.Core"),
                new(
                    "CmlLib.Core.Auth.Microsoft",
                    "Microsoft account session storage and Minecraft account authentication helpers.",
                    "https://github.com/CmlLib/CmlLib.Core.Auth.Microsoft",
                    "MIT",
                    "https://licenses.nuget.org/MIT",
                    "CmlLib.Core.Auth.Microsoft"),
                new(
                    "CmlLib.Core.Installer.Forge",
                    "Forge mod loader installation metadata and installer support.",
                    "https://github.com/CmlLib/CmlLib.Core.Installer.Forge",
                    "MIT",
                    "https://licenses.nuget.org/MIT",
                    "CmlLib.Core.Installer.Forge"),
                new(
                    "CmlLib.Core.Commons",
                    "Shared CmlLib support library resolved by the Minecraft launcher packages.",
                    "https://github.com/CmlLib/CmlLib.Core.Commons",
                    "MIT",
                    "https://licenses.nuget.org/MIT",
                    "CmlLib.Core.Commons")
            ]),
        new(
            "Accounts and auth",
            [
                new(
                    "XboxAuthNet.Game.Msal",
                    "Xbox and Microsoft OAuth flow used by the CmlLib Microsoft account client.",
                    "https://github.com/AlphaBs/XboxAuthNet",
                    "MIT",
                    "https://licenses.nuget.org/MIT",
                    "XboxAuthNet.Game.Msal, XboxAuthNet.Game, XboxAuthNet"),
                new(
                    "Microsoft Authentication Library",
                    "MSAL token acquisition and secure account cache support resolved through XboxAuthNet.",
                    "https://github.com/AzureAD/microsoft-authentication-library-for-dotnet",
                    "MIT",
                    "https://licenses.nuget.org/MIT",
                    "Microsoft.Identity.Client, Microsoft.Identity.Client.Extensions.Msal")
            ]),
        new(
            "UI and app patterns",
            [
                new(
                    "CommunityToolkit.Mvvm",
                    "Observable models, relay commands, source generators, and Ioc helpers used across UI and CoreX.",
                    "https://github.com/CommunityToolkit/dotnet",
                    "MIT",
                    "https://licenses.nuget.org/MIT",
                    "CommunityToolkit.Mvvm"),
                new(
                    "Windows Community Toolkit",
                    "Settings cards, WinUI helpers, sizers, converters, triggers, and supporting controls.",
                    "https://github.com/CommunityToolkit/Windows",
                    "MIT",
                    "https://licenses.nuget.org/MIT",
                    "CommunityToolkit.WinUI.Controls.*, CommunityToolkit.WinUI.Converters, CommunityToolkit.WinUI.Helpers"),
                new(
                    "Microsoft.Windows.CsWin32",
                    "Win32 interop source generation for platform-specific shell and window behavior.",
                    "https://github.com/microsoft/CsWin32",
                    "MIT",
                    "https://licenses.nuget.org/MIT",
                    "Microsoft.Windows.CsWin32")
            ]),
        new(
            "Data, web, and logs",
            [
                new(
                    "RestSharp",
                    "HTTP client used by the Modrinth store integrations.",
                    "https://github.com/restsharp/RestSharp",
                    "Apache-2.0",
                    "https://licenses.nuget.org/Apache-2.0",
                    "RestSharp"),
                new(
                    "Newtonsoft.Json",
                    "JSON serialization and parsing for launcher data and API payloads.",
                    "https://github.com/JamesNK/Newtonsoft.Json",
                    "MIT",
                    "https://licenses.nuget.org/MIT",
                    "Newtonsoft.Json"),
                new(
                    "Microsoft.Extensions.Logging",
                    "Structured logging abstractions used by the app, CoreX services, runtime, and notifications.",
                    "https://github.com/dotnet/dotnet",
                    "MIT",
                    "https://licenses.nuget.org/MIT",
                    "Microsoft.Extensions.Logging, Microsoft.Extensions.Logging.Console"),
                new(
                    "Serilog",
                    "File, console, and debug logging pipeline configured by the Uno host.",
                    "https://github.com/serilog/serilog",
                    "Apache-2.0",
                    "https://licenses.nuget.org/Apache-2.0",
                    "Serilog, Serilog.Sinks.File, Serilog.Sinks.Console, Serilog.Sinks.Debug")
            ])
    ];

    private readonly IAppUpdateService _updateService;
    private readonly INotificationService _notifications;
    private readonly List<ChannelOption> _availableChannels;

    private bool _isCheckingForUpdates;

    public Services.SettingsService SS { get; }

    public string AppVersion => DirectResoucres.PublicVersion;
    public string PackageVersion => DirectResoucres.PackageVersion;
    public string BuildTypeLabel => $"{DirectResoucres.BuildType} | {GetChannelLabel(DirectResoucres.ReleaseChannel)}";
    public string BuildInfo => $"{GetChannelLabel(DirectResoucres.ReleaseChannel)} {DirectResoucres.Architecture}";

    public IReadOnlyList<ChannelOption> AvailableChannels => _availableChannels;
    public Visibility NightlyArtifactsCardVisibility =>
        SS.Settings.App.Updates.PreferredChannel == AppReleaseChannel.Nightly
            ? Visibility.Visible
            : Visibility.Collapsed;

    public ChannelOption SelectedUpdateChannel
    {
        get
        {
            var selectedChannel = SS.Settings.App.Updates.PreferredChannel;
            return _availableChannels.FirstOrDefault(option => option.Channel == selectedChannel) ?? _availableChannels[0];
        }
        set
        {
            if (value is null)
            {
                return;
            }

            if (SS.Settings.App.Updates.PreferredChannel != value.Channel)
            {
                SS.Settings.App.Updates.PreferredChannel = value.Channel;
                Bindings.Update();
            }
        }
    }

    public AboutPage()
    {
        SS = Ioc.Default.GetRequiredService<Services.SettingsService>();
        _updateService = Ioc.Default.GetRequiredService<IAppUpdateService>();
        _notifications = Ioc.Default.GetRequiredService<INotificationService>();

        _availableChannels =
        [
            new ChannelOption(AppReleaseChannel.Nightly, "UpdateChannelNightly".Localize()),
            new ChannelOption(AppReleaseChannel.Prerelease, "UpdateChannelPrerelease".Localize()),
            new ChannelOption(AppReleaseChannel.Release, "UpdateChannelRelease".Localize())
        ];

        InitializeComponent();
        Bindings.Update();
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_isCheckingForUpdates)
        {
            return;
        }

        _isCheckingForUpdates = true;
        CheckUpdatesButton.IsEnabled = false;
        var operation = _notifications.Create("CheckForUpdates".Localize(), "CheckingUpdates".Localize(), isIndeterminate: true);

        try
        {
            var preferredChannel = SS.Settings.App.Updates.PreferredChannel;
            var result = await _updateService.CheckForUpdatesAsync(preferredChannel);

            switch (result.Status)
            {
                case AppUpdateStatus.UpdateAvailable:
                {
                    var message = string.Concat(
                        "Version".Localize(), ": ", result.LatestPublicVersion, "\n",
                        "PackageVersion".Localize(), ": ", result.LatestPackageVersion, "\n",
                        "UpdateChannel".Localize(), ": ", GetChannelLabel(result.LatestChannel ?? preferredChannel), "\n\n",
                        "ReleaseNotes".Localize(), ":\n\n",
                        string.IsNullOrWhiteSpace(result.ReleaseNotes) ? "-" : result.ReleaseNotes);

                    var response = await MessageBox.Show(
                        "UpdateAvailable".Localize(),
                        message,
                        LocalMessageBoxButtons.CustomWithCancel,
                        "InstallUpdate".Localize());

                    if (response == LocalMessageBoxResults.CustomResult1)
                    {
                        var installResult = await _updateService.TryInstallUpdateAsync(result);
                        if (!installResult.Succeeded)
                        {
                            await MessageBox.Show("Error".Localize(), installResult.Message ?? "CheckForUpdates".Localize(), LocalMessageBoxButtons.Ok);
                            _notifications.Complete(operation.Id, false, installResult.Message ?? "CheckForUpdates".Localize(), installResult.Error);
                            return;
                        }
                    }

                    _notifications.Complete(operation.Id, true, "UpdateAvailable".Localize());
                    break;
                }
                case AppUpdateStatus.UpToDate:
                    await MessageBox.Show("NoUpdatesAvailable".Localize(), "NoUpdates".Localize(), LocalMessageBoxButtons.Ok);
                    _notifications.Complete(operation.Id, true, "NoUpdates".Localize());
                    break;
                case AppUpdateStatus.LocalBuildIsNewer:
                    await MessageBox.Show("DowngradeAvailable".Localize(), "DowngradeDescription".Localize(), LocalMessageBoxButtons.Ok);
                    _notifications.Complete(operation.Id, true, "DowngradeAvailable".Localize());
                    break;
                case AppUpdateStatus.ManualDownloadRequired:
                {
                    var response = await MessageBox.Show(
                        "NightlyManualUpdateTitle".Localize(),
                        result.ErrorMessage ?? "NightlyManualUpdateDescription".Localize(),
                        LocalMessageBoxButtons.CustomWithCancel,
                        "OpenNightlyArtifacts".Localize());

                    if (response == LocalMessageBoxResults.CustomResult1)
                    {
                        var opened = await OpenUrlAsync(result.PreferredInstallUri ?? NightlyArtifactsFallbackUrl);
                        if (!opened)
                        {
                            await MessageBox.Show("Error".Localize(), "CouldNotOpenNightlyArtifacts".Localize(), LocalMessageBoxButtons.Ok);
                        }
                    }

                    _notifications.Complete(operation.Id, true, "NightlyManualUpdateTitle".Localize());
                    break;
                }
                default:
                    await MessageBox.Show("Error".Localize(), result.ErrorMessage ?? "CheckForUpdates".Localize(), LocalMessageBoxButtons.Ok);
                    _notifications.Complete(operation.Id, false, result.ErrorMessage ?? "CheckForUpdates".Localize());
                    break;
            }
        }
        catch (Exception ex)
        {
            this.Log().LogError(ex, "Update check failed.");
            await MessageBox.Show("Error".Localize(), ex.Message, LocalMessageBoxButtons.Ok);
            _notifications.Complete(operation.Id, false, ex.Message, ex);
        }
        finally
        {
            _isCheckingForUpdates = false;
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private void Version_Click(object sender, RoutedEventArgs e)
        => VersionInfoTip.IsOpen = true;

    private async void VersionInfoTip_ActionButtonClick(TeachingTip sender, object args)
    {
        sender.IsOpen = false;
        await CheckForUpdatesAsync();
    }

    private void VersionInfoTip_CloseButtonClick(TeachingTip sender, object args)
    {
        sender.IsOpen = false;
        CopyVersionToClipboard();
    }

    private async void Credits_Click(object sender, RoutedEventArgs e)
        => await ShowCreditsDialogAsync();

    private async Task ShowCreditsDialogAsync()
    {
        var content = CreateCreditsContent();
        var dialog = content.ToContentDialog("Credits".Localize(), "Close".Localize());
        dialog.Resources["ContentDialogMaxWidth"] = 860d;
        dialog.Resources["ContentDialogMaxHeight"] = 760d;

        try
        {
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            this.Log().LogWarning(ex, "Credits dialog failed to open.");
            await MessageBox.Show("Credits".Localize(), "CreditsDescription".Localize(), LocalMessageBoxButtons.Ok);
        }
    }

    private static StackPanel CreateCreditsContent()
    {
        var root = new StackPanel
        {
            Spacing = 16,
            MaxWidth = 760
        };

        root.Children.Add(new TextBlock
        {
            Text = "CreditsDialogIntro".Localize(),
            TextWrapping = TextWrapping.WrapWholeWords
        });

        foreach (var group in DependencyCredits)
        {
            var section = new StackPanel { Spacing = 8 };
            section.Children.Add(new TextBlock
            {
                Text = group.Title,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.WrapWholeWords
            });

            foreach (var credit in group.Credits)
            {
                section.Children.Add(CreateCreditCard(credit));
            }

            root.Children.Add(section);
        }

        return root;
    }

    private static Border CreateCreditCard(DependencyCredit credit)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = credit.Name,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords
        });
        panel.Children.Add(new TextBlock
        {
            Text = credit.Usage,
            Foreground = GetThemeBrush("ApplicationSecondaryForegroundThemeBrush"),
            TextWrapping = TextWrapping.WrapWholeWords
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{"CreditsPackages".Localize()}: {credit.Packages}",
            FontSize = 12,
            Foreground = GetThemeBrush("ApplicationSecondaryForegroundThemeBrush"),
            TextWrapping = TextWrapping.WrapWholeWords
        });

        var links = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        links.Children.Add(CreateCreditLink("Source".Localize(), credit.SourceUrl));
        links.Children.Add(CreateCreditLink($"{credit.LicenseName} {"License".Localize()}", credit.LicenseUrl));
        panel.Children.Add(links);

        return new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(8),
            Background = GetThemeBrush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = GetThemeBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            Child = panel
        };
    }

    private static HyperlinkButton CreateCreditLink(string label, string rawUrl)
        => new()
        {
            Content = label,
            NavigateUri = new Uri(rawUrl),
            Padding = new Thickness(0)
        };

    private static Brush? GetThemeBrush(string key)
        => Application.Current.Resources[key] as Brush;

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        => await CheckForUpdatesAsync();

    private async void OpenNightlyArtifacts_Click(object sender, RoutedEventArgs e)
    {
        var result = await _updateService.CheckForUpdatesAsync(AppReleaseChannel.Nightly);
        var opened = await OpenUrlAsync(result.PreferredInstallUri ?? NightlyArtifactsFallbackUrl);
        if (!opened)
        {
            await MessageBox.Show("Error".Localize(), "CouldNotOpenNightlyArtifacts".Localize(), LocalMessageBoxButtons.Ok);
        }
    }

    private void CopyVersionToClipboard()
    {
        var package = new DataPackage
        {
            RequestedOperation = DataPackageOperation.Copy
        };
        package.SetText(
            $"{"Version".Localize()}: {AppVersion}\n" +
            $"{"PackageVersion".Localize()}: {PackageVersion}\n" +
            $"{"BuildType".Localize()}: {BuildInfo}");
        Clipboard.SetContent(package);
        _notifications.Info("CopyVersion".Localize(), "Ready".Localize());
    }

    private static string GetChannelLabel(AppReleaseChannel channel)
    {
        return channel switch
        {
            AppReleaseChannel.Nightly => "UpdateChannelNightly".Localize(),
            AppReleaseChannel.Prerelease => "UpdateChannelPrerelease".Localize(),
            _ => "UpdateChannelRelease".Localize()
        };
    }

    private static async Task<bool> OpenUrlAsync(string rawUrl)
    {
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return await Launcher.LaunchUriAsync(uri);
    }
}

public sealed record ChannelOption(AppReleaseChannel Channel, string Label)
{
    public override string ToString() => Label;
}

public sealed record DependencyCreditGroup(string Title, IReadOnlyList<DependencyCredit> Credits);

public sealed record DependencyCredit(
    string Name,
    string Usage,
    string SourceUrl,
    string LicenseName,
    string LicenseUrl,
    string Packages);
