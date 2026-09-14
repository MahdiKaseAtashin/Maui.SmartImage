using NUnit.Framework;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace Maui.SmartImage.UITests;

[SetUpFixture]
public class AppiumSetup
{
    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        string appPath = AppiumSession.RequireEnv("WINDOWS_APP_PATH");
        if (!File.Exists(appPath))
        {
            throw new FileNotFoundException($"Windows app not found at '{appPath}'.", appPath);
        }

        AppiumSession.EnsureServer();

        AppiumOptions options = new();
        options.PlatformName = "Windows";
        options.AutomationName = "Windows";
        // WinAppDriver expects the legacy "app" capability (not only appium:app via options.App).
        options.AddAdditionalAppiumOption("app", appPath);
        options.AddAdditionalAppiumOption("appArguments", "--smoke");
        options.AddAdditionalAppiumOption("newCommandTimeout", 300);
        options.AddAdditionalAppiumOption("ms:waitForAppLaunch", "25");
        // WinAppDriver is started on 4724 in CI so it does not collide with Appium on 4723.
        string? wadUrl = Environment.GetEnvironmentVariable("WINAPPDRIVER_URL");
        if (!string.IsNullOrWhiteSpace(wadUrl))
        {
            options.AddAdditionalAppiumOption("wadUrl", wadUrl);
        }

        options.DeviceName = "WindowsPC";

        AppiumSession.Driver = new WindowsDriver(AppiumSession.ServerUri, options, TimeSpan.FromMinutes(5));
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
        AppiumSession.Driver?.Quit();
        AppiumSession.Driver?.Dispose();
        AppiumSession.Driver = null;
        AppiumSession.StopServer();
    }
}
