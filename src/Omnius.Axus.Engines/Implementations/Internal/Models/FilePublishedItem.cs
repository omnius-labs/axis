using Omnius.Axus.Messages;
using Omnius.Core.Cryptography;

namespace Omnius.Axus.Engines.Internal.Models;

internal record FilePublishedItem
{
    public required OmniHash RootHash { get; init; }
    public required string? FilePath { get; init; }
    public required int MaxBlockSize { get; init; }
    public required AttachedProperty? Property { get; init; }
    public required DateTime CreatedTime { get; init; }
    public required DateTime UpdatedTime { get; init; }
}
