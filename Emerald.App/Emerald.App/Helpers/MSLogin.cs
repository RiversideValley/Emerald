using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Auth.Microsoft.Mojang;
using CmlLib.Core.Auth.Microsoft.MsalClient;
using Microsoft.Identity.Client;
using System;
using System.Threading.Tasks;

namespace Emerald.WinUI.Helpers
{
    public sealed class MSLoginHelper
    {
        IPublicClientApplication app;
        JavaEditionLoginHandler handler;

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
                .WithMsalOAuth(app, factory => factory.CreateWithEmbeddedWebView())
                .Build();
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

                return s.GameSession;
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
