using System.Text.Json;
using System.Text.Json.Serialization;

namespace CmdPalDockPlus.Core.Profiles;

public sealed class ProfileStore(string path) : IProfileStore
{
    private static readonly JsonSerializerOptions Options = CreateOptions();
    private readonly string _path = path;

    public async ValueTask<ProfileDocument> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return ProfileDocument.Empty;
        }

        await using var stream = File.OpenRead(_path);
        var document = await JsonSerializer.DeserializeAsync<ProfileDocument>(stream, Options, cancellationToken).ConfigureAwait(false)
            ?? ProfileDocument.Empty;
        var errors = ProfileValidator.ValidateDocument(document);
        if (errors.Count != 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        }

        return document;
    }

    public async ValueTask SaveAsync(ProfileDocument document, CancellationToken cancellationToken)
    {
        var errors = ProfileValidator.ValidateDocument(document);
        if (errors.Count != 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        }

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = _path + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
        {
            await JsonSerializer.SerializeAsync(stream, document, Options, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temp, _path, overwrite: true);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
