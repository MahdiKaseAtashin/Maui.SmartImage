using NUnit.Framework;
using OpenQA.Selenium.Appium;

namespace Maui.SmartImage.UITests;

[TestFixture]
public class SmokeTests : BaseTest
{
    [Test]
    public void LocalImage_ReachesLoadedState()
    {
        WaitForSmokeReady();
        WaitForState("Smoke.LocalImage", "Loaded");

        AppiumElement? retry = FindDisplayedById("SmartImage.RetryOverlay");
        Assert.That(retry, Is.Null, "Local image should not show the retry overlay.");
    }

    [Test]
    public void FailedRemote_ReachesFailedState_AndShowsRetry()
    {
        WaitForSmokeReady();
        WaitForState("Smoke.FailedRemote", "Failed");

        AppiumElement retry = WaitForDisplayedById("SmartImage.RetryOverlay");
        Assert.That(retry.Displayed, Is.True);
    }

    [Test]
    public void FailedRemote_RetryTap_DoesNotCrash()
    {
        WaitForSmokeReady();
        WaitForState("Smoke.FailedRemote", "Failed");

        AppiumElement retry = WaitForDisplayedById("SmartImage.RetryOverlay");
        retry.Click();

        // After retry with MaxRetryCount=0 / no auto-retry, failure should return (or briefly Loading then Failed).
        WaitForState("Smoke.FailedRemote", "Failed", TimeSpan.FromSeconds(60));
        Assert.That(WaitForDisplayedById("SmartImage.RetryOverlay").Displayed, Is.True);
        Assert.That(TryFind("Smoke.Title") ?? TryFind("Smoke.Page"), Is.Not.Null);
    }
}
