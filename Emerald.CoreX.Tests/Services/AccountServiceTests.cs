using System.Linq;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services;
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
    public async Task LoadAllAccountsAsync_MergesOfflineSettingsWithMicrosoftAccounts_AndCleansLegacyMicrosoftEntries()
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

        var service = CreateService(baseSettingsService, microsoftClient);
        await service.InitializeAsync("test-client");

        await service.LoadAllAccountsAsync();

        Assert.Equal("test-client", microsoftClient.InitializedClientId);
        Assert.Equal(2, service.Accounts.Count);
        Assert.Contains(service.Accounts, account => account.Type == AccountType.Offline && account.UniqueId == "offline-alpha");
        Assert.Contains(service.Accounts, account => account.Type == AccountType.Microsoft && account.UniqueId == "ms-1");

        var storedAccounts = baseSettingsService.Peek<List<EAccount>>(SettingsKeys.MinecraftAccounts);
        Assert.NotNull(storedAccounts);
        var offlineOnly = Assert.Single(storedAccounts!);
        Assert.Equal(AccountType.Offline, offlineOnly.Type);
        Assert.Equal("offline-alpha", offlineOnly.UniqueId);
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
        => CreateService(baseSettingsService, new FakeMicrosoftAccountClient());

    private static AccountService CreateService(
        InMemoryBaseSettingsService baseSettingsService,
        FakeMicrosoftAccountClient microsoftAccountClient)
        => new(
            NullLogger<AccountService>.Instance,
            baseSettingsService,
            new ImmediateUiDispatcher(),
            "/tmp/emerald-tests/cml_accounts.json",
            microsoftAccountClient);

    private static void AddMicrosoftAccount(AccountService service, string name = "Microsoft")
    {
        var account = new EAccount(name, AccountType.Microsoft, $"{name}-uuid", $"{name}-id");
        service.Accounts.Add(account);
    }
}
