using System.Linq;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services;
using Emerald.CoreX.Services.Auth;
using Emerald.CoreX.Services.Auth.ElyBy;
using Emerald.CoreX.Services.Auth.Microsoft;
using Emerald.CoreX.Services.Auth.Offline;
using Emerald.CoreX.Services.Auth.OAuth;
using Emerald.CoreX.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emerald.CoreX.Tests.Services;

[Collection(IocCollection.Name)]
public sealed class AccountServiceTests
{
    [Fact]
    public async Task CustomProvider_CanLoadSignInRefreshAuthenticateAndRemove_WithoutServiceChanges()
    {
        var provider = new RecordingAccountProvider();
        var service = CreateService(new InMemoryBaseSettingsService(), new IAccountProvider[] { provider });
        await service.InitializeAsync();
        await service.LoadAllAccountsAsync();

        var account = await service.SignInAsync("test", new AccountSignInRequest("test-browser"));
        await service.RefreshAccountAsync(account);
        await service.AuthenticateAccountAsync(account);
        await service.RemoveAccountAsync(account);

        Assert.True(provider.Loaded);
        Assert.True(provider.SignedIn);
        Assert.True(provider.Refreshed);
        Assert.True(provider.Authenticated);
        Assert.True(provider.Removed);
        Assert.Equal("manage-test", service.Providers.Single().EffectiveActions.Single().ActionId);
        Assert.Empty(service.Accounts);
    }

    [Fact]
    public void DuplicateProviderIds_AreRejected()
    {
        var settings = new InMemoryBaseSettingsService();
        var exception = Assert.Throws<ArgumentException>(() =>
            CreateService(settings, new IAccountProvider[]
            {
                new OfflineAccountProvider(new AccountProviderPolicyOptions()),
                new OfflineAccountProvider(new AccountProviderPolicyOptions())
            }));

        Assert.Contains(AccountProviderIds.Offline, exception.Message);
    }

    [Fact]
    public void BuiltInProviderRegistration_AllowsMissingMicrosoftClientId()
    {
        var services = new ServiceCollection();

        var exception = Record.Exception(() => services.AddEmeraldAccountProviders(string.Empty));

        Assert.Null(exception);
    }

    [Fact]
    public async Task MissingMicrosoftClientId_PreservesAccountsButMarksProviderUnavailable()
    {
        var settings = new InMemoryBaseSettingsService();
        settings.Set(
            SettingsKeys.MinecraftAccounts,
            new List<EAccount>
            {
                new("Stored Microsoft", AccountType.Microsoft, "ms-uuid", "ms-id")
                {
                    ProviderId = AccountProviderIds.Microsoft
                }
            });
        settings.Set(SettingsKeys.SelectedMinecraftAccount, "ms-id");

        var microsoftClient = new FakeMicrosoftAccountClient();
        var policyOptions = new AccountProviderPolicyOptions
        {
            RequireMicrosoftForOfflineAccounts = true,
            RequireMicrosoftForElyByAccounts = false
        };
        var service = CreateService(
            settings,
            microsoftClient,
            new FakeNotificationService(),
            providers:
            [
                new OfflineAccountProvider(policyOptions),
                new MicrosoftAccountProvider(microsoftClient, string.Empty)
            ],
            policyOptions: policyOptions);

        await service.InitializeAsync();
        await service.LoadAllAccountsAsync();

        Assert.Null(microsoftClient.InitializedClientId);
        var account = Assert.Single(service.Accounts);
        Assert.Equal(AccountAvailability.Error, account.Availability);
        Assert.Contains("not configured", account.AvailabilityMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(service.GetProviderUsability(AccountProviderIds.Microsoft).IsAvailable);
        Assert.False(service.GetAccountUsability(account).IsAvailable);
        Assert.False(service.GetProviderUsability(AccountProviderIds.Offline).IsAvailable);
        Assert.Null(service.GetSelectedAccount());

        var signInException = await Assert.ThrowsAsync<InvalidOperationException>(() => SignInMicrosoftAsync(service));
        Assert.Contains("not configured", signInException.Message, StringComparison.OrdinalIgnoreCase);

        await service.RemoveAccountAsync(account);

        Assert.Empty(service.Accounts);
        Assert.Empty(microsoftClient.SignedOutIdentifiers);
    }

    [Fact]
    public void ProviderRequirements_AreConfiguredByProviderPolicy()
    {
        var service = CreateService(
            new InMemoryBaseSettingsService(),
            policyOptions: new AccountProviderPolicyOptions
            {
                RequireMicrosoftForOfflineAccounts = true,
                RequireMicrosoftForElyByAccounts = true
            });

        Assert.False(service.GetProviderUsability(AccountProviderIds.Offline).IsAvailable);
        Assert.False(service.GetProviderUsability(AccountProviderIds.ElyBy).IsAvailable);
        Assert.Contains(
            service.Providers.Single(provider => provider.ProviderId == AccountProviderIds.Offline).EffectiveRequirements,
            requirement => requirement.ProviderId == AccountProviderIds.Microsoft);
    }

    [Fact]
    public async Task ProviderRequirements_AreEnforcedForGenericSignIn()
    {
        var service = CreateService(
            new InMemoryBaseSettingsService(),
            policyOptions: new AccountProviderPolicyOptions { RequireMicrosoftForOfflineAccounts = true });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SignInOfflineAsync(service, "Alpha"));

        Assert.Contains("Microsoft", exception.Message);
    }

