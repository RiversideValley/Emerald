using CmlLib.Core.Auth;

namespace Emerald.CoreX.Services.Auth;

public sealed record GameAuthenticationResult(
    MSession Session,
    AccountRuntimeAuthOptions RuntimeOptions)
{
    public GameAuthenticationResult(MSession session)
        : this(session, AccountRuntimeAuthOptions.Empty)
    {
    }
}
