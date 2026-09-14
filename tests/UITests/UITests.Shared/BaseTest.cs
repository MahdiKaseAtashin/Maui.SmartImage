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
        return (AppiumElement)App.FindElement(MobileBy.AccessibilityId(automationId));
    }

    protected AppiumElement WaitForId(string automationId, TimeSpan? timeout = null)
    {
        WebDriverWait wait = new(App, timeout ?? TimeSpan.FromSeconds(30))
        {
            PollingInterval = TimeSpan.FromMilliseconds(250)
        };
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));

        return wait.Until(driver =>
        {
            try
            {
                AppiumElement element = (AppiumElement)driver.FindElement(MobileBy.AccessibilityId(automationId));
                return element.Displayed ? element : null;
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        })!;
    }

    protected void WaitForState(string automationId, string expectedState, TimeSpan? timeout = null)
    {
        WebDriverWait wait = new(App, timeout ?? TimeSpan.FromSeconds(45))
        {
            PollingInterval = TimeSpan.FromMilliseconds(400)
        };
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));

        wait.Until(driver =>
        {
            try
            {
                AppiumElement element = (AppiumElement)driver.FindElement(MobileBy.AccessibilityId(automationId));
                string? actual = ReadState(element);
                return string.Equals(actual, expectedState, StringComparison.OrdinalIgnoreCase);
            }
            catch (NoSuchElementException)
            {
                return false;
            }
        });
    }

    protected static string? ReadState(AppiumElement element)
    {
        // Platform attribute names differ; try common mappings from SemanticProperties / AutomationProperties.
        string[] attributes =
        [
            "contentDescription",
            "content-desc",
            "name",
            "label",
            "Value.Value",
            "Description",
            "HelpText"
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
        foreach (IWebElement webElement in App.FindElements(MobileBy.AccessibilityId(automationId)))
        {
            try
            {
                AppiumElement element = (AppiumElement)webElement;
                if (element.Displayed)
                {
                    return element;
                }
            }
            catch (StaleElementReferenceException)
            {
                // Ignore.
            }
        }

        return null;
    }

    protected AppiumElement WaitForDisplayedById(string automationId, TimeSpan? timeout = null)
    {
        WebDriverWait wait = new(App, timeout ?? TimeSpan.FromSeconds(45))
        {
            PollingInterval = TimeSpan.FromMilliseconds(400)
        };

        return wait.Until(_ => FindDisplayedById(automationId))!;
    }
}
