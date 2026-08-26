using CmdPalDockPlus.Windows.Destinations;
using FluentAssertions;

namespace CmdPalDockPlus.Windows.Tests.Destinations;

public sealed class DestinationDeduplicatorTests
{
    [Fact]
    public void DuplicateCanonicalTargetsAreCollapsed()
    {
        var values = new[]
        {
            new AppDestination("a", "Repo", @"D:\Repo", null, DestinationKind.Recent),
            new AppDestination("b", "Repo duplicate", @"D:\Repo\", null, DestinationKind.Recent),
            new AppDestination("c", "Other", @"D:\Other", null, DestinationKind.Recent),
        };

        DestinationDeduplicator.Deduplicate(values, 10)
            .Select(value => value.DisplayName)
            .Should().Equal("Repo", "Other");
    }

    [Fact]
    public void DifferentArgumentsRemainDistinct()
    {
        var values = new[]
        {
            new AppDestination("a", "One", @"C:\tool.exe", "--one", DestinationKind.Frequent),
            new AppDestination("b", "Two", @"C:\tool.exe", "--two", DestinationKind.Frequent),
        };

        DestinationDeduplicator.Deduplicate(values, 10).Should().HaveCount(2);
    }

    [Fact]
    public void LimitIsAppliedAfterDeduplication()
    {
        var values = Enumerable.Range(0, 5)
            .Select(index => new AppDestination($"id-{index}", $"Item {index}", $@"C:\Item{index}", null, DestinationKind.Recent));

        DestinationDeduplicator.Deduplicate(values, 2).Should().HaveCount(2);
    }
}
