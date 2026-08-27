using CmlLib.Core.ProcessBuilder;

namespace Emerald.CoreX.Services.Auth;

public sealed record AccountRuntimeAuthOptions(IReadOnlyList<MArgument> ExtraJvmArguments)
{
    public static AccountRuntimeAuthOptions Empty { get; } = new([]);
}
