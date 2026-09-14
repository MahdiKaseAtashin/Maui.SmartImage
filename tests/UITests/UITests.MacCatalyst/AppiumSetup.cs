using NUnit.Framework;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Mac;

namespace Maui.SmartImage.UITests;

[SetUpFixture]
public class AppiumSetup
{
    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        string appPath = AppiumSession.RequireEnv("MACCATALYST_APP_PATH");
        if (!Directory.Exists(appPath) && !File.Exists(appPath))
        {
            throw new FileNotFoundException($"MacCatalyst app not found at '{appPath}'.", appPath);
        }

        AppiumSession.EnsureServer();

        AppiumOptions options = new();
        options.PlatformName = "Mac";
        options.AutomationName = "Mac2";
        options.App = appPath;
        options.AddAdditionalAppiumOption("newCommandTimeout", 300);
        options.AddAdditionalAppiumOption("arguments", new[] { "--smoke" });

        AppiumSession.Driver = new MacDriver(AppiumSession.ServerUri, options, TimeSpan.FromMinutes(5));
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
