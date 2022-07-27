using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using DiscordRPC;
using Windows.Security.Authentication.Web;

namespace SDLauncher.UWP.Helpers
{
    public class RPCHelper
    {
        DiscordRpcClient Client { get; set; }
        public async Task Authenticate()
        {
            try
            {
                WebAuthenticationResult WebAuthenticationResult = await WebAuthenticationBroker.AuthenticateAsync(
                    WebAuthenticationOptions.None,
                    new Uri("https://discord.com/api/oauth2/authorize?client_id=945758066161356932&redirect_uri=http%3A%2F%2Fmsfree4all.rf.gd&response_type=code&scope=rpc%20rpc.activities.write"),
                    new Uri("http://msfree4all.rf.gd"));
                if (WebAuthenticationResult.ResponseStatus == WebAuthenticationStatus.Success)
                {
                    Client = new DiscordRpcClient(GetToken(WebAuthenticationResult.ResponseData.ToString()));
                }
                else if (WebAuthenticationResult.ResponseStatus == WebAuthenticationStatus.ErrorHttp)
                {
                    throw new Exception();
                }
                else
                {
                    throw new Exception();
                }
            }
            catch
            {
            }
        }

        private string GetToken(string uri)
        {
            string queryString = new Uri(uri.Replace("#", "?").ToString()).Query;
            var queryDictionary = HttpUtility.ParseQueryString(queryString);
            try
            {
                string accessToken = queryDictionary["code"];
                return accessToken;
            }
            catch
            {
                return null;
            }
        }
    }
}
