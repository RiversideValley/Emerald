using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Auth.Microsoft.MsalClient;
using Microsoft.Identity.Client;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace Emerald.WinUI.Helpers
{
    public sealed class MSLoginHelper
    {
        JavaEditionLoginHandler handler;
        ContentDialog InformDialog;
        OAuthMode mode;
        bool IsInTask;
        bool IsInitizlied;

        public MSLoginHelper()
        {
        }

        public MSLoginHelper(string cId, OAuthMode mode) =>
            Initialize(cId, mode);
        

        public enum OAuthMode
        {
            FromBrowser,
            EmbededDialog,
            DeviceCode
        }

        public void Initialize(string cId, OAuthMode mode)
        {
            IsInitizlied = true;
            this.mode = mode;
            var app = MsalMinecraftLoginHelper.BuildApplication(cId);
            var builder = new LoginHandlerBuilder().ForJavaEdition();

            if (mode == OAuthMode.DeviceCode)
                builder = builder.WithMsalOAuth(app, factory => factory.CreateDeviceCodeApi(result =>
                {
                    InitializeInformDialog(result);
                    return Task.CompletedTask;
                }));
            else if (mode == OAuthMode.EmbededDialog)
                builder = builder.WithMsalOAuth(app, factory => factory.CreateWithEmbeddedWebView());
            else
                builder = builder.WithMsalOAuth(app, factory => new MsalInteractiveOAuthApi(app, x => x.WithUseEmbeddedWebView(false)));

            handler = builder.Build();
        }
        private void InitializeInformDialog(DeviceCodeResult result)
        {
            App.Current.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                var m = new StackPanel() { VerticalAlignment = VerticalAlignment.Stretch };
                var lt = "GotoLinkAndEnterDeviceCode".Localize();
                var cb = new Button()
                {
                    Content = new Microsoft.UI.Xaml.Controls.FontIcon() { Glyph = "\uE16F" },
                    Padding = new(5)
                };
                cb.Click += (s, e) =>
                {
                    var p = new DataPackage
                    {
                        RequestedOperation = DataPackageOperation.Copy
                    };
                    p.SetText(result.UserCode);
                    Clipboard.SetContent(p);
                };
                var c = new StackPanel()
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new(0, 8, 0, 8),
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock()
                        {
                            VerticalAlignment = VerticalAlignment.Center,
                            Text = result.UserCode
                        },
                        cb
                    }
                };
                var t = new Run();
                var timer = new DispatcherTimer() { Interval = new TimeSpan(0, 0, 1) };
                timer.Tick += (s, e) =>
                {
                    if (result.ExpiresOn > DateTime.Now)
                    {
                        t.Text = " (" + "TimeLeft".Localize().Replace("{Time}", result.ExpiresOn.LocalDateTime.Subtract(DateTime.Now).ToString(@"mm\:ss")) + ")";
                    }
                    else
                    {
                        timer.Stop();
                    }
                };
                timer.Start();
                m.Children.Add(new TextBlock()
                {
                    TextWrapping = TextWrapping.WrapWholeWords,
                    Inlines =
                    {
                        new Run() { Text = lt.Remove(lt.IndexOf('{')) },
                        new Hyperlink()
                        {
                            Inlines = { new Run() { Text = result.VerificationUrl} },
                            NavigateUri = new Uri(result.VerificationUrl)
                        },
                        new Run() { Text = lt.Remove(0, lt.IndexOf('}') + 1) },
                        t
                    }
                });
                m.Children.Add(c);
                m.Children.Add(new ProgressBar() { IsIndeterminate = true, Margin = new(0, 5, 0, 0) });
                InformDialog = m.ToContentDialog("SignInWithMS".Localize());
                InformDialog.Closing += (_, e) => e.Cancel = IsInTask;
                _ = InformDialog.ShowAsync();
            });
        }
        public async Task<bool> Logout()
        {
            await handler.ClearCache();

            return true;
        }

        public async Task<MSession> Login()
        {
            try
            {
                if (mode == OAuthMode.FromBrowser)
                {
                    InformDialog = new ProgressBar() { IsIndeterminate = true }.ToContentDialog("SignInWithMS".Localize());
                    _ = InformDialog.ShowAsync();
                }
                App.Current.Launcher.UIState = false;
                IsInTask = true;
                var s = await handler.LoginFromOAuth();
                App.Current.Launcher.UIState = true;
                IsInTask = false;
                if (mode != OAuthMode.EmbededDialog && InformDialog != null)
                    InformDialog.Hide();

                return s.GameSession;
            }
            catch (Exception)
            {
                App.Current.Launcher.UIState = true;
                IsInTask = false;

                if (mode != OAuthMode.EmbededDialog && InformDialog != null)
                    InformDialog.Hide();

                throw;
            }
        }
    }
}
