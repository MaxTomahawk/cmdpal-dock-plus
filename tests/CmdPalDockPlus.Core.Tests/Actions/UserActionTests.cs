using CmdPalDockPlus.Core.Actions;
using FluentAssertions;

namespace CmdPalDockPlus.Core.Tests.Actions;

public sealed class UserActionTests
{
    [Fact]
    public void ShellModeIsExplicitRatherThanImplicit()
    {
        var direct = new UserActionDefinition("build", "Build", UserActionKind.Process, "dotnet", "build", null);
        var shell = new UserActionDefinition("unsafe", "Shell", UserActionKind.Shell, "cmd.exe", "/c echo hi", null);

        UserActionValidator.Validate(direct).Errors.Should().BeEmpty();
        UserActionValidator.Validate(shell).Warnings.Should().Contain("action.shell.explicit-risk");
    }
}
