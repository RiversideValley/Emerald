using System.Linq;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services;
using Emerald.CoreX.Services.Auth;
using Emerald.CoreX.Services.Auth.ElyBy;
using Emerald.CoreX.Services.Auth.Microsoft;
using Emerald.CoreX.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emerald.CoreX.Tests.Services;

[Collection(IocCollection.Name)]
public sealed class AccountServiceTests
{
    [Fact]
    public void RequireMicrosoftAccountForOfflineAccounts_IsDisabled()
    {
        var service = CreateService(new InMemoryBaseSettingsService());

        Assert.False(service.RequireMicrosoftAccountForOfflineAccounts);
    }

    [Fact]
    public void RequireMicrosoftAccountForElyByAccounts_IsDisabled()
    {
        var service = CreateService(new InMemoryBaseSettingsService());

        Assert.False(service.RequireMicrosoftAccountForElyByAccounts);
    }

    [Fact]
    public void CreateOfflineAccount_WithoutMicrosoftAccount_CreatesAccount()
    {
        var service = CreateService(new InMemoryBaseSettingsService());

        service.CreateOfflineAccount("Alpha");

        Assert.Contains(service.Accounts, account => account.Type == AccountType.Offline && account.Name == "Alpha");
    }

    [Fact]
    public void SetSelectedAccount_OfflineWithoutMicrosoftAccount_SelectsAccount()
    {
        var service = CreateService(new InMemoryBaseSettingsService());
        var offline = new EAccount("Alpha", AccountType.Offline);
        service.Accounts.Add(offline);

        service.SetSelectedAccount(offline);

        Assert.Same(offline, service.GetSelectedAccount());
    }

    [Fact]
    public async Task AuthenticateAccountAsync_OfflineWithoutMicrosoftAccount_Authenticates()
    {
        var service = CreateService(new InMemoryBaseSettingsService());
        var offline = new EAccount("Alpha", AccountType.Offline);
        service.Accounts.Add(offline);

        var result = await service.AuthenticateAccountAsync(offline);

        Assert.Equal("Alpha", result.Session.Username);
    }

    [Fact]
    public async Task AuthenticateLaunchAccountAsync_OfflineFallback_CreatesOfflineAccountFromSelectedAccountName()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        var microsoftClient = new FakeMicrosoftAccountClient();
        var service = CreateService(baseSettingsService, microsoftClient);
        var microsoft = new EAccount("Alpha", AccountType.Microsoft, "alpha-uuid", "ms-alpha");
        service.Accounts.Add(microsoft);
        service.SetSelectedAccount(microsoft);

        var result = await service.AuthenticateLaunchAccountAsync(microsoft, useOfflineFallback: true);

        Assert.Equal("Alpha", result.Session.Username);
        Assert.Empty(microsoftClient.AuthenticatedIdentifiers);
        var offline = Assert.Single(service.Accounts, account => account.Type == AccountType.Offline && account.Name == "Alpha");
        Assert.NotEqual(microsoft.UniqueId, offline.UniqueId);

