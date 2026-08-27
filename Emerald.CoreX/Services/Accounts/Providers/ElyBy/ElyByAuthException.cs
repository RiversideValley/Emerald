namespace Emerald.CoreX.Services.Auth.ElyBy;

public class ElyByAuthException : Exception
{
    public ElyByAuthException(string message)
        : base(message)
    {
    }

    public ElyByAuthException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class ElyByTwoFactorRequiredException : ElyByAuthException
{
    public ElyByTwoFactorRequiredException()
        : base("This Ely.by account requires a two-factor authentication code.")
    {
    }
}

public sealed class ElyByReauthenticationRequiredException : ElyByAuthException
{
    public ElyByReauthenticationRequiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
