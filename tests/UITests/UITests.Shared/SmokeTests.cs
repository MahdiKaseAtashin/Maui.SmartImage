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
        WaitForId("Smoke.LocalImage.Loaded", TimeSpan.FromSeconds(60));

        AppiumElement? retry = FindDisplayedById("Smoke.LocalImage.RetryOverlay");
        Assert.That(retry, Is.Null, "Local image should not show the retry overlay.");
    }

    [Test]
    public void FailedRemote_ReachesFailedState_AndShowsRetry()
    {
        WaitForSmokeReady();
        WaitForId("Smoke.FailedRemote.Failed", TimeSpan.FromSeconds(60));

        AppiumElement retry = WaitForDisplayedById("Smoke.FailedRemote.RetryOverlay");
        Assert.That(retry.Displayed, Is.True);
    }

    [Test]
    public void FailedRemote_RetryTap_DoesNotCrash()
    {
        WaitForSmokeReady();
        WaitForId("Smoke.FailedRemote.Failed", TimeSpan.FromSeconds(60));

        AppiumElement retry = WaitForDisplayedById("Smoke.FailedRemote.RetryOverlay");
        retry.Click();

        WaitForId("Smoke.FailedRemote.Failed", TimeSpan.FromSeconds(60));
        Assert.That(WaitForDisplayedById("Smoke.FailedRemote.RetryOverlay").Displayed, Is.True);
        Assert.That(TryFind("Smoke.Title") ?? TryFind("Smoke.Page"), Is.Not.Null);
    }
}
