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

        // Without bundleId, Mac2 automates Finder instead of the Catalyst app.
        // See: https://learn.microsoft.com/dotnet/maui/deployment/ui-testing
        const string bundleId = "com.mahdi.smartimage.maui.sample";

        AppiumOptions options = new();
        options.PlatformName = "Mac";
        options.AutomationName = "Mac2";
        options.App = appPath;
        options.AddAdditionalAppiumOption("bundleId", bundleId);
        options.AddAdditionalAppiumOption("newCommandTimeout", 300);
        options.AddAdditionalAppiumOption("arguments", new[] { "--smoke" });
        options.AddAdditionalAppiumOption("showServerLogs", true);

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
