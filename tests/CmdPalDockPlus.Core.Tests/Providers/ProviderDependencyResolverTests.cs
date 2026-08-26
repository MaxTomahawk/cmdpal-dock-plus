using CmdPalDockPlus.Core.Providers;
using FluentAssertions;

namespace CmdPalDockPlus.Core.Tests.Providers;

public sealed class ProviderDependencyResolverTests
{
    [Fact]
    public void ResolvesOnlyProvidersNeededByFields()
    {
        var catalog = new ProviderCatalog([
            new ProviderDescriptor("window", ["window.title", "window.state"]),
            new ProviderDescriptor("process", ["process.cpu", "process.memory"]),
            new ProviderDescriptor("vscode", ["vscode.workspace"]),
        ]);

        var result = ProviderDependencyResolver.Resolve(["window.title", "vscode.workspace"], catalog);

        result.Errors.Should().BeEmpty();
        result.FieldsByProvider.Keys.Should().BeEquivalentTo(["window", "vscode"]);
        result.FieldsByProvider["window"].Should().BeEquivalentTo(["window.title"]);
    }

    [Fact]
    public void UnknownFieldIsValidationError()
    {
        var catalog = new ProviderCatalog([new ProviderDescriptor("window", ["window.title"])]);
        var result = ProviderDependencyResolver.Resolve(["foo.bar"], catalog);
        result.Errors.Should().Contain("provider.field.unknown:foo.bar");
    }
}