    [Fact]
    public void ProviderRequirements_AreEnforcedForSelection_AndReactToAccountChanges()
    {
        var service = CreateService(
            new InMemoryBaseSettingsService(),
            policyOptions: new AccountProviderPolicyOptions { RequireMicrosoftForOfflineAccounts = true });
        var offline = new EAccount("Alpha", AccountType.Offline);
        service.Accounts.Add(offline);

        Assert.Throws<InvalidOperationException>(() => service.SetSelectedAccount(offline));

        AddMicrosoftAccount(service);
        Assert.True(service.GetProviderUsability(AccountProviderIds.Offline).IsAvailable);
        service.SetSelectedAccount(offline);
        Assert.Same(offline, service.GetSelectedAccount());
    }

    [Fact]
    public async Task RemovingRequirementAccount_ClearsNowInvalidSelection()
    {
        var service = CreateService(
            new InMemoryBaseSettingsService(),
            policyOptions: new AccountProviderPolicyOptions { RequireMicrosoftForOfflineAccounts = true });
        var microsoft = new EAccount("Microsoft", AccountType.Microsoft, "ms-uuid", "ms-id");
        var offline = new EAccount("Alpha", AccountType.Offline);
        service.Accounts.Add(microsoft);
        service.Accounts.Add(offline);
        service.SetSelectedAccount(offline);

        await service.RemoveAccountAsync(microsoft);

        Assert.Null(service.GetSelectedAccount());
        Assert.False(service.GetProviderUsability(AccountProviderIds.Offline).IsAvailable);
    }

    [Fact]
    public async Task SignInAsync_RejectsMethodsNotAdvertisedByProvider()
    {
        var provider = new RecordingAccountProvider();
        var service = CreateService(new InMemoryBaseSettingsService(), new IAccountProvider[] { provider });

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SignInAsync("test", new AccountSignInRequest("not-advertised")));

