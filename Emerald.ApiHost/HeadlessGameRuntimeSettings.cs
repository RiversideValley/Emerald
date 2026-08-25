using Emerald.CoreX.Runtime;

namespace Emerald.ApiHost;

public sealed class HeadlessGameRuntimeSettings : IGameRuntimeSettings
{
    public bool IsLogCaptureEnabled => true;
}
