using LiteDB;
using Omnius.Axis.Intaractors.Internal.Entities;
using Omnius.Axis.Intaractors.Internal.Models;
using Omnius.Axis.Intaractors.Models;
using Omnius.Axis.Utils;
using Omnius.Core;
using Omnius.Core.Cryptography;
using Omnius.Core.Helpers;

namespace Omnius.Axis.Intaractors.Internal.Repositories;

internal sealed class FileDownloaderRepository : DisposableBase
{
    private readonly LiteDatabase _database;

    public FileDownloaderRepository(string dirPath)
    {
        DirectoryHelper.CreateDirectory(dirPath);

        _database = new LiteDatabase(Path.Combine(dirPath, "lite.db"));
        _database.UtcDate = true;

        this.Items = new DownloadingFileItemRepository(_database);
    }

    protected override void OnDispose(bool disposing)
    {
        _database.Dispose();
    }

    public async ValueTask MigrateAsync(CancellationToken cancellationToken = default)
    {
        await this.Items.MigrateAsync(cancellationToken);
    }

    public DownloadingFileItemRepository Items { get; }

    public sealed class DownloadingFileItemRepository
    {
        private const string CollectionName = "downloading_box_items";

        private readonly LiteDatabase _database;

        private readonly object _lockObject = new();

        public DownloadingFileItemRepository(LiteDatabase database)
        {
            _database = database;
        }

        internal async ValueTask MigrateAsync(CancellationToken cancellationToken = default)
        {
            lock (_lockObject)
            {
                if (_database.GetDocumentVersion(CollectionName) <= 0)
                {
                    var col = this.GetCollection();
                    col.EnsureIndex(x => x.Seed!.RootHash, true);
                }

                _database.SetDocumentVersion(CollectionName, 1);
            }
        }

        private ILiteCollection<DownloadingFileItemEntity> GetCollection()
        {
            var col = _database.GetCollection<DownloadingFileItemEntity>(CollectionName);
            return col;
        }

        public bool Exists(Seed seed)
        {
            lock (_lockObject)
            {
                var seedEntity = SeedEntity.Import(seed);

                var col = this.GetCollection();
                return col.Exists(n => n.Seed == seedEntity);
            }
        }

        public bool Exists(OmniHash rootHash)
        {
            lock (_lockObject)
            {
                var rootHashEntity = OmniHashEntity.Import(rootHash);

                var col = this.GetCollection();
                return col.Exists(n => n.Seed!.RootHash == rootHashEntity);
            }
        }

        public IEnumerable<DownloadingFileItem> FindAll()
        {
            lock (_lockObject)
            {
                var col = this.GetCollection();
                return col.FindAll().Select(n => n.Export()).ToArray();
            }
        }

        public DownloadingFileItem? FindOne(Seed seed)
        {
            lock (_lockObject)
            {
                var seedEntity = SeedEntity.Import(seed);

                var col = this.GetCollection();
                return col.FindOne(n => n.Seed == seedEntity)?.Export();
            }
        }

        public void Upsert(DownloadingFileItem item)
        {
            lock (_lockObject)
            {
                var itemEntity = DownloadingFileItemEntity.Import(item);

                var col = this.GetCollection();

                _database.BeginTrans();

                col.DeleteMany(n => n.Seed!.RootHash == itemEntity.Seed!.RootHash);
                col.Insert(itemEntity);

                _database.Commit();
            }
        }

        public void Delete(Seed seed)
        {
            lock (_lockObject)
            {
                var seedEntity = SeedEntity.Import(seed);

                var col = this.GetCollection();
                col.DeleteMany(n => n.Seed!.RootHash == seedEntity.RootHash);
            }
        }
    }
}
