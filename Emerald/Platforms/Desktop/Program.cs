using Uno.UI.Hosting;
using Emerald.Services;

namespace Emerald;
public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        CrashFaultInjection.ConfigureFromArguments(args);
        var crashCoordinator = CrashBootstrap.Initialize();
        try
        {
#if DEBUG
            if (CrashFaultInjection.IsRequested("DesktopHost"))
            {
                throw new NotImplementedException("Intentional desktop-host crash test.");
            }
#endif

            var host = UnoPlatformHostBuilder.Create()
                .App(() => new App())
                .UseX11()
                .UseLinuxFrameBuffer()
                .UseMacOS()
                .UseWin32()
                .Build();

            host.Run();
            CrashBootstrap.RequestNormalShutdown();
        }
        catch (Exception exception)
        {
            crashCoordinator.CaptureAndTerminate(exception, "Desktop host");
        }
    }
}
