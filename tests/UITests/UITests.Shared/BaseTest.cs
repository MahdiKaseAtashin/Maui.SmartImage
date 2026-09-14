using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;

namespace Maui.SmartImage.UITests;

public abstract class BaseTest
{
    protected AppiumDriver App => AppiumSession.Driver
        ?? throw new InvalidOperationException("Appium session was not started. Check AppiumSetup.");

    protected AppiumElement FindById(string automationId)
    {
        return TryFind(automationId)
            ?? throw new NoSuchElementException($"No element found for '{automationId}'.");
    }

    protected void WaitForSmokeReady(TimeSpan? timeout = null)
    {
        WebDriverWait wait = new(App, timeout ?? TimeSpan.FromSeconds(60))
        {
            PollingInterval = TimeSpan.FromMilliseconds(400)
        };
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));

        wait.Until(_ =>
            TryFind("Smoke.Title")
            ?? TryFind("Smoke.Page")
            ?? TryFindByName("UI Smoke"));
    }

    protected AppiumElement WaitForId(string automationId, TimeSpan? timeout = null)
    {
        WebDriverWait wait = new(App, timeout ?? TimeSpan.FromSeconds(45))
        {
            PollingInterval = TimeSpan.FromMilliseconds(250)
        };
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));

        return wait.Until(_ => TryFind(automationId))!;
    }

    protected void WaitForState(string automationId, string expectedState, TimeSpan? timeout = null)
    {
        WebDriverWait wait = new(App, timeout ?? TimeSpan.FromSeconds(60))
        {
            PollingInterval = TimeSpan.FromMilliseconds(400)
        };
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));

        wait.Until(_ =>
        {
            AppiumElement? element = TryFind(automationId);
            if (element is null)
            {
                return false;
            }

            string? actual = ReadState(element);
            return string.Equals(actual, expectedState, StringComparison.OrdinalIgnoreCase);
        });
    }

    protected static string? ReadState(AppiumElement element)
    {
        string[] attributes =
        [
            "contentDescription",
            "content-desc",
            "name",
            "label",
            "Value.Value",
            "Description",
            "HelpText",
            "title",
            "value"
        ];

        foreach (string attribute in attributes)
        {
            try
            {
                string? value = element.GetAttribute(attribute);
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                foreach (string state in new[] { "Loaded", "Failed", "Loading", "Idle" })
                {
                    if (string.Equals(value, state, StringComparison.OrdinalIgnoreCase))
                    {
                        return state;
                    }
                }
            }
            catch (WebDriverException)
            {
                // Try next attribute.
            }
        }

        return null;
    }

    protected AppiumElement? FindDisplayedById(string automationId)
    {
        AppiumElement? element = TryFind(automationId);
        if (element is null)
        {
            return null;
        }

        try
        {
            return element.Displayed ? element : null;
        }
        catch (StaleElementReferenceException)
        {
            return null;
        }
    }

    protected AppiumElement WaitForDisplayedById(string automationId, TimeSpan? timeout = null)
    {
        WebDriverWait wait = new(App, timeout ?? TimeSpan.FromSeconds(45))
        {
            PollingInterval = TimeSpan.FromMilliseconds(400)
        };

        return wait.Until(_ => FindDisplayedById(automationId))!;
    }

    protected AppiumElement? TryFind(string automationId)
    {
        foreach (By by in LocatorStrategies(automationId))
        {
            try
            {
                AppiumElement element = (AppiumElement)App.FindElement(by);
                if (element.Displayed)
                {
                    return element;
                }
            }
            catch (WebDriverException)
            {
                // Try next strategy.
            }
        }

        return null;
    }

    private AppiumElement? TryFindByName(string name)
    {
        try
        {
            AppiumElement element = (AppiumElement)App.FindElement(By.Name(name));
            return element.Displayed ? element : null;
        }
        catch (WebDriverException)
        {
            return null;
        }
    }

    private static IEnumerable<By> LocatorStrategies(string automationId)
    {
        yield return MobileBy.AccessibilityId(automationId);
        yield return By.Name(automationId);
        yield return By.Id(automationId);
    }
}
