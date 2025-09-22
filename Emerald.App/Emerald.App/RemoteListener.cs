using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

public class RemoteListener : IDisposable
{
    private readonly int _udpPort;
    private readonly int _tcpPort;
    private readonly int _tcpStreamPort;

    // --- REFACTORED MEMBERS ---
    // Use a CancellationTokenSource to manage cancellation gracefully.
    private CancellationTokenSource? _cancellationTokenSource;
    private List<Task> _runningTasks = new List<Task>();

    // Manage network clients at the class level for proper disposal.
    private UdpClient? _udpClient;
    private TcpListener? _tcpListener;
    private TcpListener? _tcpStreamListener;
    // --- END REFACTORED MEMBERS ---

    public event EventHandler<string>? ChatReceived;
    public event EventHandler? PrankReceived;

    public RemoteListener(int udpPort = 6000, int tcpPort = 6001, int tcpStreamPort = 7001)
    {
        _udpPort = udpPort;
        _tcpPort = tcpPort;
        _tcpStreamPort = tcpStreamPort;
    }

    // --- REFACTORED Stop METHOD ---
    public async Task StopAsync()
    {
        if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            return;

        Console.WriteLine("[Listener] Stopping all services...");

        // 1. Signal all tasks that they need to stop.
        _cancellationTokenSource.Cancel();

        try
        {
            // 2. Wait for all tasks to complete their shutdown logic.
            await Task.WhenAll(_runningTasks);
        }
        catch (OperationCanceledException)
        {
            // This is expected and normal when tasks are cancelled.
            Console.WriteLine("[Listener] All tasks successfully cancelled.");
        }

        // 3. The IDisposable pattern calls Dispose(), which handles cleanup.
    }

    // --- REFACTORED Start METHOD ---
    public void StartListening()
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            return;

        Console.WriteLine("[Listener] Starting all services...");

        // Create a new cancellation source for this run.
        _cancellationTokenSource = new CancellationTokenSource();
        _runningTasks = new List<Task>();

