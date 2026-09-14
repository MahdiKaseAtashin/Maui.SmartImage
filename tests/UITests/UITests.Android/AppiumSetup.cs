using NUnit.Framework;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace Maui.SmartImage.UITests;

[SetUpFixture]
public class AppiumSetup
{
    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        string appPath = AppiumSession.RequireEnv("ANDROID_APP_PATH");
        if (!File.Exists(appPath))
        {
            throw new FileNotFoundException($"Android APK not found at '{appPath}'.", appPath);
        }

        AppiumOptions options = new();
        options.PlatformName = "Android";
        options.AutomationName = "UiAutomator2";
        options.App = appPath;
        options.AddAdditionalAppiumOption("newCommandTimeout", 300);
        options.AddAdditionalAppiumOption("autoGrantPermissions", true);
        options.AddAdditionalAppiumOption("disableWindowAnimation", true);
        options.AddAdditionalAppiumOption("appWaitActivity", "*");

        // Prefer an already-running emulator/device when DEVICE_NAME is set.
        string? deviceName = Environment.GetEnvironmentVariable("ANDROID_DEVICE_NAME");
        if (!string.IsNullOrWhiteSpace(deviceName))
        {
            options.DeviceName = deviceName;
        }

        AppiumSession.Driver = new AndroidDriver(AppiumSession.ServerUri, options, TimeSpan.FromMinutes(5));
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
        AppiumSession.Driver?.Quit();
        AppiumSession.Driver?.Dispose();
        AppiumSession.Driver = null;
    }
}
