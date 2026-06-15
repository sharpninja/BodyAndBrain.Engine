using LiteDB;

namespace BodyAndBrain.Engine;

/// <summary>
/// A repository over a single LiteDB collection (table). Reads pass through; writes are serialized by
/// a per-collection <see cref="SemaphoreSlim"/> so concurrent writes to the same table cannot race.
/// </summary>
public interface ILiteDbRepository<T>
    where T : class
{
    Task UpsertAsync(T entity, CancellationToken ct = default);
    Task InsertAsync(T entity, CancellationToken ct = default);
    Task<T?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
}

/// <summary>
/// Generic LiteDB-backed repository. Each instance owns one collection and one write gate, so all
/// mutations to that table are serialized (one writer at a time) while reads remain concurrent.
/// </summary>
public sealed class LiteDbRepository<T> : ILiteDbRepository<T>, IDisposable
    where T : class
{
    private readonly ILiteCollection<T> _collection;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public LiteDbRepository(ILiteDatabase database, string collectionName)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);
        _collection = database.GetCollection<T>(collectionName);
    }

    public async Task UpsertAsync(T entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _collection.Upsert(entity);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task InsertAsync(T entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _collection.Insert(entity);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public Task<T?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<T?>(_collection.FindById(id));
    }

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<T>>(_collection.FindAll().ToList());
    }

    public void Dispose() => _writeGate.Dispose();
}

/// <summary>
/// Builds a LiteDB <see cref="BsonMapper"/> with the game entities' ids registered eagerly. Using a
/// dedicated per-database mapper (instead of <see cref="BsonMapper.Global"/>) avoids a concurrency
/// hazard where the lazily-initialized global mapper is read while another database instance is still
/// registering an entity, which surfaced as "Member Id not found on BsonMapper for type ...".
/// </summary>
public static class GameStoreMapper
{
    public static BsonMapper Create()
    {
        var mapper = new BsonMapper();
        mapper.Entity<PlayerCharacterRecord>().Id(x => x.Id);
        mapper.Entity<NpcRecord>().Id(x => x.Id);
        mapper.Entity<ActionLogRecord>().Id(x => x.Id);
        return mapper;
    }
}