        // Start each listener as a Task and pass it the cancellation token.
        _runningTasks.Add(Task.Run(() => UdpListenLoop(_cancellationTokenSource.Token)));
        _runningTasks.Add(Task.Run(() => TcpListenLoop(_cancellationTokenSource.Token)));
        _runningTasks.Add(Task.Run(() => TcpStreamLoop(_cancellationTokenSource.Token)));
    }

    private async Task UdpListenLoop(CancellationToken token)
    {
        // Initialize the client here.
        using (_udpClient = new UdpClient(_udpPort))
        {
            _udpClient.EnableBroadcast = true;
            Console.WriteLine($"[Listener] Listening on UDP port {_udpPort}...");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Use ReceiveAsync with the token to make the wait cancellable.
                    var result = await _udpClient.ReceiveAsync(token);
                    string message = Encoding.UTF8.GetString(result.Buffer);


                    if (message == "WHO_IS_THERE?")
                    {
                        string response = "ITS_ME:" + GetLocalIPAddress();
                        byte[] data = Encoding.UTF8.GetBytes(response);
                        await _udpClient.SendAsync(data, data.Length, result.RemoteEndPoint);
                    }
                    else if (message.StartsWith("CHAT:"))
                    {
                        ChatReceived?.Invoke(this, message.Substring("CHAT:".Length));
                    }
                    else if (message == "PRANK")
                    {
                        PrankReceived?.Invoke(this, EventArgs.Empty);
                    }
                    else if (message.StartsWith("KEY:"))
                    {
                        string key = message.Substring("KEY:".Length);
                        SimulateKey(key);
                    }

                    else if (message.StartsWith("CLICK:"))
                    {
                        string[] parts = message.Split(':');
                        if (parts.Length == 3 &&
                            int.TryParse(parts[1], out int x) &&
                            int.TryParse(parts[2], out int y))
                        {
                            SimulateClick(x, y);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break; // Exit loop cleanly on cancellation.
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                        Console.WriteLine($"[UDP Listener] Error: {ex.Message}");
                }
            }
        }
        Console.WriteLine("[Listener] UDP Listener stopped.");
    }

    private async Task TcpListenLoop(CancellationToken token)
    {
        _tcpListener = new TcpListener(IPAddress.Any, _tcpPort);
        try
        {
            _tcpListener.Start();
            Console.WriteLine($"[Listener] TCP listening for screenshots on port {_tcpPort}...");

            while (!token.IsCancellationRequested)
            {
                // AcceptTcpClientAsync can be cancelled by the token.
                using var client = await _tcpListener.AcceptTcpClientAsync(token);
                using var ns = client.GetStream();
                using var bmp = CaptureScreen();
                await bmp.SaveAsync(ns, ImageFormat.Jpeg, token);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping.
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
                Console.WriteLine($"[TCP Listener] Error: {ex.Message}");
        }
        finally
        {
            _tcpListener.Stop();
            Console.WriteLine("[Listener] TCP Listener stopped.");
        }
    }

    private async Task TcpStreamLoop(CancellationToken token)
    {
        _tcpStreamListener = new TcpListener(IPAddress.Any, _tcpStreamPort);
        try
        {
            _tcpStreamListener.Start();
            Console.WriteLine($"[Listener] TCP streaming enabled on port {_tcpStreamPort}...");

            while (!token.IsCancellationRequested)
            {
                using var client = await _tcpStreamListener.AcceptTcpClientAsync(token);
                using var ns = client.GetStream();

                Debug.WriteLine("[Streamer] Client connected, starting stream...");
                while (client.Connected && !token.IsCancellationRequested)
                {
                    using var bmp = CaptureScreen();
                    using var ms = new MemoryStream();
                    await bmp.SaveAsync(ms, ImageFormat.Jpeg, token);
                    byte[] frameBytes = ms.ToArray();

                    byte[] lengthBytes = BitConverter.GetBytes(frameBytes.Length);
                    await ns.WriteAsync(lengthBytes, 0, lengthBytes.Length, token);
                    await ns.WriteAsync(frameBytes, 0, frameBytes.Length, token);
                    await ns.FlushAsync(token);

                    // Use Task.Delay for a cancellable wait.
                    await Task.Delay(200, token); // ~5 FPS
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
                Console.WriteLine($"[TCP Stream] Error: {ex.Message}");
        }
        finally
        {
            _tcpStreamListener.Stop();
            Console.WriteLine("[Listener] TCP Streamer stopped.");
        }
    }

    // --- IDisposable Implementation for Cleanup ---
    public void Dispose()
    {
        // This ensures network resources are always released.
        _cancellationTokenSource?.Dispose();
        _udpClient?.Dispose();
        _tcpListener?.Stop();
        _tcpStreamListener?.Stop();
    }

    private Bitmap CaptureScreen()
    {
        var bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(0, 0, 0, 0, bmp.Size);
        }
        return bmp;
    }

    private string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();
        }
        return "UNKNOWN";
    }

    private void SimulateClick(int x, int y)
    {
        Cursor.Position = new System.Drawing.Point(x, y);
        mouse_event(0x02, 0, 0, 0, 0);
        mouse_event(0x04, 0, 0, 0, 0);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);


    private void SimulateKey(string key)
    {
        try
        {
            // Convert string to Keys enum if possible
            if (Enum.TryParse(key, out Keys parsedKey))
            {
                SendKeyInput(parsedKey);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[KeySim] Error simulating key {key}: {ex.Message}");
        }
    }

    private void SendKeyInput(Keys key)
    {
        // keybd_event is enough for basic key input
        byte vk = (byte)key;
        keybd_event(vk, 0, 0, 0); // key down
        keybd_event(vk, 0, 2, 0); // key up
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
}

// Add this extension method to your project for bmp.SaveAsync
public static class ImageExtensions
{
    public static Task SaveAsync(this Image image, Stream stream, ImageFormat format, CancellationToken token)
    {
        return Task.Run(() => {
            if (token.IsCancellationRequested) return;
            image.Save(stream, format);
        }, token);
    }
}