        var storedAccounts = baseSettingsService.Peek<List<EAccount>>(SettingsKeys.MinecraftAccounts);
        Assert.NotNull(storedAccounts);
        Assert.Contains(storedAccounts!, account => account.Type == AccountType.Offline && account.Name == "Alpha");
    }

    [Fact]
    public async Task SignInElyByAccountAsync_WithoutMicrosoftAccount_UsesBrowserOAuth()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        var elyByClient = new FakeElyByAuthClient();
        var oauthBrowser = new FakeElyByOAuthBrowser();
        var service = CreateServiceWithEly(baseSettingsService, elyByClient: elyByClient, elyByOAuthBrowser: oauthBrowser);

        await service.SignInElyByAccountAsync();

        Assert.Single(oauthBrowser.Requests);
        Assert.Equal(["ely-oauth-code"], elyByClient.ExchangeOAuthCodeCalls);
        Assert.Contains(service.Accounts, account => account.Type == AccountType.ElyBy && account.Name == "ElyOAuthPlayer");
    }

    [Fact]
    public async Task SignInElyByAccountAsync_CanceledDuringBrowserAuthorization_DoesNotAddAccount()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        var elyByClient = new FakeElyByAuthClient();
        var authorizationStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oauthBrowser = new FakeElyByOAuthBrowser
        {
            OnAuthorizeAsync = async (_, cancellationToken) =>
            {
                authorizationStarted.SetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new ElyByOAuthAuthorizationResult("unused-code");
            }
        };

        var service = CreateServiceWithEly(baseSettingsService, elyByClient: elyByClient, elyByOAuthBrowser: oauthBrowser);
        using var cancellation = new CancellationTokenSource();

        var signInTask = service.SignInElyByAccountAsync(cancellation.Token);
        await authorizationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => signInTask);
        Assert.Empty(service.Accounts);
        Assert.Empty(elyByClient.ExchangeOAuthCodeCalls);
    }

    [Fact]
    public void SetSelectedAccount_ElyByWithoutMicrosoftAccount_SelectsAccount()
    {
        var service = CreateService(new InMemoryBaseSettingsService());
        var elyBy = new EAccount("ElyAlpha", AccountType.ElyBy, "ely-alpha-uuid", "elyby:ely-alpha-uuid");
        service.Accounts.Add(elyBy);

        service.SetSelectedAccount(elyBy);

        Assert.Same(elyBy, service.GetSelectedAccount());
    }

    [Fact]
    public async Task AuthenticateAccountAsync_ElyByWithoutMicrosoftAccount_Authenticates()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        baseSettingsService.Set(
            SettingsKeys.ElyByAccounts,
            new List<ElyByStoredAccount>
            {
                new()
                {
                    UniqueId = "elyby:ely-alpha-uuid",
                    Name = "ElyAlpha",
                    UUID = "ely-alpha-uuid",
                    AccessToken = "ely-access",
                    ClientToken = "ely-client",
                    LastUsed = DateTime.UtcNow.AddHours(-1)
                }
            });

        var service = CreateServiceWithEly(baseSettingsService, elyByClient: new FakeElyByAuthClient());
        await service.InitializeAsync("client-id");
        await service.LoadAllAccountsAsync();

        var elyBy = Assert.Single(service.Accounts, account => account.Type == AccountType.ElyBy);
        var result = await service.AuthenticateAccountAsync(elyBy);

        Assert.Equal("ElyAlpha", result.Session.Username);
    }

    [Fact]
    public async Task LoadAllAccountsAsync_LoadsOfflineFromSettings_UsesCmlLibForMicrosoft_AndWarnsWhenStoredMicrosoftIsLoggedOut()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        baseSettingsService.Set(
            SettingsKeys.MinecraftAccounts,
            new List<EAccount>
            {
                new("OfflineAlpha", AccountType.Offline, uniqueId: "offline-alpha") { LastUsed = DateTime.UtcNow.AddMinutes(-10) },
                new("LegacyMicrosoft", AccountType.Microsoft, "legacy-uuid", "legacy-id")
            });

        var microsoftClient = new FakeMicrosoftAccountClient();
        microsoftClient.Accounts.Add(new MicrosoftAccountInfo("ms-1", "Microsoft Alpha", "ms-uuid-1", DateTime.UtcNow.AddMinutes(-5)));
        var notificationService = new FakeNotificationService();

        var service = CreateService(baseSettingsService, microsoftClient, notificationService);
        await service.InitializeAsync("test-client");

        await service.LoadAllAccountsAsync();

        Assert.Equal("test-client", microsoftClient.InitializedClientId);
        Assert.Equal(2, service.Accounts.Count);
        Assert.Contains(service.Accounts, account => account.Type == AccountType.Offline && account.UniqueId == "offline-alpha");
        Assert.Contains(service.Accounts, account => account.Type == AccountType.Microsoft && account.UniqueId == "ms-1");

        var storedAccounts = baseSettingsService.Peek<List<EAccount>>(SettingsKeys.MinecraftAccounts);
        Assert.NotNull(storedAccounts);
        Assert.Equal(2, storedAccounts!.Count);
        Assert.Contains(storedAccounts, account => account.Type == AccountType.Offline && account.UniqueId == "offline-alpha");
        Assert.Contains(storedAccounts, account => account.Type == AccountType.Microsoft && account.UniqueId == "ms-1");
        Assert.Single(notificationService.WarningCalls);
        Assert.Contains("LegacyMicrosoft", notificationService.WarningCalls[0].Message);
    }

    [Fact]
    public async Task SignInMicrosoftAccountAsync_SelectsMaterializedAccount_WhenNoSelectionExists()
    {
        var microsoftClient = new FakeMicrosoftAccountClient();
        microsoftClient.OnInteractiveSignInAsync = (client, _) =>
        {
            var identifier = "ms-new";
            client.Accounts.Add(new MicrosoftAccountInfo(identifier, "New Microsoft", identifier, DateTime.UtcNow));
            client.DefaultAccountIdentifier = identifier;
            return Task.FromResult(new MicrosoftInteractiveSignInResult(identifier, "New Microsoft", identifier));
        };

        var service = CreateService(new InMemoryBaseSettingsService(), microsoftClient);
        await service.InitializeAsync("client-id");

        await service.SignInMicrosoftAccountAsync();

        var selectedAccount = service.GetSelectedAccount();
        Assert.NotNull(selectedAccount);
        Assert.Equal("ms-new", selectedAccount!.UniqueId);
        Assert.Equal(AccountType.Microsoft, selectedAccount.Type);
        Assert.Contains(service.Accounts, account => account.UniqueId == "ms-new" && account.IsSelected);
    }

    [Fact]
    public async Task SignInMicrosoftAccountAsync_CanceledDuringInteractiveSignIn_DoesNotMaterializeAccount()
    {
        var signInStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var microsoftClient = new FakeMicrosoftAccountClient
        {
            OnInteractiveSignInAsync = async (_, cancellationToken) =>
            {
                signInStarted.SetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new MicrosoftInteractiveSignInResult("unused-id", "Unused", "unused-id");
            }
        };

        var service = CreateService(new InMemoryBaseSettingsService(), microsoftClient);
        await service.InitializeAsync("client-id");
        using var cancellation = new CancellationTokenSource();

        var signInTask = service.SignInMicrosoftAccountAsync(cancellation.Token);
        await signInStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => signInTask);
        Assert.Empty(service.Accounts);
    }

    [Fact]
    public async Task SignInMicrosoftAccountAsync_Throws_WhenAccountDoesNotMaterialize()
    {
        var microsoftClient = new FakeMicrosoftAccountClient
        {
            OnInteractiveSignInAsync = (_, _) => Task.FromResult(new MicrosoftInteractiveSignInResult("missing-id", "Ghost", "missing-id"))
        };

        var service = CreateService(new InMemoryBaseSettingsService(), microsoftClient);
        await service.InitializeAsync("client-id");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SignInMicrosoftAccountAsync());

        Assert.Equal(
            "Microsoft sign-in completed, but Emerald could not materialize the signed-in account.",
            exception.Message);
    }

    [Fact]
    public async Task RestoreSelectedAccount_UsesMicrosoftIdentifier()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        baseSettingsService.Set(SettingsKeys.SelectedMinecraftAccount, "ms-beta");

        var microsoftClient = new FakeMicrosoftAccountClient();
        microsoftClient.Accounts.Add(new MicrosoftAccountInfo("ms-alpha", "Shared", "uuid-alpha", DateTime.UtcNow.AddMinutes(-10)));
        microsoftClient.Accounts.Add(new MicrosoftAccountInfo("ms-beta", "Shared", "uuid-beta", DateTime.UtcNow));

        var service = CreateService(baseSettingsService, microsoftClient);
        await service.InitializeAsync("client-id");

        await service.LoadAllAccountsAsync();

        var selectedAccount = service.GetSelectedAccount();
        Assert.NotNull(selectedAccount);
        Assert.Equal("ms-beta", selectedAccount!.UniqueId);
        Assert.True(selectedAccount.IsSelected);
    }

    [Fact]
    public async Task AuthenticateAccountAsync_MicrosoftUsesIdentifier_AndUpdatesLastUsed()
    {
        var microsoftClient = new FakeMicrosoftAccountClient();
        microsoftClient.Accounts.Add(new MicrosoftAccountInfo("ms-identifier", "SharedName", "real-uuid", DateTime.UtcNow.AddHours(-1)));

        var service = CreateService(new InMemoryBaseSettingsService(), microsoftClient);
        await service.InitializeAsync("client-id");
        await service.LoadAllAccountsAsync();

        var account = Assert.Single(service.Accounts, candidate => candidate.Type == AccountType.Microsoft);
        account.UUID = "mismatched-uuid";
        var before = account.LastUsed;

        await service.AuthenticateAccountAsync(account);

        Assert.Equal(["ms-identifier"], microsoftClient.AuthenticatedIdentifiers);
        Assert.True(account.LastUsed >= before);
    }

    [Fact]
    public async Task SignInElyByAccountAsync_AddsOAuthStoredAccount_AndSelectsWhenNoSelectionExists()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        var elyByClient = new FakeElyByAuthClient
        {
            ExchangeOAuthCodeResult = new ElyByAuthSession(
                "ElyAlpha",
                "ely-alpha-uuid",
                "ely-access",
                "ely-client",
                "ely-refresh",
                DateTimeOffset.UtcNow.AddHours(1),
                ElyByAuthFlow.OAuth)
        };
        var oauthBrowser = new FakeElyByOAuthBrowser { Code = "browser-code" };

        var service = CreateServiceWithEly(baseSettingsService, elyByClient: elyByClient, elyByOAuthBrowser: oauthBrowser);

        await service.SignInElyByAccountAsync();

        var account = Assert.Single(service.Accounts, account => account.Type == AccountType.ElyBy);
        Assert.Equal("ElyAlpha", account.Name);
        Assert.Equal("ely-alpha-uuid", account.UUID);
        Assert.Equal("elyby:ely-alpha-uuid", account.UniqueId);
        Assert.Equal(AccountProviderIds.ElyBy, account.ProviderId);
        Assert.Same(account, service.GetSelectedAccount());
        Assert.Single(oauthBrowser.Requests);
        Assert.Equal(["browser-code"], elyByClient.ExchangeOAuthCodeCalls);

        var storedElyAccounts = baseSettingsService.Peek<List<ElyByStoredAccount>>(SettingsKeys.ElyByAccounts);
        Assert.NotNull(storedElyAccounts);
        var stored = Assert.Single(storedElyAccounts!);
        Assert.Equal("ely-access", stored.AccessToken);
        Assert.Equal("ely-client", stored.ClientToken);
        Assert.Equal("ely-refresh", stored.RefreshToken);
        Assert.Equal(ElyByAuthFlow.OAuth, stored.AuthFlow);
    }

    [Fact]
    public async Task AuthenticateAccountAsync_ElyByRefreshesExpiredToken_AndAddsAuthlibJavaAgent()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        baseSettingsService.Set(
            SettingsKeys.ElyByAccounts,
            new List<ElyByStoredAccount>
            {
                new()
                {
                    UniqueId = "elyby:ely-alpha-uuid",
                    Name = "ElyAlpha",
                    UUID = "ely-alpha-uuid",
                    AccessToken = "expired-access",
                    ClientToken = "ely-client",
                    LastUsed = DateTime.UtcNow.AddHours(-1)
                }
            });

        var elyByClient = new FakeElyByAuthClient
        {
            ValidateResult = false,
            RefreshResult = new ElyByAuthSession("ElyAlpha", "ely-alpha-uuid", "fresh-access", "ely-client")
        };
        var authlibInjector = new FakeAuthlibInjectorService();
        var service = CreateServiceWithEly(baseSettingsService, elyByClient: elyByClient, authlibInjectorService: authlibInjector);
        await service.InitializeAsync("client-id");
        await service.LoadAllAccountsAsync();
        AddMicrosoftAccount(service);

        var account = Assert.Single(service.Accounts, account => account.Type == AccountType.ElyBy);
        var result = await service.AuthenticateAccountAsync(account);

        Assert.Equal("ElyAlpha", result.Session.Username);
        Assert.Equal("ely-alpha-uuid", result.Session.UUID);
        Assert.Equal("fresh-access", result.Session.AccessToken);
        Assert.Equal("ely-client", result.Session.ClientToken);
        Assert.Equal("msa", result.Session.UserType);
        Assert.Equal([("expired-access", "ely-client")], elyByClient.ValidateCalls);
        Assert.Equal(["elyby:ely-alpha-uuid"], elyByClient.RefreshCalls);
        Assert.Equal(1, authlibInjector.Calls);
        Assert.Contains(result.RuntimeOptions.ExtraJvmArguments, argument => argument.Values.Contains("-javaagent:/fake/authlib-injector.jar=ely.by"));

        var storedElyAccounts = baseSettingsService.Peek<List<ElyByStoredAccount>>(SettingsKeys.ElyByAccounts);
        Assert.NotNull(storedElyAccounts);
        Assert.Equal("fresh-access", Assert.Single(storedElyAccounts!).AccessToken);
    }

    [Fact]
    public async Task AuthenticateAccountAsync_ElyByOAuthRefreshesExpiredAccessToken_AndAddsAuthlibJavaAgent()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        baseSettingsService.Set(
            SettingsKeys.ElyByAccounts,
            new List<ElyByStoredAccount>
            {
                new()
                {
                    UniqueId = "elyby:ely-alpha-uuid",
                    Name = "ElyAlpha",
                    UUID = "ely-alpha-uuid",
                    AccessToken = "expired-oauth-access",
                    ClientToken = "ely-client",
                    RefreshToken = "ely-refresh",
                    AuthFlow = ElyByAuthFlow.OAuth,
                    AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                    LastUsed = DateTime.UtcNow.AddHours(-1)
                }
            });

        var elyByClient = new FakeElyByAuthClient
        {
            RefreshResult = new ElyByAuthSession(
                "ElyAlpha",
                "ely-alpha-uuid",
                "fresh-oauth-access",
                "ely-client",
                "ely-refresh",
                DateTimeOffset.UtcNow.AddHours(1),
                ElyByAuthFlow.OAuth)
        };
        var authlibInjector = new FakeAuthlibInjectorService();
        var service = CreateServiceWithEly(baseSettingsService, elyByClient: elyByClient, authlibInjectorService: authlibInjector);
        await service.InitializeAsync("client-id");
        await service.LoadAllAccountsAsync();

        var account = Assert.Single(service.Accounts, account => account.Type == AccountType.ElyBy);
        var result = await service.AuthenticateAccountAsync(account);

        Assert.Equal("fresh-oauth-access", result.Session.AccessToken);
        Assert.Empty(elyByClient.ValidateCalls);
        Assert.Equal(["elyby:ely-alpha-uuid"], elyByClient.RefreshCalls);
        Assert.Equal(1, authlibInjector.Calls);

        var storedElyAccounts = baseSettingsService.Peek<List<ElyByStoredAccount>>(SettingsKeys.ElyByAccounts);
        Assert.NotNull(storedElyAccounts);
        var stored = Assert.Single(storedElyAccounts!);
        Assert.Equal("fresh-oauth-access", stored.AccessToken);
        Assert.Equal("ely-refresh", stored.RefreshToken);
        Assert.Equal(ElyByAuthFlow.OAuth, stored.AuthFlow);
    }

    [Fact]
    public async Task RemoveAccountAsync_MicrosoftUsesIdentifier_AndReloadsFromBackend()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        var microsoftClient = new FakeMicrosoftAccountClient();
        microsoftClient.Accounts.Add(new MicrosoftAccountInfo("ms-remove", "AccountToRemove", "uuid-remove", DateTime.UtcNow));
        microsoftClient.Accounts.Add(new MicrosoftAccountInfo("ms-keep", "AccountToKeep", "uuid-keep", DateTime.UtcNow.AddMinutes(-5)));

        var service = CreateService(baseSettingsService, microsoftClient);
        await service.InitializeAsync("client-id");
        await service.LoadAllAccountsAsync();

        var removable = Assert.Single(service.Accounts, account => account.UniqueId == "ms-remove");
        removable.UUID = "not-the-identifier";
        service.SetSelectedAccount(removable);

        await service.RemoveAccountAsync(removable);

        Assert.Equal(["ms-remove"], microsoftClient.SignedOutIdentifiers);
        Assert.DoesNotContain(service.Accounts, account => account.UniqueId == "ms-remove");
        Assert.Contains(service.Accounts, account => account.UniqueId == "ms-keep");
        Assert.Null(service.GetSelectedAccount());
        Assert.Null(baseSettingsService.Peek<string>(SettingsKeys.SelectedMinecraftAccount));
    }

    [Fact]
    public void CreateOfflineAccount_FirstAccountBecomesSelected()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        var service = CreateService(baseSettingsService);
        AddMicrosoftAccount(service);

        service.CreateOfflineAccount("Alpha");

        var account = Assert.Single(service.Accounts, account => account.Type == AccountType.Offline);
        Assert.Same(account, service.GetSelectedAccount());
        Assert.Equal(account.UniqueId, baseSettingsService.Peek<string>(SettingsKeys.SelectedMinecraftAccount));
    }

    [Fact]
    public void SelectedAccount_RemainsIndependentFromMostRecentlyUsed()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        var service = CreateService(baseSettingsService);
        AddMicrosoftAccount(service);

        service.CreateOfflineAccount("Alpha");
        service.CreateOfflineAccount("Beta");

        var alpha = service.Accounts.First(account => account.Name == "Alpha");
        var beta = service.Accounts.First(account => account.Name == "Beta");

        alpha.LastUsed = DateTime.UtcNow.AddMinutes(10);
        beta.LastUsed = DateTime.UtcNow.AddMinutes(-10);
        service.SetSelectedAccount(beta);

        Assert.Same(beta, service.GetSelectedAccount());
        Assert.Same(alpha, service.GetMostRecentlyUsedAccount());
        Assert.Equal(beta.UniqueId, baseSettingsService.Peek<string>(SettingsKeys.SelectedMinecraftAccount));
    }

    [Fact]
    public async Task RemoveAccount_ClearsStoredSelection_WhenSelectedAccountIsDeleted()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        var service = CreateService(baseSettingsService);
        AddMicrosoftAccount(service);

        service.CreateOfflineAccount("Alpha");
        var selectedAccount = service.GetSelectedAccount();
        Assert.NotNull(selectedAccount);

        await service.RemoveAccountAsync(selectedAccount!);

        Assert.Null(service.GetSelectedAccount());
        Assert.Null(baseSettingsService.Peek<string>(SettingsKeys.SelectedMinecraftAccount));
    }

    [Fact]
    public void SetSelectedAccount_LegacyAccountWithoutUniqueId_GeneratesIdentifierAndPersistsSelection()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        var service = CreateService(baseSettingsService);
        AddMicrosoftAccount(service);

        service.CreateOfflineAccount("Alpha");
        service.CreateOfflineAccount("Beta");

        var alpha = service.Accounts.First(account => account.Name == "Alpha");
        alpha.UniqueId = string.Empty;

        service.SetSelectedAccount(alpha);

        var selected = service.GetSelectedAccount();
        Assert.NotNull(selected);
        Assert.Equal("Alpha", selected!.Name);
        Assert.False(string.IsNullOrWhiteSpace(alpha.UniqueId));
        Assert.Equal(alpha.UniqueId, baseSettingsService.Peek<string>(SettingsKeys.SelectedMinecraftAccount));

        var storedAccounts = baseSettingsService.Peek<List<EAccount>>(SettingsKeys.MinecraftAccounts);
        Assert.NotNull(storedAccounts);
        Assert.Contains(storedAccounts!, account => account.Name == "Alpha" && !string.IsNullOrWhiteSpace(account.UniqueId));
    }

    private static AccountService CreateService(InMemoryBaseSettingsService baseSettingsService)
        => CreateService(baseSettingsService, new FakeMicrosoftAccountClient(), new FakeNotificationService());

    private static AccountService CreateService(
        InMemoryBaseSettingsService baseSettingsService,
        FakeMicrosoftAccountClient microsoftAccountClient)
        => CreateService(baseSettingsService, microsoftAccountClient, new FakeNotificationService());

    private static AccountService CreateService(
        InMemoryBaseSettingsService baseSettingsService,
        FakeMicrosoftAccountClient microsoftAccountClient,
        FakeNotificationService notificationService,
        FakeElyByAuthClient? elyByClient = null,
        FakeElyByOAuthBrowser? elyByOAuthBrowser = null,
        FakeAuthlibInjectorService? authlibInjectorService = null)
        => new(
            NullLogger<AccountService>.Instance,
            baseSettingsService,
            new ImmediateUiDispatcher(),
            "/tmp/emerald-tests/cml_accounts.json",
            microsoftAccountClient,
            notificationService,
            elyByClient,
            new ElyByAccountStore(baseSettingsService),
            elyByOAuthBrowser ?? new FakeElyByOAuthBrowser(),
            authlibInjectorService ?? new FakeAuthlibInjectorService());

    private static AccountService CreateServiceWithEly(
        InMemoryBaseSettingsService baseSettingsService,
        FakeElyByAuthClient? elyByClient = null,
        FakeElyByOAuthBrowser? elyByOAuthBrowser = null,
        FakeAuthlibInjectorService? authlibInjectorService = null)
        => CreateService(
            baseSettingsService,
            new FakeMicrosoftAccountClient(),
            new FakeNotificationService(),
            elyByClient,
            elyByOAuthBrowser,
            authlibInjectorService);

    private static void AddMicrosoftAccount(AccountService service, string name = "Microsoft")
    {
        var account = new EAccount(name, AccountType.Microsoft, $"{name}-uuid", $"{name}-id");
        service.Accounts.Add(account);
    }
}
