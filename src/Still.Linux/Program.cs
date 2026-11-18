using System.Net.Sockets;
using System.Text;
using System.Threading;
using Avalonia.Threading;

namespace Still.Linux;

public static class Program
{
    internal static string[] Args { get; private set; } = [];
    internal static bool Verify => Args.Contains("--verify");
    private static FileStream? instanceLock;
    private static Socket? listener;
    private static string socketPath = "";

    [STAThread]
    public static int Main(string[] args)
    {
        Args = args;
        if (args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine("Still — a futuristic desktop clock.\n\n  still              start the clock (or bring up Settings if it is already running)\n  still --settings   start with Settings open\n  still --capture    save renders of every preset and Settings to ./screenshots, then keep running\n  still --version    print the version");
            return 0;
        }
        if (args.Contains("--version")) { Console.WriteLine("Still " + typeof(Program).Assembly.GetName().Version?.ToString(3)); return 0; }

        string runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } dir && Directory.Exists(dir) ? dir : Path.GetTempPath();
        string name = Verify ? "still-verify" : "still";
        socketPath = Path.Combine(runtime, name + ".sock");
        try
        {
            // FileShare.None is an advisory flock on Linux: it is released automatically if Still crashes.
            instanceLock = new FileStream(Path.Combine(runtime, name + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            if (Signal("show")) return 0;
            Console.Error.WriteLine("Still is already running. Use its clock icon in the system tray to open Settings or unlock it.");
            return 1;
        }

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
        }
        finally
        {
            listener?.Dispose();
            try { File.Delete(socketPath); } catch (IOException) { }
            instanceLock.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<ClockApplication>()
        .UsePlatformDetect()
        .WithInterFont()
        .ConfigureFonts(fonts => fonts.AddFontCollection(new Avalonia.Media.Fonts.EmbeddedFontCollection(
            new Uri("fonts:Still", UriKind.Absolute), new Uri("avares://still/Assets/Fonts", UriKind.Absolute))))
        .With(new X11PlatformOptions { WmClass = "still" })
        .LogToTrace();

    /// <summary>A second launch asks the running clock to unlock and show Settings, then exits.</summary>
    private static bool Signal(string message)
    {
        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.Connect(new UnixDomainSocketEndPoint(socketPath));
            socket.Send(Encoding.UTF8.GetBytes(message + "\n"));
            return true;
        }
        catch (SocketException) { return false; }
    }

    internal static void Listen(Action<string> received)
    {
        try
        {
            try { File.Delete(socketPath); } catch (IOException) { }
            listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(socketPath));
            File.SetUnixFileMode(socketPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            listener.Listen(4);
        }
        catch (Exception e) when (e is SocketException or IOException or UnauthorizedAccessException) { listener = null; return; }
        new Thread(() =>
        {
            var buffer = new byte[64];
            while (true)
            {
                try
                {
                    using var client = listener.Accept();
                    client.ReceiveTimeout = 1000;
                    int read = client.Receive(buffer);
                    string message = Encoding.UTF8.GetString(buffer, 0, read).Trim();
                    if (message.Length > 0) Dispatcher.UIThread.Post(() => received(message));
                }
                catch (SocketException) { if (listener == null) return; }
                catch (ObjectDisposedException) { return; }
            }
        }) { IsBackground = true, Name = "Still instance listener" }.Start();
    }
}
