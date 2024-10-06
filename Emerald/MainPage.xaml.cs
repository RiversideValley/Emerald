namespace Emerald;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        this.InitializeComponent();
        this.Loaded += MainPage_Loaded;
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
#if WINDOWS
        Emerald.Uno.Helpers.WindowManager.SetTitleBar(App.Current.MainWindow, AppTitleBar);
#endif
    }
}
