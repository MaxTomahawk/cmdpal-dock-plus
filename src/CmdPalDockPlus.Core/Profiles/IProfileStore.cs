namespace CmdPalDockPlus.Core.Profiles;

public interface IProfileStore
{
    ValueTask<ProfileDocument> LoadAsync(CancellationToken cancellationToken);

    ValueTask SaveAsync(ProfileDocument document, CancellationToken cancellationToken);
}
