using Xunit;
using Maui.SmartImage.Services;
using FluentAssertions;

namespace Maui.SmartImage.Tests;

public class LoadGenerationGuardTests
{
    [Fact]
    public void IsCurrent_ForTheGenerationJustBegun_ReturnsTrue()
    {
        LoadGenerationGuard guard = new();

        long generation = guard.Begin();

        guard.IsCurrent(generation).Should().BeTrue();
    }

    [Fact]
    public void IsCurrent_AfterANewerGenerationBegins_ReturnsFalseForTheOlderOne()
    {
        LoadGenerationGuard guard = new();

        long firstGeneration = guard.Begin();
        long secondGeneration = guard.Begin();

        guard.IsCurrent(firstGeneration).Should().BeFalse();
        guard.IsCurrent(secondGeneration).Should().BeTrue();
    }

    [Fact]
    public void Begin_CalledMultipleTimes_ProducesIncreasingGenerations()
    {
        LoadGenerationGuard guard = new();

        long first = guard.Begin();
        long second = guard.Begin();
        long third = guard.Begin();

        second.Should().BeGreaterThan(first);
        third.Should().BeGreaterThan(second);
    }

    [Fact]
    public void IsCurrent_WithUnknownGeneration_ReturnsFalse()
    {
        LoadGenerationGuard guard = new();
        guard.Begin();

        guard.IsCurrent(999).Should().BeFalse();
    }
}
