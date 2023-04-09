using System.Collections.Immutable;
using Omnius.Axus.Interactors.Internal.Models;
using Omnius.Axus.Interactors.Internal.Repositories;
using Omnius.Axus.Interactors.Models;
using Omnius.Core;
using Omnius.Core.Cryptography;
using Omnius.Core.RocketPack;
using Omnius.Core.Storages;

namespace Omnius.Axus.Interactors;

public sealed partial class SeedDownloader : AsyncDisposableBase, ISeedDownloader
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

    private readonly IProfileDownloader _profileDownloader;
    private readonly IAxusServiceMediator _serviceMediator;
    private readonly IBytesPool _bytesPool;
    private readonly SeedDownloaderOptions _options;

    private readonly SeedBoxDownloaderRepository _seedBoxDownloaderRepo;
    private readonly CachedMemoRepository _cachedMemoRepo;
    private readonly ISingleValueStorage _configStorage;

    private Task _watchLoopTask = null!;

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private readonly AsyncLock _asyncLock = new();

    private const string Channel = "note-v1";
    private const string Zone = "note-downloader-v1";

    public static async ValueTask<MemoDownloader> CreateAsync(IProfileDownloader profileDownloader, IAxusServiceMediator serviceMediator, ISingleValueStorageFactory singleValueStorageFactory, IKeyValueStorageFactory keyValueStorageFactory, IBytesPool bytesPool, MemoDownloaderOptions options, CancellationToken cancellationToken = default)
    {
        var memoDownloader = new MemoDownloader(profileDownloader, serviceMediator, singleValueStorageFactory, keyValueStorageFactory, bytesPool, options);
        await memoDownloader.InitAsync(cancellationToken);
        return memoDownloader;
    }

    private MemoDownloader(IProfileDownloader profileDownloader, IAxusServiceMediator serviceMediator, ISingleValueStorageFactory singleValueStorageFactory, IKeyValueStorageFactory keyValueStorageFactory, IBytesPool bytesPool, MemoDownloaderOptions options)
    {
        _profileDownloader = profileDownloader;
        _serviceMediator = serviceMediator;
        _bytesPool = bytesPool;
        _options = options;

        _seedBoxDownloaderRepo = new NoteBoxDownloaderRepository(Path.Combine(_options.ConfigDirectoryPath, "status"));
        _configStorage = singleValueStorageFactory.Create(Path.Combine(_options.ConfigDirectoryPath, "config"), _bytesPool);
        _cachedMemoRepo = new CachedMemoRepository(Path.Combine(_options.ConfigDirectoryPath, "cached_memos"), _bytesPool);
    }

    internal async ValueTask InitAsync(CancellationToken cancellationToken = default)
    {
        await _seedBoxDownloaderRepo.MigrateAsync(cancellationToken);

        _watchLoopTask = this.WatchLoopAsync(_cancellationTokenSource.Token);
    }

    protected override async ValueTask OnDisposeAsync()
    {
        _cancellationTokenSource.Cancel();
        await _watchLoopTask;
        _cancellationTokenSource.Dispose();

        _seedBoxDownloaderRepo.Dispose();
        _configStorage.Dispose();
    }

    private async Task WatchLoopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);

            for (; ; )
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);

                var signatures = await this.GetSignaturesAsync(cancellationToken);

                await this.SyncMemoDownloaderRepo(signatures, cancellationToken);

                await this.SyncSubscribedShouts(cancellationToken);
                await this.SyncSubscribedFiles(cancellationToken);

                await this.UpdateCachedMemosAsync(signatures, cancellationToken);
                _cachedMemoRepo.Shrink(signatures);
            }
        }
        catch (OperationCanceledException e)
        {
            _logger.Debug(e, "Operation Canceled");
        }
        catch (Exception e)
        {
            _logger.Error(e, "Unexpected Exception");
        }
    }

    private async ValueTask<ImmutableHashSet<OmniSignature>> GetSignaturesAsync(CancellationToken cancellationToken = default)
    {
        using (await _asyncLock.LockAsync(cancellationToken))
        {
            var builder = ImmutableHashSet.CreateBuilder<OmniSignature>();

            foreach (var signature in await _profileDownloader.GetSignaturesAsync(cancellationToken))
            {
                builder.Add(signature);
            }

            return builder.ToImmutable();
        }
    }

    private async ValueTask SyncMemoDownloaderRepo(ImmutableHashSet<OmniSignature> targetSignatures, CancellationToken cancellationToken = default)
    {
        using (await _asyncLock.LockAsync(cancellationToken))
        {
            foreach (var profileItem in _seedBoxDownloaderRepo.BarkItems.FindAll())
            {
                if (targetSignatures.Contains(profileItem.Signature)) continue;
                _seedBoxDownloaderRepo.BarkItems.Delete(profileItem.Signature);
            }

            foreach (var signature in targetSignatures)
            {
                if (_seedBoxDownloaderRepo.BarkItems.Exists(signature)) continue;

                var newBarkItem = new NoteBoxDownloadingItem()
                {
                    Signature = signature,
                    ShoutUpdatedTime = DateTime.MinValue,
                };
                _seedBoxDownloaderRepo.BarkItems.Upsert(newBarkItem);
            }
        }
    }

    private async ValueTask SyncSubscribedShouts(CancellationToken cancellationToken = default)
    {
        using (await _asyncLock.LockAsync(cancellationToken))
        {
            var reports = await _serviceMediator.GetSubscribedShoutReportsAsync(Zone, cancellationToken);
            var signatures = reports.Select(n => n.Signature).ToHashSet();

            foreach (var signature in signatures)
            {
                if (_seedBoxDownloaderRepo.BarkItems.Exists(signature)) continue;
                await _serviceMediator.UnsubscribeShoutAsync(signature, Channel, Zone, cancellationToken);
            }

            foreach (var memoItem in _seedBoxDownloaderRepo.BarkItems.FindAll())
            {
                if (signatures.Contains(memoItem.Signature)) continue;
                await _serviceMediator.SubscribeShoutAsync(memoItem.Signature, Channel, Zone, cancellationToken);
            }

            foreach (var memoItem in _seedBoxDownloaderRepo.BarkItems.FindAll())
            {
                using var shout = await _serviceMediator.TryExportShoutAsync(memoItem.Signature, Channel, memoItem.ShoutUpdatedTime, cancellationToken);
                if (shout is null) continue;

                var rootHash = RocketMessage.FromBytes<OmniHash>(shout.Value.Memory);

                var newBarkItem = memoItem with
                {
                    RootHash = rootHash,
                    ShoutUpdatedTime = shout.UpdatedTime.ToDateTime(),
                };
                _seedBoxDownloaderRepo.BarkItems.Upsert(newBarkItem);
            }
        }
    }

    private async ValueTask SyncSubscribedFiles(CancellationToken cancellationToken = default)
    {
        using (await _asyncLock.LockAsync(cancellationToken))
        {
            var reports = await _serviceMediator.GetSubscribedFileReportsAsync(Zone, cancellationToken);
            var rootHashes = reports.Select(n => n.RootHash).ToHashSet();

            foreach (var rootHash in rootHashes)
            {
                if (_seedBoxDownloaderRepo.BarkItems.Exists(rootHash)) continue;
                await _serviceMediator.UnpublishFileFromMemoryAsync(rootHash, Zone, cancellationToken);
            }

            foreach (var rootHash in _seedBoxDownloaderRepo.BarkItems.FindAll().Select(n => n.RootHash))
            {
                if (rootHash == OmniHash.Empty) continue;
                if (rootHashes.Contains(rootHash)) continue;
                await _serviceMediator.SubscribeFileAsync(rootHash, Zone, cancellationToken);
            }
        }
    }

    private async ValueTask UpdateCachedMemosAsync(ImmutableHashSet<OmniSignature> targetSignatures, CancellationToken cancellationToken = default)
    {
        using (await _asyncLock.LockAsync(cancellationToken))
        {
            foreach (var signature in targetSignatures)
            {
                var cachedContentShoutUpdatedTime = _cachedMemoRepo.FetchShoutUpdatedTime(signature);

                var cachedContent = await this.TryInternalExportAsync(signature, cachedContentShoutUpdatedTime, cancellationToken);
                if (cachedContent is null) continue;

                _cachedMemoRepo.UpsertBulk(cachedContent);
            }
        }
    }

    private async ValueTask<CachedNoteBox?> TryInternalExportAsync(OmniSignature signature, DateTime cachedContentShoutUpdatedTime, CancellationToken cancellationToken = default)
    {
        using (await _asyncLock.LockAsync(cancellationToken))
        {
            await Task.Delay(0, cancellationToken).ConfigureAwait(false);

            var memoItem = _seedBoxDownloaderRepo.BarkItems.FindOne(signature);
            if (memoItem is null || memoItem.RootHash == OmniHash.Empty || memoItem.ShoutUpdatedTime <= cachedContentShoutUpdatedTime) return null;

            using var contentBytes = await _serviceMediator.TryExportFileToMemoryAsync(memoItem.RootHash, cancellationToken);
            if (contentBytes is null) return null;

            var content = RocketMessage.FromBytes<NoteBox>(contentBytes.Memory);

            var cachedContent = new CachedNoteBox(signature, Timestamp64.FromDateTime(memoItem.ShoutUpdatedTime), content);
            return cachedContent;
        }
    }

    public async ValueTask<IEnumerable<NoteReport>> FindMessagesByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        using (await _asyncLock.LockAsync(cancellationToken))
        {
            var results = _cachedMemoRepo.FetchMemoByTag(tag);
            return results.Select(n => n.ToReport());
        }
    }

    public async ValueTask<NoteReport?> FindMessageBySelfHashAsync(OmniHash selfHash, CancellationToken cancellationToken = default)
    {
        using (await _asyncLock.LockAsync(cancellationToken))
        {
            var result = _cachedMemoRepo.FetchMemoBySelfHash(selfHash);
            return result?.ToReport();
        }
    }

    public async ValueTask<MemoDownloaderConfig> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        using (await _asyncLock.LockAsync(cancellationToken))
        {
            var config = await _configStorage.TryGetValueAsync<MemoDownloaderConfig>(cancellationToken);

            if (config is null)
            {
                config = new MemoDownloaderConfig(
                    tags: Array.Empty<Utf8String>(),
                    maxMemoCount: 10000
                );

                await _configStorage.TrySetValueAsync(config, cancellationToken);
            }

            return config;
        }
    }

    public async ValueTask SetConfigAsync(MemoDownloaderConfig config, CancellationToken cancellationToken = default)
    {
        using (await _asyncLock.LockAsync(cancellationToken))
        {
            await _configStorage.TrySetValueAsync(config, cancellationToken);
        }
    }

    public ValueTask<FindSeedsResult> FindSeedsAsync(FindSeedsCondition condition, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    ValueTask<SeedDownloaderConfig> ISeedDownloader.GetConfigAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask SetConfigAsync(SeedDownloaderConfig config, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
