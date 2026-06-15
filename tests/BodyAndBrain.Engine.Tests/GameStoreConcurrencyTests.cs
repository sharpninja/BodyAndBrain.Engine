using BodyAndBrain.Engine;
using LiteDB;

namespace BodyAndBrain.Engine.Tests;

/// <summary>
/// Covers the LiteDB store refactor: a repository per table with serialized writes, and a dedicated
/// per-database BsonMapper so concurrent store instances cannot race the global mapper.
/// </summary>
public sealed class GameStoreConcurrencyTests
{
    private static string TempDb() => Path.Combine(Path.GetTempPath(), $"bab-store-{Guid.NewGuid():n}.db");

    [Fact]
    public async Task ParallelStores_EachUpsertAndRead_DoNotRaceTheMapper()
    {
        // Reproduces the original failure mode: many independent stores (each its own LiteDatabase)
        // upserting and reading concurrently. With BsonMapper.Global this threw
        // "Member Id not found on BsonMapper for type NpcRecord"; a per-database mapper fixes it.
        var paths = new List<string>();
        try
        {
            var tasks = Enumerable.Range(0, 32).Select(i => Task.Run(async () =>
            {
                var path = TempDb();
                lock (paths) { paths.Add(path); }
                using var store = new LiteDbGameStore(path);
                var npc = new NpcRecord { Id = $"npc-{i}", Name = $"Goblin {i}", MaxHits = 20, CurrentHits = 20, IsMonster = true };
                await store.UpsertNpcAsync(npc);
                var read = await store.GetNpcAsync(npc.Id);
                Assert.NotNull(read);
                Assert.Equal(npc.Id, read!.Id);
            }));

            await Task.WhenAll(tasks);
        }
        finally
        {
            foreach (var p in paths)
            {
                TryDelete(p);
            }
        }
    }

    [Fact]
    public async Task Repository_ConcurrentWritesToSameTable_AreSerialized_AndPersist()
    {
        var path = TempDb();
        try
        {
            using var db = new LiteDatabase(path, GameStoreMapper.Create());
            ILiteDbRepository<NpcRecord> repo = new LiteDbRepository<NpcRecord>(db, "npcs");

            // 100 concurrent writers: distinct ids plus repeated overwrites of one shared id.
            var writers = Enumerable.Range(0, 100).Select(i => repo.UpsertAsync(new NpcRecord
            {
                Id = i % 5 == 0 ? "shared" : $"npc-{i}",
                Name = $"Mob {i}",
                MaxHits = 10,
                CurrentHits = 10,
            }));

            await Task.WhenAll(writers); // must not throw despite concurrent writes to one table

            var all = await repo.GetAllAsync();
            Assert.Contains(all, n => n.Id == "shared");
            Assert.Equal(80, all.Count(n => n.Id.StartsWith("npc-", StringComparison.Ordinal)));
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task Store_RoundTrips_PlayerAndNpc()
    {
        var path = TempDb();
        try
        {
            using var store = new LiteDbGameStore(path);

            await store.UpsertPlayerAsync(new PlayerCharacterRecord { Id = "pc-1", Name = "Reynauld", MaxHits = 45, CurrentHits = 45 });
            await store.UpsertNpcAsync(new NpcRecord { Id = "npc-1", Name = "Skeleton", MaxHits = 40, CurrentHits = 40, IsMonster = true });
            await store.InsertActionLogAsync(new ActionLogRecord { Id = "log-1", ActionId = "physical-attack-1h-edge", ActorId = "pc-1" });

            Assert.Equal("Reynauld", (await store.GetPlayerAsync("pc-1"))!.Name);
            Assert.Equal("Skeleton", (await store.GetNpcAsync("npc-1"))!.Name);
            Assert.Single(await store.ListActionLogsAsync());
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort temp cleanup
        }
    }
}
