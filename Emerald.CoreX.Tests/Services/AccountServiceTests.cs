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
    public void RequireMicrosoftAccountForOfflineAccounts_IsEnabled()
    {
        var service = CreateService(new InMemoryBaseSettingsService());

        Assert.True(service.RequireMicrosoftAccountForOfflineAccounts);
    }

    [Fact]
    public void RequireMicrosoftAccountForElyByAccounts_IsEnabled()
    {
        var service = CreateService(new InMemoryBaseSettingsService());

        Assert.True(service.RequireMicrosoftAccountForElyByAccounts);
    }

    [Fact]
    public void CreateOfflineAccount_WithoutMicrosoftAccount_Throws()
    {
        var service = CreateService(new InMemoryBaseSettingsService());

        var exception = Assert.Throws<InvalidOperationException>(() => service.CreateOfflineAccount("Alpha"));

        Assert.Equal("Creating offline accounts requires at least one Microsoft account.", exception.Message);
    }

    [Fact]
    public void SetSelectedAccount_OfflineWithoutMicrosoftAccount_Throws()
    {
        var service = CreateService(new InMemoryBaseSettingsService());
        var offline = new EAccount("Alpha", AccountType.Offline);
        service.Accounts.Add(offline);

        var exception = Assert.Throws<InvalidOperationException>(() => service.SetSelectedAccount(offline));

        Assert.Equal("Selecting an offline account requires at least one Microsoft account.", exception.Message);
        Assert.Null(service.GetSelectedAccount());
    }

    [Fact]
    public async Task AuthenticateAccountAsync_OfflineWithoutMicrosoftAccount_Throws()
    {
        var service = CreateService(new InMemoryBaseSettingsService());
        var offline = new EAccount("Alpha", AccountType.Offline);
        service.Accounts.Add(offline);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AuthenticateAccountAsync(offline));

        Assert.Equal("Offline accounts require at least one Microsoft account.", exception.Message);
    }

    [Fact]
    public async Task SignInElyByAccountAsync_WithoutMicrosoftAccount_Throws()
    {
        var service = CreateServiceWithEly(new InMemoryBaseSettingsService());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SignInElyByAccountAsync("ely@example.com", "password"));

        Assert.Equal("Signing in with Ely.by requires at least one Microsoft account.", exception.Message);
    }

    [Fact]
    public void SetSelectedAccount_ElyByWithoutMicrosoftAccount_Throws()
    {
        var service = CreateService(new InMemoryBaseSettingsService());
        var elyBy = new EAccount("ElyAlpha", AccountType.ElyBy, "ely-alpha-uuid", "elyby:ely-alpha-uuid");
        service.Accounts.Add(elyBy);

        var exception = Assert.Throws<InvalidOperationException>(() => service.SetSelectedAccount(elyBy));

        Assert.Equal("Selecting an Ely.by account requires at least one Microsoft account.", exception.Message);
        Assert.Null(service.GetSelectedAccount());
    }

    [Fact]
    public async Task AuthenticateAccountAsync_ElyByWithoutMicrosoftAccount_Throws()
    {
        var service = CreateService(new InMemoryBaseSettingsService());
        var elyBy = new EAccount("ElyAlpha", AccountType.ElyBy, "ely-alpha-uuid", "elyby:ely-alpha-uuid");
        service.Accounts.Add(elyBy);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AuthenticateAccountAsync(elyBy));

        Assert.Equal("Ely.by accounts require at least one Microsoft account.", exception.Message);
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
        microsoftClient.OnInteractiveSignInAsync = client =>
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
    public async Task SignInMicrosoftAccountAsync_Throws_WhenAccountDoesNotMaterialize()
    {
        var microsoftClient = new FakeMicrosoftAccountClient
        {
            OnInteractiveSignInAsync = _ => Task.FromResult(new MicrosoftInteractiveSignInResult("missing-id", "Ghost", "missing-id"))
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
    public async Task SignInElyByAccountAsync_AddsStoredAccount_AndSelectsWhenNoSelectionExists()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        var elyByClient = new FakeElyByAuthClient
        {
            AuthenticateResult = new ElyByAuthSession("ElyAlpha", "ely-alpha-uuid", "ely-access", "ely-client")
        };

        var service = CreateServiceWithEly(baseSettingsService, elyByClient: elyByClient);
        AddMicrosoftAccount(service);

        await service.SignInElyByAccountAsync("ely@example.com", "password", "123456");

        var account = Assert.Single(service.Accounts, account => account.Type == AccountType.ElyBy);
        Assert.Equal("ElyAlpha", account.Name);
        Assert.Equal("ely-alpha-uuid", account.UUID);
        Assert.Equal("elyby:ely-alpha-uuid", account.UniqueId);
        Assert.Equal(AccountProviderIds.ElyBy, account.ProviderId);
        Assert.Same(account, service.GetSelectedAccount());
        Assert.Equal([("ely@example.com", "password", "123456")], elyByClient.AuthenticateCalls);

        var storedElyAccounts = baseSettingsService.Peek<List<ElyByStoredAccount>>(SettingsKeys.ElyByAccounts);
        Assert.NotNull(storedElyAccounts);
        var stored = Assert.Single(storedElyAccounts!);
        Assert.Equal("ely-access", stored.AccessToken);
        Assert.Equal("ely-client", stored.ClientToken);
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
            authlibInjectorService ?? new FakeAuthlibInjectorService());

    private static AccountService CreateServiceWithEly(
        InMemoryBaseSettingsService baseSettingsService,
        FakeElyByAuthClient? elyByClient = null,
        FakeAuthlibInjectorService? authlibInjectorService = null)
        => CreateService(
            baseSettingsService,
            new FakeMicrosoftAccountClient(),
            new FakeNotificationService(),
            elyByClient,
            authlibInjectorService);

    private static void AddMicrosoftAccount(AccountService service, string name = "Microsoft")
    {
        var account = new EAccount(name, AccountType.Microsoft, $"{name}-uuid", $"{name}-id");
        service.Accounts.Add(account);
    }
}