        Assert.Contains("does not expose", exception.Message);
        Assert.False(provider.SignedIn);
    }

    [Fact]
    public async Task SignInAsync_OfflineWithoutMicrosoftRequirement_CreatesAccount()
    {
        var service = CreateService(new InMemoryBaseSettingsService());

        await SignInOfflineAsync(service, "Alpha");

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
    public async Task SignInAsync_ElyByWithoutMicrosoftRequirement_UsesBrowserOAuth()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        var elyByClient = new FakeElyByAuthClient();
        var oauthBrowser = new FakeElyByOAuthBrowser();
        var service = CreateServiceWithEly(baseSettingsService, elyByClient: elyByClient, elyByOAuthBrowser: oauthBrowser);

        await SignInElyByAsync(service);

        Assert.Single(oauthBrowser.Requests);
        Assert.Equal(["ely-oauth-code"], elyByClient.ExchangeOAuthCodeCalls);
        Assert.Contains(service.Accounts, account => account.Type == AccountType.ElyBy && account.Name == "ElyOAuthPlayer");
    }

    [Fact]
    public async Task SignInAsync_ElyByCanceledDuringBrowserAuthorization_DoesNotAddAccount()
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
                return new BrowserOAuthAuthorizationResult("unused-code");
            }
        };

        var service = CreateServiceWithEly(baseSettingsService, elyByClient: elyByClient, elyByOAuthBrowser: oauthBrowser);
        using var cancellation = new CancellationTokenSource();

        var signInTask = SignInElyByAsync(service, cancellation.Token);
        await authorizationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => signInTask);
        Assert.Empty(service.Accounts);
        Assert.Empty(elyByClient.ExchangeOAuthCodeCalls);
    }

    [Fact]
    public async Task SetSelectedAccount_ElyByWithoutMicrosoftRequirement_SelectsStoredAccount()
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
                    ClientToken = "ely-client"
                }
            });
        var service = CreateService(baseSettingsService);
        await service.LoadAllAccountsAsync();
        var elyBy = Assert.Single(service.Accounts);

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
        await service.InitializeAsync();
        await service.LoadAllAccountsAsync();

        var elyBy = Assert.Single(service.Accounts, account => account.Type == AccountType.ElyBy);
        var result = await service.AuthenticateAccountAsync(elyBy);

        Assert.Equal("ElyAlpha", result.Session.Username);
    }

    [Fact]
    public async Task UnconfiguredElyBy_DisablesNewSignIn_ButDoesNotStrandLegacyDirectAccount()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        baseSettingsService.Set(
            SettingsKeys.ElyByAccounts,
            new List<ElyByStoredAccount>
            {
                new()
                {
                    UniqueId = "elyby:legacy-uuid",
                    Name = "LegacyEly",
                    UUID = "legacy-uuid",
                    AccessToken = "legacy-access",
                    ClientToken = "legacy-client",
                    AuthFlow = ElyByAuthFlow.Direct
                }
            });
        var service = CreateService(
            baseSettingsService,
            new FakeMicrosoftAccountClient(),
            new FakeNotificationService(),
            elyByClient: new FakeElyByAuthClient { ValidateResult = true },
            elyByOAuthOptions: new ElyByOAuthOptions(string.Empty, string.Empty, string.Empty));
        await service.LoadAllAccountsAsync();
        var account = Assert.Single(service.Accounts);

        Assert.False(service.GetProviderUsability(AccountProviderIds.ElyBy).IsAvailable);
        Assert.True(service.GetAccountUsability(account).IsAvailable);
        await service.RefreshAccountAsync(account);
        await Assert.ThrowsAsync<InvalidOperationException>(() => SignInElyByAsync(service));
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
        await service.InitializeAsync();

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
    public async Task SignInAsync_MicrosoftSelectsMaterializedAccount_WhenNoSelectionExists()
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
        await service.InitializeAsync();

        await SignInMicrosoftAsync(service);

        var selectedAccount = service.GetSelectedAccount();
        Assert.NotNull(selectedAccount);
        Assert.Equal("ms-new", selectedAccount!.UniqueId);
        Assert.Equal(AccountType.Microsoft, selectedAccount.Type);
        Assert.Contains(service.Accounts, account => account.UniqueId == "ms-new" && account.IsSelected);
    }

    [Fact]
    public async Task SignInAsync_MicrosoftCanceledDuringInteractiveSignIn_DoesNotMaterializeAccount()
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
        await service.InitializeAsync();
        using var cancellation = new CancellationTokenSource();

        var signInTask = SignInMicrosoftAsync(service, cancellation.Token);
        await signInStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => signInTask);
        Assert.Empty(service.Accounts);
    }

    [Fact]
    public async Task SignInAsync_MicrosoftThrows_WhenAccountDoesNotMaterialize()
    {
        var microsoftClient = new FakeMicrosoftAccountClient
        {
            OnInteractiveSignInAsync = (_, _) => Task.FromResult(new MicrosoftInteractiveSignInResult("missing-id", "Ghost", "missing-id"))
        };

        var service = CreateService(new InMemoryBaseSettingsService(), microsoftClient);
        await service.InitializeAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => SignInMicrosoftAsync(service));

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
        await service.InitializeAsync();

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
        await service.InitializeAsync();
        await service.LoadAllAccountsAsync();

        var account = Assert.Single(service.Accounts, candidate => candidate.Type == AccountType.Microsoft);
        account.UUID = "mismatched-uuid";
        var before = account.LastUsed;

        await service.AuthenticateAccountAsync(account);

        Assert.Equal(["ms-identifier"], microsoftClient.AuthenticatedIdentifiers);
        Assert.True(account.LastUsed >= before);
    }

    [Fact]
    public async Task SignInAsync_ElyByAddsOAuthStoredAccount_AndSelectsWhenNoSelectionExists()
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

        await SignInElyByAsync(service);

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
        await service.InitializeAsync();
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
        await service.InitializeAsync();
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
        await service.InitializeAsync();
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
    public async Task SignInAsync_FirstOfflineAccountBecomesSelected()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        var service = CreateService(baseSettingsService);
        AddMicrosoftAccount(service);

        await SignInOfflineAsync(service, "Alpha");

        var account = Assert.Single(service.Accounts, account => account.Type == AccountType.Offline);
        Assert.Same(account, service.GetSelectedAccount());
        Assert.Equal(account.UniqueId, baseSettingsService.Peek<string>(SettingsKeys.SelectedMinecraftAccount));
    }

    [Fact]
    public async Task SelectedAccount_RemainsIndependentFromMostRecentlyUsed()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        var service = CreateService(baseSettingsService);
        AddMicrosoftAccount(service);

        await SignInOfflineAsync(service, "Alpha");
        await SignInOfflineAsync(service, "Beta");

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

        await SignInOfflineAsync(service, "Alpha");
        var selectedAccount = service.GetSelectedAccount();
        Assert.NotNull(selectedAccount);

        await service.RemoveAccountAsync(selectedAccount!);

        Assert.Null(service.GetSelectedAccount());
        Assert.Null(baseSettingsService.Peek<string>(SettingsKeys.SelectedMinecraftAccount));
    }

    [Fact]
    public async Task SetSelectedAccount_LegacyAccountWithoutUniqueId_GeneratesIdentifierAndPersistsSelection()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        var service = CreateService(baseSettingsService);
        AddMicrosoftAccount(service);

        await SignInOfflineAsync(service, "Alpha");
        await SignInOfflineAsync(service, "Beta");

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
        AccountProviderPolicyOptions policyOptions)
        => CreateService(
            baseSettingsService,
            new FakeMicrosoftAccountClient(),
            new FakeNotificationService(),
            policyOptions: policyOptions);

    private static AccountService CreateService(
        InMemoryBaseSettingsService baseSettingsService,
        IEnumerable<IAccountProvider> providers)
        => CreateService(baseSettingsService, new FakeMicrosoftAccountClient(), new FakeNotificationService(), providers: providers);

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
        FakeAuthlibInjectorService? authlibInjectorService = null,
        ElyByOAuthOptions? elyByOAuthOptions = null,
        IEnumerable<IAccountProvider>? providers = null,
        AccountProviderPolicyOptions? policyOptions = null)
    {
        policyOptions ??= new AccountProviderPolicyOptions
        {
            RequireMicrosoftForOfflineAccounts = false,
            RequireMicrosoftForElyByAccounts = false
        };
        providers ??=
        [
            new OfflineAccountProvider(policyOptions),
            new MicrosoftAccountProvider(microsoftAccountClient, "test-client"),
            new ElyByAccountProvider(
                new ElyByAccountStore(baseSettingsService),
                elyByClient ?? new FakeElyByAuthClient(),
                elyByOAuthBrowser ?? new FakeElyByOAuthBrowser(),
                authlibInjectorService ?? new FakeAuthlibInjectorService(),
                elyByOAuthOptions ?? new ElyByOAuthOptions(
                    "test-client",
                    "test-secret",
                    "http://127.0.0.1:48157/oauth/elyby/callback"),
                policyOptions,
                NullLogger<ElyByAccountProvider>.Instance)
        ];

        return new AccountService(
            NullLogger<AccountService>.Instance,
            baseSettingsService,
            new ImmediateUiDispatcher(),
            providers,
            "/tmp/emerald-tests/cml_accounts.json",
            notificationService);
    }

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
            authlibInjectorService: authlibInjectorService);

    private static void AddMicrosoftAccount(AccountService service, string name = "Microsoft")
    {
        var account = new EAccount(name, AccountType.Microsoft, $"{name}-uuid", $"{name}-id");
        service.Accounts.Add(account);
    }

    private static Task<EAccount> SignInOfflineAsync(
        AccountService service,
        string username,
        CancellationToken cancellationToken = default)
        => service.SignInAsync(
            AccountProviderIds.Offline,
            new AccountSignInRequest(OfflineAccountProvider.CreateMethodId, username),
            cancellationToken);

    private static Task<EAccount> SignInMicrosoftAsync(
        AccountService service,
        CancellationToken cancellationToken = default)
        => service.SignInAsync(
            AccountProviderIds.Microsoft,
            new AccountSignInRequest(MicrosoftAccountProvider.BrowserMethodId),
            cancellationToken);

    private static Task<EAccount> SignInElyByAsync(
        AccountService service,
        CancellationToken cancellationToken = default)
        => service.SignInAsync(
            AccountProviderIds.ElyBy,
            new AccountSignInRequest(ElyByAccountProvider.BrowserMethodId),
            cancellationToken);

    private sealed class RecordingAccountProvider : IAccountProvider
    {
        private readonly EAccount _account = new("Test player", AccountType.Other, "test-uuid", "test-account")
        {
            ProviderId = "test"
        };

        public AccountProviderDescriptor Descriptor { get; } = new(
            "test",
            "Test provider",
            [new AccountSignInMethodDescriptor("test-browser", "Test browser", "Use the test provider browser", IsDefault: true)],
            Actions: [new AccountProviderActionDescriptor("manage-test", "Manage test", new Uri("https://example.test"))]);
        public bool Loaded { get; private set; }
        public bool SignedIn { get; private set; }
        public bool Refreshed { get; private set; }
        public bool Authenticated { get; private set; }
        public bool Removed { get; private set; }

        public Task InitializeAsync(AccountProviderInitializationContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AccountProviderLoadResult> LoadAccountsAsync(IReadOnlyList<EAccount> persistedAccounts, CancellationToken cancellationToken = default)
        {
            Loaded = true;
            return Task.FromResult(new AccountProviderLoadResult([]));
        }

        public Task<EAccount> SignInAsync(AccountSignInRequest request, CancellationToken cancellationToken = default)
        {
            SignedIn = true;
            return Task.FromResult(_account);
        }

        public Task RefreshAsync(EAccount account, CancellationToken cancellationToken = default)
        {
            Refreshed = true;
            return Task.CompletedTask;
        }

        public Task<GameAuthenticationResult> AuthenticateForLaunchAsync(EAccount account, CancellationToken cancellationToken = default)
        {
            Authenticated = true;
            return Task.FromResult<GameAuthenticationResult>(null!);
        }

        public Task RemoveAsync(EAccount account, CancellationToken cancellationToken = default)
        {
            Removed = true;
            return Task.CompletedTask;
        }
    }
}
