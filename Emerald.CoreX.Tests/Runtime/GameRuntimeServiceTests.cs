using System.Collections.ObjectModel;
using System.Net;
using CmlLib.Core;
using Emerald.CoreX.Installation;
using Emerald.CoreX.Models;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services;
using Emerald.CoreX.Services.Auth;
using Emerald.CoreX.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emerald.CoreX.Tests.Runtime;

[Collection(IocCollection.Name)]
public sealed class GameRuntimeServiceTests
{
    [Theory]
    [InlineData(AccountType.Microsoft)]
    [InlineData(AccountType.ElyBy)]
    public async Task LaunchAsync_OfflineModeWithNetworkAccount_WarnsAndDoesNotStartLaunch(AccountType accountType)
    {
        using var network = new NetworkCapabilityService(new HttpClient(new NoRequestsHandler()));
        network.ReportFailure(NetworkCapability.MinecraftMetadata, new HttpRequestException("offline"));
        var notifications = new FakeNotificationService();
        var account = new EAccount("Online player", accountType);
        var accounts = new RecordingAccountService(account);
        var installer = new RecordingInstallationService();
        var runtime = new GameRuntimeService(
            NullLogger<GameRuntimeService>.Instance,
            notifications,
            accounts,
            new TestRuntimeSettings(),
            new ImmediateUiDispatcher(),
            installer,
            network);
        var game = new Game(
            new MinecraftPath(Path.Combine(Path.GetTempPath(), "emerald-runtime-tests", Guid.NewGuid().ToString("N"))),
            new Emerald.CoreX.Versions.Version
            {
                DisplayName = "Test",
                BasedOn = "test",
                ReleaseType = "release"
            },
            globalGameSettingsService: new TestGlobalGameSettingsService());

        var session = await runtime.LaunchAsync(game);

        Assert.Null(session);
        Assert.Equal(0, installer.PrepareLaunchCalls);
        Assert.Equal(0, accounts.AuthenticationCalls);
        var warning = Assert.Single(notifications.WarningCalls);
        Assert.Equal("Offline account required", warning.Title);
        Assert.Contains("Select an offline account", warning.Message);
    }

    private sealed class NoRequestsHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new Xunit.Sdk.XunitException("The offline-account guard should not send HTTP requests.");
    }

    private sealed class TestRuntimeSettings : IGameRuntimeSettings
    {
        public bool IsLogCaptureEnabled => false;
    }

    private sealed class TestGlobalGameSettingsService : IGlobalGameSettingsService
    {
        public Emerald.CoreX.Models.GameSettings Settings { get; } = new();
        public Emerald.CoreX.Models.GameSettings CloneCurrent() => Settings.Clone();
        public void LoadForBasePath(string basePath) { }
        public void Save() { }
    }

    private sealed class RecordingAccountService(EAccount selected) : IAccountService
    {
        public ObservableCollection<EAccount> Accounts { get; } = [selected];
        public IReadOnlyList<AccountProviderDescriptor> Providers { get; } =
        [
            new(AccountProviderIds.Microsoft, "Microsoft", [], RequiresNetworkForLaunch: true),
            new(AccountProviderIds.ElyBy, "Ely.by", [], RequiresNetworkForLaunch: true)
        ];
        public int AuthenticationCalls { get; private set; }

        public AccountProviderUsability GetProviderUsability(string providerId) => AccountProviderUsability.Available;
        public AccountProviderUsability GetAccountUsability(EAccount account) => AccountProviderUsability.Available;
        public Task LoadAllAccountsAsync() => Task.CompletedTask;
        public Task<EAccount> SignInAsync(string providerId, AccountSignInRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RefreshAccountAsync(EAccount account, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveAccountAsync(EAccount account) => throw new NotSupportedException();
        public Task<GameAuthenticationResult> AuthenticateAccountAsync(EAccount account) => AuthenticateAsync();
        public Task<GameAuthenticationResult> AuthenticateLaunchAccountAsync(EAccount account, bool useOfflineFallback) => AuthenticateAsync();
        public EAccount? GetMostRecentlyUsedAccount() => selected;
        public EAccount? GetSelectedAccount() => selected;
        public void SetSelectedAccount(EAccount? account) => throw new NotSupportedException();
        public Task InitializeAsync() => Task.CompletedTask;

        private Task<GameAuthenticationResult> AuthenticateAsync()
        {
            AuthenticationCalls++;
            throw new Xunit.Sdk.XunitException("Authentication should not run for a network account in offline mode.");
        }
    }

    private sealed class RecordingInstallationService : IInstanceInstallationService
    {
        public int PrepareLaunchCalls { get; private set; }

        public Task<InstanceInstallResult> InstallAsync(Game game, IProgress<InstallationProgress>? progress = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<InstanceInstallResult> RepairAsync(Game game, IProgress<InstallationProgress>? progress = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<InstanceIntegrityReport> VerifyAsync(Game game, IntegrityCheckLevel level, IProgress<InstallationProgress>? progress = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<InstanceIntegrityReport?> VerifyWhenIdleAsync(Game game, IntegrityCheckLevel level, IProgress<InstallationProgress>? progress = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<LaunchReadinessResult> PrepareLaunchAsync(Game game, CancellationToken cancellationToken = default)
        {
            PrepareLaunchCalls++;
            throw new Xunit.Sdk.XunitException("Launch preflight should not run after the offline-account guard.");
        }
    }
}
