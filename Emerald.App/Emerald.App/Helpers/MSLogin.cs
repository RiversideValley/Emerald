using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Auth.Microsoft.Mojang;
using CmlLib.Core.Auth.Microsoft.MsalClient;
using Microsoft.Identity.Client;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Markup;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace Emerald.WinUI.Helpers
{
    public sealed class MSLoginHelper
    {
        JavaEditionLoginHandler handler;
        ContentDialog DeviceCodeDialog;
        public MSLoginHelper()
        {
        }

        public enum Exceptions
        {
            Success,
            NoAccount,
            Cancelled,
            ConnectFailed
        }

        public async Task Initialize(string cId)
        {
            var app = await MsalMinecraftLoginHelper.BuildApplicationWithCache(cId);
            handler = new LoginHandlerBuilder()
                .ForJavaEdition()
                .WithMsalOAuth(app, factory => factory.CreateDeviceCodeApi(result =>
                {
                    InitializeDeviceCodeDialog(result);
                    return Task.CompletedTask;
                }))
                .Build();
        }
        private void InitializeDeviceCodeDialog(DeviceCodeResult result)
        {
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
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
                    var p = new DataPackage();
                    p.RequestedOperation = DataPackageOperation.Copy;
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
                        new Run() { Text = lt.Remove(0, lt.IndexOf('}')) },
                        t
                    }
                });
                m.Children.Add(c);
                m.Children.Add(new ProgressBar() { IsIndeterminate = true, Margin = new(0, 5, 0, 0) });
                DeviceCodeDialog =  m.ToContentDialog("SignInWithMS".Localize());
                _ = DeviceCodeDialog.ShowAsync();
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
                var s = await handler.LoginFromOAuth();
                if (DeviceCodeDialog != null)
                    DeviceCodeDialog.Hide();

                return s.GameSession;
            }
            catch (Exception)
            {
                if(DeviceCodeDialog!= null)
                    DeviceCodeDialog.Hide();

                throw;
            }
        }
    }
}
