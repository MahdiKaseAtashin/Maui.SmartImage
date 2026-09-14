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

        try
        {
            wait.Until(_ =>
                TryFind("Smoke.Title")
                ?? TryFind("Smoke.Page")
                ?? TryFindByName("UI Smoke"));
        }
        catch (WebDriverTimeoutException ex)
        {
            throw new WebDriverTimeoutException(
                $"{ex.Message}{Environment.NewLine}PageSource:{Environment.NewLine}{TruncatePageSource()}",
                ex);
        }
    }

    protected AppiumElement WaitForId(string automationId, TimeSpan? timeout = null)
    {
        WebDriverWait wait = new(App, timeout ?? TimeSpan.FromSeconds(45))
        {
            PollingInterval = TimeSpan.FromMilliseconds(250)
        };
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));

        try
        {
            return wait.Until(_ => TryFind(automationId))!;
        }
        catch (WebDriverTimeoutException ex)
        {
            throw new WebDriverTimeoutException(
                $"Timed out waiting for '{automationId}'.{Environment.NewLine}PageSource:{Environment.NewLine}{TruncatePageSource()}",
                ex);
        }
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
            }
        }

        return null;
    }

    protected AppiumElement? FindDisplayedById(string automationId)
    {
        AppiumElement? element = TryFind(automationId, requireDisplayed: true);
        return element;
    }

    protected AppiumElement WaitForDisplayedById(string automationId, TimeSpan? timeout = null)
    {
        WebDriverWait wait = new(App, timeout ?? TimeSpan.FromSeconds(45))
        {
            PollingInterval = TimeSpan.FromMilliseconds(400)
        };

        try
        {
            return wait.Until(_ => FindDisplayedById(automationId))!;
        }
        catch (WebDriverTimeoutException ex)
        {
            throw new WebDriverTimeoutException(
                $"Timed out waiting for displayed '{automationId}'.{Environment.NewLine}PageSource:{Environment.NewLine}{TruncatePageSource()}",
                ex);
        }
    }

    protected AppiumElement? TryFind(string automationId, bool requireDisplayed = false)
    {
        foreach (By by in LocatorStrategies(automationId))
        {
            try
            {
                AppiumElement element = (AppiumElement)App.FindElement(by);
                if (requireDisplayed)
                {
                    if (element.Displayed)
                    {
                        return element;
                    }

                    continue;
                }

                return element;
            }
            catch (WebDriverException)
            {
            }
        }

        return null;
    }

    private AppiumElement? TryFindByName(string name)
    {
        try
        {
            AppiumElement element = (AppiumElement)App.FindElement(By.Name(name));
            return element;
        }
        catch (WebDriverException)
        {
            return null;
        }
    }

    private string TruncatePageSource(int maxChars = 8000)
    {
        try
        {
            string source = App.PageSource ?? string.Empty;
            if (source.Length <= maxChars)
            {
                return source;
            }

            return source[..maxChars] + $"{Environment.NewLine}... truncated ({source.Length} chars total)";
        }
        catch (Exception ex)
        {
            return $"(failed to read PageSource: {ex.Message})";
        }
    }

    private static IEnumerable<By> LocatorStrategies(string automationId)
    {
        yield return MobileBy.AccessibilityId(automationId);
        yield return MobileBy.Id(automationId);
        yield return By.Name(automationId);

        string escaped = EscapeForXPath(automationId);
        yield return By.XPath($"//*[@content-desc={escaped} or @contentDescription={escaped} or @name={escaped} or @label={escaped} or @value={escaped} or @text={escaped} or text()={escaped}]");
    }

    private static string EscapeForXPath(string value)
    {
        if (!value.Contains('\''))
        {
            return $"'{value}'";
        }

        if (!value.Contains('"'))
        {
            return $"\"{value}\"";
        }

        return "concat('" + value.Replace("'", "',\"'\",'") + "')";
    }
}
