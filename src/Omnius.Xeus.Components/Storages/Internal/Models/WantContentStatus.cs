using Omnius.Core.Cryptography;

namespace Omnius.Xeus.Components.Storages.Internal.Models
{
    internal sealed class WantContentStatus
    {
        public WantContentStatus(OmniHash hash)
        {
            this.Hash = hash;
        }

        public OmniHash Hash { get; }
    }
}