using System.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Emerald.Controls;

/// <summary>Uses the same skin-head renderer as Home and the navigation sidebar.</summary>
public sealed class AccountHead : Grid
{
    private readonly Image _image = new();
    private EAccount? _subscribedAccount;
    private int _version;

    public static readonly DependencyProperty AccountProperty = DependencyProperty.Register(
        nameof(Account), typeof(EAccount), typeof(AccountHead),
        new PropertyMetadata(null, (sender, _) => ((AccountHead)sender).BindAccount()));

    public EAccount? Account
    {
        get => (EAccount?)GetValue(AccountProperty);
        set => SetValue(AccountProperty, value);
    }

    public AccountHead()
    {
        Children.Add(new SymbolIcon(Symbol.Contact));
        Children.Add(_image);
        Loaded += (_, _) => BindAccount();
        Unloaded += (_, _) =>
        {
            ++_version;
            Unsubscribe();
        };
    }

    private void Unsubscribe()
    {
        if (_subscribedAccount != null) _subscribedAccount.PropertyChanged -= AccountChanged;
        _subscribedAccount = null;
    }

    private void BindAccount()
    {
        ++_version;
        Unsubscribe();
        _image.Source = null;
        if (!IsLoaded || Account == null) return;
        _subscribedAccount = Account;
        _subscribedAccount.PropertyChanged += AccountChanged;
        _ = LoadAsync(Account);
    }

    private void AccountChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EAccount.Skin))
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (IsLoaded && Account is { } account) _ = LoadAsync(account);
            });
        }
    }

    private async Task LoadAsync(EAccount account)
    {
        var version = ++_version;
        try
        {
            var skin = account.Skin ?? await Ioc.Default.GetRequiredService<IAccountService>().GetSkinAsync(account);
            if (version != _version || !IsLoaded || Account != account) return;
            var image = await MinecraftSkinImageFactory.CreateHeadAsync(skin, 80);
            if (version == _version && IsLoaded && Account == account) _image.Source = image;
        }
        catch
        {
            // Keep the contact glyph when a skin is unavailable, as Home does.
        }
    }
}
