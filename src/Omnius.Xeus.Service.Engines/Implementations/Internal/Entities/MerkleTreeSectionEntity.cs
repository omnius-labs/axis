using System;
using System.Linq;
using Omnius.Core.Cryptography;
using Omnius.Xeus.Service.Engines.Internal.Models;

namespace Omnius.Xeus.Service.Engines.Internal.Entities
{
    internal record MerkleTreeSectionEntity
    {
        public int Depth { get; set; }

        public uint BlockLength { get; set; }

        public ulong Length { get; set; }

        public OmniHashEntity[]? Hashes { get; set; }

        public static MerkleTreeSectionEntity Import(MerkleTreeSection value)
        {
            return new MerkleTreeSectionEntity()
            {
                Depth = value.Depth,
                BlockLength = value.BlockLength,
                Length = value.Length,
                Hashes = value.Hashes.Select(n => OmniHashEntity.Import(n)).ToArray(),
            };
        }

        public MerkleTreeSection Export()
        {
            return new MerkleTreeSection(this.Depth, this.BlockLength, this.Length, this.Hashes?.Select(n => n.Export())?.ToArray() ?? Array.Empty<OmniHash>());
        }
    }
}
