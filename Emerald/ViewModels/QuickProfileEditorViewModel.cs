using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Emerald.CoreX;
using Emerald.CoreX.Models;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services;
using Emerald.CoreX.Services.Servers;
using Emerald.CoreX.Services.Worlds;

namespace Emerald.ViewModels;

public partial class QuickProfileEditorViewModel : ObservableObject
{
    private readonly IQuickProfileService _profiles;
    private readonly IMinecraftWorldService _worlds;
    private readonly QuickProfile? _original;
    private CancellationTokenSource? _load;
    private bool _initializing;
    public ObservableCollection<Game> Games { get; }
    public ObservableCollection<EAccount> Accounts { get; }
    public ObservableCollection<SavedServer> Servers { get; }
    public ObservableCollection<MinecraftWorld> Worlds { get; } = [];

    public IReadOnlyList<Choice<MinecraftLaunchTargetKind>> Destinations { get; } =
        new[] { MinecraftLaunchTargetKind.MainMenu, MinecraftLaunchTargetKind.World, MinecraftLaunchTargetKind.Server }
            .Select(x => new Choice<MinecraftLaunchTargetKind>(x, DashboardText.Target(x))).ToArray();

    public IReadOnlyList<QuickProfileGlyphOption> Icons { get; } = QuickProfileGlyphs.All;
    public IReadOnlyList<Choice<uint>> Colors { get; private set; }
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private Game? _selectedGame;
    [ObservableProperty] private EAccount? _selectedAccount;
    [ObservableProperty] private SavedServer? _selectedServer;
    [ObservableProperty] private MinecraftWorld? _selectedWorld;
    [ObservableProperty] private Choice<MinecraftLaunchTargetKind>? _destination;
    [ObservableProperty] private QuickProfileGlyphOption? _icon;
    [ObservableProperty] private QuickProfileIconKind _iconKind;
    [ObservableProperty] private string? _blockIconFileName;
    [ObservableProperty] private Choice<uint>? _accent;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _error = string.Empty;
    [ObservableProperty] private QuickProfileCardViewModel? _preview;
    public bool IsServer => Destination?.Value == MinecraftLaunchTargetKind.Server;
    public bool IsWorld => Destination?.Value == MinecraftLaunchTargetKind.World;
    public bool IsGlyphIcon => IconKind == QuickProfileIconKind.Glyph;
    public bool IsBlockIcon => IconKind == QuickProfileIconKind.Block;
    public bool CanSave => !IsLoading && string.IsNullOrEmpty(Error);
    public bool HasError => !string.IsNullOrEmpty(Error);

    public string SetupSummary => string.Join(" · ",
        new[] { SelectedGame?.Version.DisplayName, SelectedAccount?.Name, Preview?.Target }.Where(x =>
            !string.IsNullOrWhiteSpace(x)));

    public QuickProfileEditorViewModel(HomePageViewModel home, IAccountService accounts, IQuickProfileService profiles,
        IMinecraftWorldService worlds, QuickProfile? original)
    {
        _initializing = true;
        _profiles = profiles;
        _worlds = worlds;
        _original = original;
        Games = new ObservableCollection<Game>(home.Games);
        Accounts = new ObservableCollection<EAccount>(accounts.Accounts);
        Servers = new ObservableCollection<SavedServer>(home.FavoriteServers);
        Colors = CreateColors(original);
        SetInitialValues(home, accounts, original);
        _initializing = false;
        Update();
        _ = LoadWorldsAsync(original?.WorldFolderName ?? home.SelectedWorld?.FolderName);
    }

    private static IReadOnlyList<Choice<uint>> CreateColors(QuickProfile? original)
    {
        IReadOnlyList<Choice<uint>> colors =
        [
            new(0xFF107C10, DashboardText.Get("Emerald")), new(0xFF0067C0, DashboardText.Get("Blue")),
            new(0xFF744DA9, DashboardText.Get("Purple")), new(0xFFCA5010, DashboardText.Get("Orange")),
            new(0xFFC239B3, DashboardText.Get("Rose")), new(0xFFD13438, DashboardText.Get("Red")),
            new(0xFFFFB900, DashboardText.Get("Amber")), new(0xFF498205, DashboardText.Get("Lime")),
            new(0xFF038387, DashboardText.Get("Teal")), new(0xFF0099BC, DashboardText.Get("Cyan")),
            new(0xFF4F4DAB, DashboardText.Get("Indigo")), new(0xFF69797E, DashboardText.Get("Slate"))
        ];

        return original != null && colors.All(x => x.Value != original.AccentArgb)
            ? colors.Append(new Choice<uint>(original.AccentArgb, DashboardText.Get("Custom"))).ToArray()
            : colors;
    }

    private void SetInitialValues(HomePageViewModel home, IAccountService accounts, QuickProfile? original)
    {
        SelectedGame = original == null
            ? home.SelectedGame
            : Games.FirstOrDefault(x => x.InstanceId == original.InstanceId);
        SelectedAccount = original == null
            ? accounts.GetSelectedAccount()
            : Accounts.FirstOrDefault(x => x.UniqueId == original.AccountUniqueId);
        Destination = Destinations.First(x => x.Value == (original?.TargetKind ?? home.SelectedDestination));
        SelectedServer = original == null
            ? home.SelectedServer
            : Servers.FirstOrDefault(x => x.Id == original.SavedServerId);
        Icon = Icons.FirstOrDefault(x => x.Key == original?.GlyphKey) ??
               Icons.First(x => x.Key == (IsWorld ? "World" : IsServer ? "Server" : "Play"));
        IconKind = original?.IconKind ?? QuickProfileIconKind.Glyph;
        BlockIconFileName = original?.BlockIconFileName;
        if (IconKind == QuickProfileIconKind.Block && string.IsNullOrWhiteSpace(BlockIconFileName))
        {
            IconKind = QuickProfileIconKind.Glyph;
        }

        Accent = Colors.First(x => x.Value == (original?.AccentArgb ?? 0xFF107C10));
        Name = original?.Name ??
               (IsServer ? SelectedServer?.Name :
                   IsWorld ? home.SelectedWorld?.DisplayName : SelectedGame?.Version.DisplayName) ??
               DashboardText.Get("QuickPlay");
    }

