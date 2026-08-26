using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX;
using Emerald.CoreX.GameOptions;
using Emerald.Helpers;
using Emerald.Services;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Emerald.Views;

public sealed partial class GameOptionsDialog : ContentDialog
{
    public GameOptionsViewModel ViewModel { get; }

    public GameOptionsDialog(Game game)
    {
        ViewModel = Ioc.Default.GetService<GameOptionsViewModel>()!;
        DataContext = ViewModel;
        InitializeComponent();
        Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
        this.StretchToWindow();
        RequestedTheme = (ElementTheme)Ioc.Default
            .GetService<SettingsService>()!.Settings.App.Appearance.Theme;

        // Fire-and-forget load; the ViewModel shows a spinner while loading.
        _ = ViewModel.LoadCommand.ExecuteAsync(game);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SaveAsync();
        if (ViewModel.LastSaveSucceeded) Hide();
    }
}

/// <summary>Selects the correct DataTemplate based on <see cref="MinecraftOptionType"/>.</summary>
public sealed class OptionTemplateSelector : DataTemplateSelector
{
    public DataTemplate? BooleanTemplate  { get; set; }
    public DataTemplate? SliderTemplate   { get; set; }
    public DataTemplate? EnumTemplate     { get; set; }
    public DataTemplate? SoundTemplate    { get; set; }
    public DataTemplate? KeyBindTemplate  { get; set; }
    public DataTemplate? ReadOnlyTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        if (item is MinecraftOptionEntry e)
        {
            return e.Type switch
            {
                MinecraftOptionType.Boolean     => BooleanTemplate,
                MinecraftOptionType.IntSlider   => SliderTemplate,
                MinecraftOptionType.FloatSlider => SliderTemplate,
                MinecraftOptionType.Enum        => EnumTemplate,
                MinecraftOptionType.SoundVolume => SoundTemplate,
                MinecraftOptionType.KeyBind     => KeyBindTemplate,
                MinecraftOptionType.ReadOnly    => ReadOnlyTemplate,
                _                               => BooleanTemplate
            } ?? base.SelectTemplateCore(item);
        }
        return base.SelectTemplateCore(item);
    }
}
