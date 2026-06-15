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

/// <summary>
/// LiteDB-backed game store composed of one repository per table. Each repository serializes its own
/// writes (see <see cref="LiteDbRepository{T}"/>) and the database uses a dedicated entity-aware
/// <see cref="BsonMapper"/> so concurrent first-use cannot race the global mapper.
/// </summary>
public sealed class LiteDbGameStore : IGameStore, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly LiteDbRepository<PlayerCharacterRecord> _players;
    private readonly LiteDbRepository<NpcRecord> _npcs;
    private readonly LiteDbRepository<ActionLogRecord> _actionLogs;

    public LiteDbGameStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _database = new LiteDatabase(connectionString, GameStoreMapper.Create());
        _players = new LiteDbRepository<PlayerCharacterRecord>(_database, "players");
        _npcs = new LiteDbRepository<NpcRecord>(_database, "npcs");
        _actionLogs = new LiteDbRepository<ActionLogRecord>(_database, "action_logs");
    }

    public Task UpsertPlayerAsync(PlayerCharacterRecord character, CancellationToken ct = default)
        => _players.UpsertAsync(character, ct);

    public Task<PlayerCharacterRecord?> GetPlayerAsync(string id, CancellationToken ct = default)
        => _players.GetByIdAsync(id, ct);

    public Task UpsertNpcAsync(NpcRecord npc, CancellationToken ct = default)
        => _npcs.UpsertAsync(npc, ct);

    public Task<NpcRecord?> GetNpcAsync(string id, CancellationToken ct = default)
        => _npcs.GetByIdAsync(id, ct);

    public Task InsertActionLogAsync(ActionLogRecord log, CancellationToken ct = default)
        => _actionLogs.InsertAsync(log, ct);

    public Task<IReadOnlyList<ActionLogRecord>> ListActionLogsAsync(CancellationToken ct = default)
        => _actionLogs.GetAllAsync(ct);

    public void Dispose()
    {
        _players.Dispose();
        _npcs.Dispose();
        _actionLogs.Dispose();
        _database.Dispose();
    }
}
