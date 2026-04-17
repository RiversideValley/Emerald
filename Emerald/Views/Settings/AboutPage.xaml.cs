using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Notifications;
using Emerald.Helpers;
using Emerald.Models;
using Emerald.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.Logging;
using Windows.ApplicationModel.DataTransfer;
using LocalMessageBoxButtons = Emerald.Helpers.Enums.MessageBoxButtons;

namespace Emerald.Views.Settings;

public sealed partial class AboutPage : Page
{
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

                    if (response == Helpers.Enums.MessageBoxResults.CustomResult1)
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
        => await MessageBox.Show("Credits".Localize(), "CreditsDescription".Localize(), LocalMessageBoxButtons.Ok);

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        => await CheckForUpdatesAsync();

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
}

public sealed record ChannelOption(AppReleaseChannel Channel, string Label)
{
    public override string ToString() => Label;
}
