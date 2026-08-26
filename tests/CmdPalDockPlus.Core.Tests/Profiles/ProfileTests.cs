using CmdPalDockPlus.Core.Profiles;
using FluentAssertions;

namespace CmdPalDockPlus.Core.Tests.Profiles;

public sealed class ProfileTests
{
    [Fact]
    public void ProfileRequiresApplicationTarget()
    {
        var profile = new AppProfile(
            "vscode",
            "Visual Studio Code",
            new ApplicationMatch(null, null),
            GroupingMode.Grouped,
            new DisplayTemplate("{app.name}", ""));

        ProfileValidator.Validate(profile).Errors.Should().Contain("application.target.required");
    }

    [Fact]
    public void ProfileDocumentDefaultsToSchemaOne()
    {
        ProfileDocument.Empty.SchemaVersion.Should().Be(1);
        ProfileDocument.Empty.Profiles.Should().BeEmpty();
    }
}
