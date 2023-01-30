using System.Buffers;
using Omnius.Axus.Engines.Primitives;
using Omnius.Axus.Messages;
using Omnius.Core.Cryptography;

namespace Omnius.Axus.Engines;

public interface ISubscribedFileStorage : IWritableFileStorage, IAsyncDisposable
{
    ValueTask<IEnumerable<SubscribedFileReport>> GetSubscribedFileReportsAsync(string zone, CancellationToken cancellationToken = default);
    ValueTask<IEnumerable<OmniHash>> GetWantRootHashesAsync(CancellationToken cancellationToken = default);
    ValueTask<IEnumerable<OmniHash>> GetWantBlockHashesAsync(OmniHash rootHash, CancellationToken cancellationToken = default);
    ValueTask<bool> ContainsWantContentAsync(OmniHash rootHash, CancellationToken cancellationToken = default);
    ValueTask<bool> ContainsWantBlockAsync(OmniHash rootHash, OmniHash blockHash, CancellationToken cancellationToken = default);
    ValueTask SubscribeFileAsync(OmniHash rootHash, string zone, CancellationToken cancellationToken = default);
    ValueTask UnsubscribeFileAsync(OmniHash rootHash, string zone, CancellationToken cancellationToken = default);
    ValueTask<bool> TryExportFileAsync(OmniHash rootHash, string filePath, CancellationToken cancellationToken = default);
    ValueTask<bool> TryExportFileAsync(OmniHash rootHash, IBufferWriter<byte> bufferWriter, CancellationToken cancellationToken = default);
}
