using Omnius.Core.Cryptography;
using Omnius.Xeus.Engines.Internal.Models;
using Omnius.Xeus.Models;

namespace Omnius.Xeus.Engines.Internal.Entities;

internal record DecodedFileItemEntity
{
    public OmniHashEntity? RootHash { get; set; }

    public MerkleTreeSectionEntity[]? MerkleTreeSections { get; set; }

    public int State { get; set; }

    public static DecodedFileItemEntity Import(DecodedFileItem value)
    {
        return new DecodedFileItemEntity()
        {
            RootHash = OmniHashEntity.Import(value.RootHash),
            MerkleTreeSections = value.MerkleTreeSections?.Select(n => MerkleTreeSectionEntity.Import(n))?.ToArray() ?? Array.Empty<MerkleTreeSectionEntity>(),
            State = (int)value.State,
        };
    }

    public DecodedFileItem Export()
    {
        return new DecodedFileItem(this.RootHash?.Export() ?? OmniHash.Empty, this.MerkleTreeSections?.Select(n => n.Export())?.ToArray() ?? Array.Empty<MerkleTreeSection>(), (SubscribedFileState)this.State);
    }
}
