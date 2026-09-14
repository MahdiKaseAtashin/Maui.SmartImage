using System.Net.Sockets;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Service;

namespace Maui.SmartImage.UITests;

/// <summary>
/// Holds the active Appium session. Each platform [SetUpFixture] assigns <see cref="Driver"/>.
/// </summary>
public static class AppiumSession
{
    public const int DefaultPort = 4723;

    public static AppiumDriver? Driver { get; set; }

    public static Uri ServerUri { get; } = new($"http://127.0.0.1:{DefaultPort}/");

    private static AppiumLocalService? _localService;

    public static string RequireEnv(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Environment variable '{name}' is required. Set it to the built sample app path.");
        }

        return ResolvePath(value);
    }

    public static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        string? workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
        if (!string.IsNullOrWhiteSpace(workspace))
        {
            return Path.GetFullPath(Path.Combine(workspace, path));
        }

        return Path.GetFullPath(path);
    }

    public static void EnsureServer()
    {
        if (IsPortOpen(DefaultPort))
        {
            return;
        }

        AppiumServiceBuilder builder = new AppiumServiceBuilder()
            .UsingPort(DefaultPort)
            .WithLogFile(new FileInfo(Path.Combine(Environment.CurrentDirectory, "appium-local.log")));

        _localService = builder.Build();
        _localService.Start();

        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (IsPortOpen(DefaultPort))
            {
                return;
            }

            Thread.Sleep(500);
        }

        throw new TimeoutException($"Appium local service did not open port {DefaultPort}.");
    }

    public static void StopServer()
    {
        try
        {
            _localService?.Dispose();
        }
        catch
        {
            // Ignore shutdown races.
        }

        _localService = null;
    }

    private static bool IsPortOpen(int port)
    {
        try
        {
            using TcpClient client = new();
            IAsyncResult result = client.BeginConnect("127.0.0.1", port, null, null);
            bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
            if (!success)
            {
                return false;
            }

            client.EndConnect(result);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
