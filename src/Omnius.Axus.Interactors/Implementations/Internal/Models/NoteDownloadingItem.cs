using Omnius.Core.Cryptography;

namespace Omnius.Axus.Interactors.Internal.Models;

internal record NoteBoxDownloadingItem
{
    public OmniSignature Signature { get; init; } = OmniSignature.Empty;
    public OmniHash RootHash { get; init; }
    public DateTime ShoutUpdatedTime { get; init; }
}
