using OpenQA.Selenium.Appium;

namespace Maui.SmartImage.UITests;

/// <summary>
/// Holds the active Appium session. Each platform [SetUpFixture] assigns <see cref="Driver"/>.
/// </summary>
public static class AppiumSession
{
    public static AppiumDriver? Driver { get; set; }

    public static Uri ServerUri { get; } = new("http://127.0.0.1:4723/");

    public static string RequireEnv(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Environment variable '{name}' is required. Set it to the built sample app path.");
        }

        return value;
    }
}
