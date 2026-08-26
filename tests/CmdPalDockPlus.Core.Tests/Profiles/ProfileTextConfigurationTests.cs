using CmdPalDockPlus.Core.Actions;
using CmdPalDockPlus.Core.Profiles;
using CmdPalDockPlus.Core.Rules;
using FluentAssertions;

namespace CmdPalDockPlus.Core.Tests.Profiles;

public sealed class ProfileTextConfigurationTests
{
    [Fact]
    public void RulesRoundTripPreservesBehavioralShape()
    {
        DockRule[] rules =
        [
            new DockRule(
                "project",
                [new RuleCondition("vscode.workspace", RuleOperator.Equals, "PowerToys")],
                [new SetTitleTemplateAction("PT · {window.title}"), new SeparateAction()]),
        ];

        var json = ProfileTextConfiguration.FormatRules(rules);
        var parsed = ProfileTextConfiguration.ParseRules(json);

        parsed.Should().HaveCount(1);
        parsed[0].Id.Should().Be("project");
        parsed[0].Conditions.Should().ContainSingle().Which.Should().BeEquivalentTo(rules[0].Conditions[0]);
        parsed[0].Actions.Should().HaveCount(2);
        parsed[0].Actions[0].Should().BeEquivalentTo(rules[0].Actions[0]);
        parsed[0].Actions[1].Should().BeOfType<SeparateAction>();
    }

    [Fact]
    public void ActionsRoundTripPreservesFields()
    {
        UserActionDefinition[] actions =
        [
            new("docs", "Open docs", UserActionKind.Uri, "https://example.com/docs", null, null),
            new("tool", "Run tool", UserActionKind.Process, "tool.exe", "--fast", "C:\\Tools"),
        ];

        var parsed = ProfileTextConfiguration.ParseActions(ProfileTextConfiguration.FormatActions(actions));

        parsed.Should().BeEquivalentTo(actions);
    }

    [Fact]
    public void InvalidRegexIsRejectedDuringRuleParsing()
    {
        var json = """
        [{"id":"bad","when":[{"field":"window.title","op":"regex","value":"["}],"then":[{"action":"hide"}]}]
        """;

        var action = () => ProfileTextConfiguration.ParseRules(json);

        action.Should().Throw<ProfileTextConfigurationException>()
            .WithMessage("*invalid*");
    }

    [Fact]
    public void UnknownActionKindIsRejected()
    {
        var json = """
        [{"id":"x","name":"Bad","kind":"magic","target":"x"}]
        """;

        var action = () => ProfileTextConfiguration.ParseActions(json);

        action.Should().Throw<ProfileTextConfigurationException>()
            .WithMessage("*unknown kind*");
    }
}
