using LiteDB;

namespace BodyAndBrain.Engine;

public interface IGameStore
{
    Task UpsertPlayerAsync(PlayerCharacterRecord character, CancellationToken ct = default);
    Task<PlayerCharacterRecord?> GetPlayerAsync(string id, CancellationToken ct = default);
    Task UpsertNpcAsync(NpcRecord npc, CancellationToken ct = default);
    Task<NpcRecord?> GetNpcAsync(string id, CancellationToken ct = default);
    Task InsertActionLogAsync(ActionLogRecord log, CancellationToken ct = default);
    Task<IReadOnlyList<ActionLogRecord>> ListActionLogsAsync(CancellationToken ct = default);
}

public sealed class LiteDbGameStore : IGameStore, IDisposable
{
    private readonly LiteDatabase _database;

    public LiteDbGameStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _database = new LiteDatabase(connectionString);
        _database.GetCollection<PlayerCharacterRecord>("players").EnsureIndex(x => x.Id, unique: true);
        _database.GetCollection<NpcRecord>("npcs").EnsureIndex(x => x.Id, unique: true);
        _database.GetCollection<ActionLogRecord>("action_logs").EnsureIndex(x => x.Id, unique: true);
    }

    public Task UpsertPlayerAsync(PlayerCharacterRecord character, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _database.GetCollection<PlayerCharacterRecord>("players").Upsert(character);
        return Task.CompletedTask;
    }

    public Task<PlayerCharacterRecord?> GetPlayerAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        PlayerCharacterRecord? record = _database.GetCollection<PlayerCharacterRecord>("players").FindById(id);
        return Task.FromResult<PlayerCharacterRecord?>(record);
    }

    public Task UpsertNpcAsync(NpcRecord npc, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _database.GetCollection<NpcRecord>("npcs").Upsert(npc);
        return Task.CompletedTask;
    }

    public Task<NpcRecord?> GetNpcAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        NpcRecord? record = _database.GetCollection<NpcRecord>("npcs").FindById(id);
        return Task.FromResult<NpcRecord?>(record);
    }

    public Task InsertActionLogAsync(ActionLogRecord log, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _database.GetCollection<ActionLogRecord>("action_logs").Insert(log);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ActionLogRecord>> ListActionLogsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ActionLogRecord>>(_database.GetCollection<ActionLogRecord>("action_logs").FindAll().ToList());
    }

    public void Dispose() => _database.Dispose();
}
