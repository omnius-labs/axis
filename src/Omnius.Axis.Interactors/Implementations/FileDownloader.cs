using Omnius.Axis.Interactors.Internal.Models;
using Omnius.Axis.Interactors.Internal.Repositories;
using Omnius.Axis.Interactors.Models;
using Omnius.Axis.Models;
using Omnius.Core;
using Omnius.Core.Helpers;
using Omnius.Core.Storages;

namespace Omnius.Axis.Interactors;

public sealed class FileDownloader : AsyncDisposableBase, IFileDownloader
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

    private readonly IAxisServiceMediator _service;
    private readonly IBytesPool _bytesPool;
    private readonly FileDownloaderOptions _options;

    private readonly ISingleValueStorage _configStorage;
    private readonly FileDownloaderRepository _fileDownloaderRepo;

    private Task _watchLoopTask = null!;

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private readonly AsyncLock _asyncLock = new();

    private const string Registrant = "Omnius.Axis.Interactors.FileDownloader";

    public static async ValueTask<FileDownloader> CreateAsync(IAxisServiceMediator service, ISingleValueStorageFactory singleValueStorageFactory, IBytesPool bytesPool, FileDownloaderOptions options, CancellationToken cancellationToken = default)
    {
        var fileDownloader = new FileDownloader(service, singleValueStorageFactory, bytesPool, options);
        await fileDownloader.InitAsync(cancellationToken);
        return fileDownloader;
    }

    private FileDownloader(IAxisServiceMediator service, ISingleValueStorageFactory singleValueStorageFactory, IBytesPool bytesPool, FileDownloaderOptions options)
    {
        _service = service;
        _bytesPool = bytesPool;
        _options = options;

        _configStorage = singleValueStorageFactory.Create(Path.Combine(_options.ConfigDirectoryPath, "config"), _bytesPool);
        _fileDownloaderRepo = new FileDownloaderRepository(Path.Combine(_options.ConfigDirectoryPath, "status"));
    }

    private async ValueTask InitAsync(CancellationToken cancellationToken = default)
    {
        await _fileDownloaderRepo.MigrateAsync(cancellationToken);

        _watchLoopTask = this.WatchLoopAsync(_cancellationTokenSource.Token);
    }

    protected override async ValueTask OnDisposeAsync()
    {
        _cancellationTokenSource.Cancel();
        await _watchLoopTask;
        _cancellationTokenSource.Dispose();

        _fileDownloaderRepo.Dispose();
    }

    private async Task WatchLoopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);

            for (; ; )
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);

                await this.SyncSubscribedFiles(cancellationToken);
                await this.TryExportSubscribedFiles(cancellationToken);
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

    private async ValueTask SyncSubscribedFiles(CancellationToken cancellationToken = default)
    {
        using (await _asyncLock.LockAsync(cancellationToken))
        {
            var reports = await _service.GetSubscribedFileReportsAsync(cancellationToken);
            var hashes = reports
                .Where(n => n.Registrant == Registrant)
                .Select(n => n.RootHash)
                .ToHashSet();

            foreach (var hash in hashes)
            {
                if (_fileDownloaderRepo.Items.Exists(hash)) continue;
                await _service.UnsubscribeFileAsync(hash, Registrant, cancellationToken);
            }

            foreach (var seed in _fileDownloaderRepo.Items.FindAll().Select(n => n.FileSeed))
            {
                if (hashes.Contains(seed.RootHash)) continue;
                await _service.SubscribeFileAsync(seed.RootHash, Registrant, cancellationToken);
            }
        }
    }

    private async ValueTask TryExportSubscribedFiles(CancellationToken cancellationToken = default)
    {
        using (await _asyncLock.LockAsync(cancellationToken))
        {
            var config = await this.GetConfigAsync(cancellationToken);
            var basePath = Path.GetFullPath(config.DestinationDirectory);

            var reports = await _service.GetSubscribedFileReportsAsync(cancellationToken);
            var reportMap = reports
                .Where(n => n.Registrant == Registrant)
                .ToDictionary(n => n.RootHash);

            foreach (var item in _fileDownloaderRepo.Items.FindAll())
            {
                if (item.State == DownloadingFileState.Completed) continue;

                if (!reportMap.TryGetValue(item.FileSeed.RootHash, out var report)) continue;
                if (report.Status.State != SubscribedFileState.Downloaded) continue;

                DirectoryHelper.CreateDirectory(basePath);
                var filePath = Path.Combine(basePath, item.FileSeed.Name);

                if (await _service.TryExportFileToStorageAsync(item.FileSeed.RootHash, filePath, cancellationToken))
                {
                    var newItem = new DownloadingFileItem(item.FileSeed, filePath, item.CreatedTime, DownloadingFileState.Completed);
                    _fileDownloaderRepo.Items.Upsert(newItem);
                }
            }
        }
    }

    public async ValueTask<IEnumerable<DownloadingFileReport>> GetDownloadingFileReportsAsync(CancellationToken cancellationToken = default)
    {
        using (await _asyncLock.LockAsync(cancellationToken))
        {
            var results = new List<DownloadingFileReport>();

            var reports = await _service.GetSubscribedFileReportsAsync(cancellationToken);
            var reportMap = reports
                .Where(n => n.Registrant == Registrant)
                .ToDictionary(n => n.RootHash);

            foreach (var item in _fileDownloaderRepo.Items.FindAll())
            {
                if (!reportMap.TryGetValue(item.FileSeed.RootHash, out var report)) continue;

                var status = new DownloadingFileStatus(report.Status.CurrentDepth, report.Status.DownloadedBlockCount,
                    report.Status.TotalBlockCount, item.State);
                results.Add(new DownloadingFileReport(item.FileSeed, item.FilePath, item.CreatedTime, status));
            }

            return results;
        }
    }

    public async ValueTask RegisterAsync(FileSeed fileSeed, CancellationToken cancellationToken = default)
    {
        using (await _asyncLock.LockAsync(cancellationToken))
        {
            if (_fileDownloaderRepo.Items.Exists(fileSeed)) return;

            var now = DateTime.UtcNow;
            var item = new DownloadingFileItem(fileSeed, null, now, DownloadingFileState.Downloading);
            _fileDownloaderRepo.Items.Upsert(item);
        }
    }

    public async ValueTask UnregisterAsync(FileSeed fileSeed, CancellationToken cancellationToken = default)
    {
        using (await _asyncLock.LockAsync(cancellationToken))
        {
            if (!_fileDownloaderRepo.Items.Exists(fileSeed)) return;

            _fileDownloaderRepo.Items.Delete(fileSeed);
        }
    }

    public async ValueTask<FileDownloaderConfig> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        using (await _asyncLock.LockAsync(cancellationToken))
        {
            var config = await _configStorage.TryGetValueAsync<FileDownloaderConfig>(cancellationToken);

            if (config is null)
            {
                config = new FileDownloaderConfig(
                    destinationDirectory: "../downloads"
                );

                await _configStorage.TrySetValueAsync(config, cancellationToken);
            }

            return config;
        }
    }

    public async ValueTask SetConfigAsync(FileDownloaderConfig config, CancellationToken cancellationToken = default)
    {
        using (await _asyncLock.LockAsync(cancellationToken))
        {
            await _configStorage.TrySetValueAsync(config, cancellationToken);
        }
    }
}
