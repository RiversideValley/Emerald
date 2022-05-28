using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmlLib.Core.Auth.Microsoft.MsalClient;
using CmlLib.Core;
using Microsoft.Identity.Client;
using CmlLib.Core.Auth.Microsoft;
using System.Net.Http;

namespace SDLauncher_UWP.Helpers
{
    class MSLogin
    {
        IPublicClientApplication app;
        MsalMinecraftLoginHandler handler;
        public MSLogin()
        {
        }
        public enum Exceptions
        {
            Success,
            NoAccount,
            Cancelled,
            ConnectFailed
        }
        public void Initialize()
        {
            app = MsalMinecraftLoginHelper.CreateDefaultApplicationBuilder("ff127655-b402-4ca7-815f-b878147837ad").Build();
            handler = new MsalMinecraftLoginHandler(app);
        }
        public async Task<bool> LoginSilent()
        {
            try
            {
                vars.session = await handler.LoginSilent();
                return true;
            }
            catch (MsalUiRequiredException)
            {
                return false;
            }
        }
        public async Task<bool> Logout()
        {
            await handler.RemoveAccounts();
            return true;
        }
        public async Task<Exceptions> Login()
        {
            try
            {
                vars.session = await handler.LoginInteractive(useEmbeddedWebView: true);
                return Exceptions.Success;
            }
            catch (MinecraftAuthException)
            {
                return Exceptions.NoAccount;
            }
            catch (MsalClientException)
            {
                return Exceptions.Cancelled;
            }
            catch (HttpRequestException)
            {
                return Exceptions.ConnectFailed;
            }
        }
    }
}
