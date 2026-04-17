using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Notifications;
using Emerald.Helpers;
using Emerald.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.Logging;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using LocalMessageBoxButtons = Emerald.Helpers.Enums.MessageBoxButtons;
using LocalMessageBoxResults = Emerald.Helpers.Enums.MessageBoxResults;

namespace Emerald.Views.Settings;

public sealed partial class AboutPage : Page
{
    private readonly IAppUpdateService _updateService;
    private readonly INotificationService _notifications;

    private bool _isCheckingForUpdates;

    public Services.SettingsService SS { get; }

    public string AppVersion => DirectResoucres.AppVersion;
    public string BuildType => DirectResoucres.BuildType;
    public string BuildInfo => $"{DirectResoucres.BuildType} {DirectResoucres.Architecture}";

    public AboutPage()
    {
        SS = Ioc.Default.GetRequiredService<Services.SettingsService>();
        _updateService = Ioc.Default.GetRequiredService<IAppUpdateService>();
        _notifications = Ioc.Default.GetRequiredService<INotificationService>();

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
            var result = await _updateService.CheckForUpdatesAsync(SS.Settings.App.Updates.IncludePreReleases);

            switch (result.Status)
            {
                case AppUpdateStatus.UpdateAvailable:
                {
                    var message = string.Concat(
                        "Version".Localize(), ": ", result.LatestVersion, "\n\n",
                        "ReleaseNotes".Localize(), ":\n\n",
                        string.IsNullOrWhiteSpace(result.ReleaseNotes) ? "-" : result.ReleaseNotes);

                    var response = await MessageBox.Show(
                        "UpdateAvailable".Localize(),
                        message,
                        LocalMessageBoxButtons.CustomWithCancel,
                        "UpdateNow".Localize());

                    if (response == LocalMessageBoxResults.CustomResult1
                        && !string.IsNullOrWhiteSpace(result.ReleaseUrl)
                        && Uri.TryCreate(result.ReleaseUrl, UriKind.Absolute, out var releaseUri))
                    {
                        await Launcher.LaunchUriAsync(releaseUri);
                        _notifications.Info("UsingBrowser".Localize(), result.ReleaseUrl);
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
            $"{"BuildType".Localize()}: {BuildInfo}");
        Clipboard.SetContent(package);
        _notifications.Info("CopyVersion".Localize(), "Ready".Localize());
    }
}