    partial void OnSelectedGameChanged(Game? value)
    {
        if (!_initializing)
        {
            SelectedWorld = null;
            _ = LoadWorldsAsync(null);
        }

        Update();
    }

    private async Task LoadWorldsAsync(string? folder)
    {
        _load?.Cancel();
        var load = _load = new CancellationTokenSource();
        var game = SelectedGame;
        IsLoading = true;
        try
        {
            var worlds = game == null ? [] : await _worlds.ScanAsync(game, load.Token);
            if (load.IsCancellationRequested)
            {
                return;
            }

            Worlds.ReplaceWith(worlds.Where(x => x.IsReadable));
            SelectedWorld = Worlds.FirstOrDefault(x => x.FolderName == folder);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            Error = DashboardText.Get("WorldLoadFailed");
        }
        finally
        {
            if (ReferenceEquals(_load, load))
            {
                IsLoading = false;
                Update();
            }
        }
    }

    public void CancelLoading()
    {
        _load?.Cancel();
    }

    partial void OnNameChanged(string value)
    {
        Update();
    }

    partial void OnSelectedAccountChanged(EAccount? value)
    {
        Update();
    }

    partial void OnSelectedServerChanged(SavedServer? value)
    {
        Update();
    }

    partial void OnSelectedWorldChanged(MinecraftWorld? value)
    {
        Update();
    }

    partial void OnIconChanged(QuickProfileGlyphOption? value)
    {
        Update();
    }

    partial void OnIconKindChanged(QuickProfileIconKind value)
    {
        OnPropertyChanged(nameof(IsGlyphIcon));
        OnPropertyChanged(nameof(IsBlockIcon));
        Update();
    }

    partial void OnBlockIconFileNameChanged(string? value)
    {
        Update();
    }

    partial void OnAccentChanged(Choice<uint>? value)
    {
        Update();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnDestinationChanged(Choice<MinecraftLaunchTargetKind>? value)
    {
        if (!_initializing)
        {
            if (!IsServer)
            {
                SelectedServer = null;
            }

            if (!IsWorld)
            {
                SelectedWorld = null;
            }
        }

        OnPropertyChanged(nameof(IsServer));
        OnPropertyChanged(nameof(IsWorld));
        Update();
    }

    public QuickProfile CreateProfile()
    {
        return new QuickProfile
        {
            Id = _original?.Id ?? Guid.NewGuid(),
            CreatedAt = _original?.CreatedAt ?? DateTimeOffset.UtcNow,
            Name = Name.Trim(),
            GlyphKey = Icon?.Key ?? "Play",
            IconKind = this.IconKind,
            BlockIconFileName = IsBlockIcon ? this.BlockIconFileName : null,
            AccentArgb = Accent?.Value ?? 0xFF107C10,
            InstanceId = SelectedGame?.InstanceId ?? Guid.Empty,
            AccountUniqueId = SelectedAccount?.UniqueId ?? string.Empty,
            TargetKind = Destination?.Value ?? MinecraftLaunchTargetKind.MainMenu,
            SavedServerId = IsServer ? SelectedServer?.Id : null,
            WorldFolderName = IsWorld ? SelectedWorld?.FolderName : null,
            TargetDisplayNameSnapshot = IsServer ? SelectedServer?.Name :
                IsWorld ? SelectedWorld?.DisplayName : DashboardText.Get("MainMenu")
        };
    }

    private void Update()
    {
        if (_initializing)
        {
            return;
        }

        var profile = CreateProfile();
        var validation = _profiles.Validate(profile, Games, Accounts, Servers);
        Error = string.IsNullOrWhiteSpace(Name) ? DashboardText.Get("EnterName")
            : IsBlockIcon && string.IsNullOrWhiteSpace(BlockIconFileName) ? DashboardText.Get("ChooseIcon")
            : SelectedGame == null ? DashboardText.Get("ChooseInstance")
            : SelectedAccount == null ? DashboardText.Get("ChooseAccount")
            : IsServer && SelectedServer == null ? DashboardText.Get("ChooseServer")
            : IsWorld && SelectedWorld == null ? DashboardText.Get("ChooseWorld")
            : !validation.IsValid ? DashboardText.Get("ProfileRepair") : string.Empty;
        Preview = new QuickProfileCardViewModel(profile, validation, SelectedGame?.Version.DisplayName,
            SelectedAccount?.Name);
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(SetupSummary));
    }

    public void SelectGlyph(QuickProfileGlyphOption icon)
    {
        IconKind = QuickProfileIconKind.Glyph;
        Icon = icon;
    }

    public void SelectBlock(string fileName)
    {
        IconKind = QuickProfileIconKind.Block;
        BlockIconFileName = Path.GetFileName(fileName);
    }

    public void SetAccent(uint argb)
    {
        Accent = Colors.FirstOrDefault(x => x.Value == argb) ?? new Choice<uint>(argb, DashboardText.Get("Custom"));
    }
}
