namespace CmdPalDockPlus.Core.Providers;

public sealed record ProviderDependencyResult(
    IReadOnlyDictionary<string, IReadOnlySet<string>> FieldsByProvider,
    IReadOnlyList<string> Errors);

public static class ProviderDependencyResolver
{
    public static ProviderDependencyResult Resolve(IEnumerable<string> requestedFields, ProviderCatalog catalog)
    {
        var byProvider = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var errors = new List<string>();

        foreach (var field in requestedFields.Distinct(StringComparer.Ordinal))
        {
            var descriptor = catalog.Providers.FirstOrDefault(provider => provider.Fields.Contains(field));
            if (descriptor is null)
            {
                errors.Add($"provider.field.unknown:{field}");
                continue;
            }

            if (!byProvider.TryGetValue(descriptor.Id, out var fields))
            {
                fields = new HashSet<string>(StringComparer.Ordinal);
                byProvider.Add(descriptor.Id, fields);
            }

            fields.Add(field);
        }

        return new(
            byProvider.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlySet<string>)kvp.Value, StringComparer.Ordinal),
            errors);
    }
}
