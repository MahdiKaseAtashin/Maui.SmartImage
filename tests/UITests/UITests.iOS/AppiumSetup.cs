using NUnit.Framework;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.iOS;

namespace Maui.SmartImage.UITests;

[SetUpFixture]
public class AppiumSetup
{
    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        string appPath = AppiumSession.RequireEnv("IOS_APP_PATH");
        if (!Directory.Exists(appPath) && !File.Exists(appPath))
        {
            throw new FileNotFoundException($"iOS app bundle not found at '{appPath}'.", appPath);
        }

        AppiumSession.EnsureServer();

        AppiumOptions options = new();
        options.PlatformName = "iOS";
        options.AutomationName = "XCUITest";
        options.App = appPath;
        options.AddAdditionalAppiumOption("newCommandTimeout", 300);
        options.AddAdditionalAppiumOption("autoAcceptAlerts", true);

        string? deviceName = Environment.GetEnvironmentVariable("IOS_DEVICE_NAME");
        options.DeviceName = string.IsNullOrWhiteSpace(deviceName) ? "iPhone 15" : deviceName;

        string? udid = Environment.GetEnvironmentVariable("IOS_UDID");
        if (!string.IsNullOrWhiteSpace(udid))
        {
            options.AddAdditionalAppiumOption("udid", udid);
        }

        string? platformVersion = Environment.GetEnvironmentVariable("IOS_PLATFORM_VERSION");
        if (!string.IsNullOrWhiteSpace(platformVersion))
        {
            options.PlatformVersion = platformVersion;
        }

        options.AddAdditionalAppiumOption("wdaLaunchTimeout", 180000);
        options.AddAdditionalAppiumOption("wdaConnectionTimeout", 180000);

        AppiumSession.Driver = new IOSDriver(AppiumSession.ServerUri, options, TimeSpan.FromMinutes(5));
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
