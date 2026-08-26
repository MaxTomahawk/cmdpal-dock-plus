using CmdPalDockPlus.Core.Templates;
using FluentAssertions;

namespace CmdPalDockPlus.Core.Tests.Templates;

public sealed class TemplateCompilerTests
{
    [Fact]
    public void FieldSubstitutionEvaluates()
    {
        var template = TemplateCompiler.Compile("prefix {window.title}");
        template.Evaluate(new Dictionary<string, object?> { ["window.title"] = "Editor" })
            .Should().Be("prefix Editor");
    }

    [Fact]
    public void NullCoalesceUsesFallbackAndTracksBothDependencies()
    {
        var template = TemplateCompiler.Compile("{vscode.workspace ?? window.title}");
        template.Dependencies.Should().BeEquivalentTo(["vscode.workspace", "window.title"]);
        template.Evaluate(new Dictionary<string, object?>
        {
            ["vscode.workspace"] = null,
            ["window.title"] = "PowerToys",
        }).Should().Be("PowerToys");
    }

    [Fact]
    public void ChainedNullCoalesceUsesFirstAvailableValueAndTracksAllDependencies()
    {
        var template = TemplateCompiler.Compile("{media.title ?? window.title ?? app.name}");

        template.Dependencies.Should().Equal("media.title", "window.title", "app.name");
        template.Evaluate(new Dictionary<string, object?>
        {
            ["media.title"] = null,
            ["window.title"] = null,
            ["app.name"] = "Player",
        }).Should().Be("Player");
    }

    [Fact]
    public void NumericFormatIsApplied()
    {
        var template = TemplateCompiler.Compile("{process.cpu:0.0}%");
        template.Evaluate(new Dictionary<string, object?> { ["process.cpu"] = 1.74 })
            .Should().Be("1.7%");
    }

    [Fact]
    public void ExecutableSyntaxIsRejected()
    {
        var action = () => TemplateCompiler.Compile("{System.Diagnostics.Process.Start('cmd')}");
        action.Should().Throw<TemplateParseException>();
    }
}
