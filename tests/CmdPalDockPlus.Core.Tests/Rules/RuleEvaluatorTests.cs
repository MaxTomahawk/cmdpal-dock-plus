using CmdPalDockPlus.Core.Rules;
using FluentAssertions;

namespace CmdPalDockPlus.Core.Tests.Rules;

public sealed class RuleEvaluatorTests
{
    [Theory]
    [InlineData(RuleOperator.Equals, "PowerToys", "PowerToys", true)]
    [InlineData(RuleOperator.Contains, "PowerToys - Code", "PowerToys", true)]
    [InlineData(RuleOperator.StartsWith, @"D:\Projects\Repo", @"D:\Projects", true)]
    public void StringOperatorsEvaluate(RuleOperator op, string actual, string expected, bool match)
    {
        RuleEvaluator.Matches(
            new RuleCondition("window.title", op, expected),
            new Dictionary<string, object?> { ["window.title"] = actual })
            .Should().Be(match);
    }

    [Fact]
    public void EarlierDisplayOverrideSurvivesTerminalGroupingAction()
    {
        DockRule[] rules =
        [
            new("rename", [new RuleCondition("window.title", RuleOperator.Contains, "Power")], [new SetTitleTemplateAction("Power")]),
            new("split", [new RuleCondition("window.title", RuleOperator.Contains, "Power")], [new SeparateAction()]),
            new("ignored", [], [new SetSubtitleTemplateAction("ignored")]),
        ];

        var result = RuleEvaluator.Evaluate(rules, new Dictionary<string, object?> { ["window.title"] = "PowerToys" });
        result.TitleTemplate.Should().Be("Power");
        result.Grouping.Should().Be(RuleGrouping.Separate);
        result.SubtitleTemplate.Should().BeNull();
    }

    [Fact]
    public void InvalidRegexDoesNotHangAndReturnsFalse()
    {
        RuleEvaluator.Matches(
            new RuleCondition("window.title", RuleOperator.Regex, "("),
            new Dictionary<string, object?> { ["window.title"] = "text" })
            .Should().BeFalse();
    }

    [Fact]
    public void ValidatorRejectsMalformedGroupKeyTemplate()
    {
        var rule = new DockRule("workspace", [], [new GroupAction("{workspace")]);

        RuleValidator.Validate(rule)
            .Should().ContainSingle("rule.template.invalid:workspace:group");
    }

    [Fact]
    public void ValidatorRejectsMalformedPresentationTemplates()
    {
        var rule = new DockRule(
            "presentation",
            [],
            [
                new SetTitleTemplateAction("{window.title"),
                new SetSubtitleTemplateAction("{vscode.workspace"),
                new SetIconTemplateAction("{app.icon"),
            ]);

        RuleValidator.Validate(rule).Should().BeEquivalentTo(
        [
            "rule.template.invalid:presentation:title",
            "rule.template.invalid:presentation:subtitle",
            "rule.template.invalid:presentation:icon",
        ]);
    }
}
