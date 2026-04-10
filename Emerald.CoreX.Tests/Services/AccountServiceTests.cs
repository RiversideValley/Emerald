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

        Assert.Equal("Selecting offline accounts requires at least one Microsoft account.", exception.Message);
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
        => new(NullLogger<AccountService>.Instance, baseSettingsService);

    private static void AddMicrosoftAccount(AccountService service, string name = "Microsoft")
    {
        var account = new EAccount(name, AccountType.Microsoft, $"{name}-uuid", $"{name}-id");
        service.Accounts.Add(account);
    }
}
