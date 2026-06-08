using BodyAndBrain.Engine;
using Microsoft.Extensions.DependencyInjection;
using SharpNinja.FeatureFlags.Cqrs;
using YamlDotNet.Serialization;

namespace BodyAndBrain.Engine.Tests;

public sealed class EngineTests
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder().Build();

    [Fact]
    public async Task CatalogLoadsAndValidatesThroughCqrsQuery()
    {
        using var services = CreateServices();
        var dispatcher = services.GetRequiredService<IDispatcher>();

        var dataResult = await dispatcher.QueryAsync(new GetGameDataQuery());
        AssertSuccess(dataResult);
        var data = dataResult.Value!;

        Assert.Equal(8, data.Races.Count);
        Assert.Equal(9, data.Professions.Count);
        Assert.True(data.Apprenticeships.Count >= 19);
        Assert.True(data.Spells.Count >= 70);
        Assert.True(data.Actions.Count >= 140);
        Assert.Contains(data.SpellLists, x => x.Name == "Necromancer");
        Assert.Contains(data.Weapons, x => x.Name == "Claws");

        var validationResult = await dispatcher.QueryAsync(new ValidateGameDataQuery());
        AssertSuccess(validationResult);
        Assert.True(validationResult.Value!.IsValid, string.Join(Environment.NewLine, validationResult.Value.Diagnostics));
    }

    [Fact]
    public async Task CreateLevelAndLoadPlayerCharacterThroughCqrs()
    {
        using var services = CreateServices();
        var dispatcher = services.GetRequiredService<IDispatcher>();

        var createdResult = await dispatcher.SendAsync(new CreatePlayerCharacterCommand("Mira", "Human", "Fighter", "Soldier"));
        AssertSuccess(createdResult);
        var created = createdResult.Value!;

        var leveledResult = await dispatcher.SendAsync(new ApplyLevelUpCommand(
            created.Id,
            new Dictionary<string, int> { ["Strength"] = 2 },
            new Dictionary<string, int> { ["Primary Melee"] = 1 }));
        AssertSuccess(leveledResult);

        var loadedResult = await dispatcher.QueryAsync(new GetPlayerCharacterQuery(created.Id));
        AssertSuccess(loadedResult);
        Assert.NotNull(loadedResult.Value);
        Assert.Equal(2, loadedResult.Value!.Level);
        Assert.Equal(54, loadedResult.Value.Stats["Strength"]);
        Assert.Equal(1, loadedResult.Value.Skills["Primary Melee"]);
    }

    [Fact]
    public async Task GenerateNpcAndRenderMarkdownThroughCqrs()
    {
        using var services = CreateServices();
        var dispatcher = services.GetRequiredService<IDispatcher>();

        var npcResult = await dispatcher.SendAsync(new GenerateNpcCommand("Orc", "Fighter", 5, "Orc Warrior"));
        AssertSuccess(npcResult);

        var markdownResult = await dispatcher.QueryAsync(new RenderNpcMarkdownQuery(npcResult.Value!.Id));
        AssertSuccess(markdownResult);
        Assert.Contains("# Orc Warrior", markdownResult.Value);
        Assert.Contains("*Race:* Orc", markdownResult.Value);
        Assert.Contains("| Stat | Score | Bonus |", markdownResult.Value);
    }

    [Fact]
    public async Task PhysicalAttackReturnsYamlAndMutatesTargetHits()
    {
        using var services = CreateServices();
        var dispatcher = services.GetRequiredService<IDispatcher>();

        var actor = await CreateActorAsync(dispatcher);
        var targetResult = await dispatcher.SendAsync(new GenerateNpcCommand("Human", "Rogue", 5, "Practice Target"));
        AssertSuccess(targetResult);
        var target = targetResult.Value!;

        var attackResult = await dispatcher.SendAsync(new ExecuteGameActionCommand(
            "physical-attack-1h-edge",
            actor.Id,
            target.Id,
            RollOverride: 80,
            Parameters: new Dictionary<string, string> { ["damage"] = "6" }));
        AssertSuccess(attackResult);

        var yaml = ParseYaml(attackResult.Value!.Document);
        Assert.Equal("physical-attack-1h-edge", yaml["actionId"]);
        Assert.False(AsBoolean(yaml["requiresAdjudication"]));
        Assert.True(((IList<object>)yaml["stateChanges"]!).Count > 0);

        var reloadedTarget = await dispatcher.QueryAsync(new GetNpcQuery(target.Id));
        AssertSuccess(reloadedTarget);
        Assert.Equal(target.CurrentHits - 6, reloadedTarget.Value!.CurrentHits);
    }

    [Fact]
    public async Task SpellWithoutDeterministicRuleReturnsAdjudicationYaml()
    {
        using var services = CreateServices();
        var dispatcher = services.GetRequiredService<IDispatcher>();
        var actor = await CreateActorAsync(dispatcher);

        var result = await dispatcher.SendAsync(new ExecuteGameActionCommand("cast-spell-necromancer-animate-dead", actor.Id));
        AssertSuccess(result);

        var yaml = ParseYaml(result.Value!.Document);
        Assert.True(AsBoolean(yaml["requiresAdjudication"]));
        Assert.Equal("adjudication-spell", yaml["adjudicationReason"]);
        Assert.Contains("GM adjudication", ((Dictionary<object, object?>)yaml["outcome"]!)["result"]!.ToString());
    }

    [Fact]
    public async Task EveryCatalogActionDispatchesThroughCqrsAndReturnsRequiredYamlShape()
    {
        using var services = CreateServices();
        var dispatcher = services.GetRequiredService<IDispatcher>();
        var catalog = services.GetRequiredService<IGameDataCatalog>();
        var actor = await CreateActorAsync(dispatcher);
        var parameters = new Dictionary<string, string>
        {
            ["amount"] = "1",
            ["condition"] = "stunned",
            ["damage"] = "1",
        };

        foreach (var action in catalog.Data.Actions)
        {
            var result = await dispatcher.SendAsync(new ExecuteGameActionCommand(action.Id, actor.Id, RollOverride: 75, Parameters: parameters));
            AssertSuccess(result);
            AssertRequiredYamlShape(ParseYaml(result.Value!.Document));
        }
    }

    [Fact]
    public async Task EveryValidNpcCombinationGeneratesAtBoundaryLevelsThroughCqrs()
    {
        using var services = CreateServices();
        var dispatcher = services.GetRequiredService<IDispatcher>();
        var catalog = services.GetRequiredService<IGameDataCatalog>();

        foreach (var race in catalog.Data.Races)
        {
            foreach (var profession in race.ValidProfessions)
            {
                foreach (var level in new[] { 1, 5, 50 })
                {
                    var result = await dispatcher.SendAsync(new GenerateNpcCommand(race.Name, profession, level, Persist: false));
                    AssertSuccess(result);
                    Assert.Equal(level, result.Value!.Level);
                    Assert.Equal(race.Name, result.Value.Race);
                    Assert.Equal(profession, result.Value.Profession);
                    Assert.InRange(result.Value.CurrentHits, 1, result.Value.MaxHits);
                }
            }
        }
    }

    [Fact]
    public void InventoryCoverageIncludesDefinitionsRequiredByPlan()
    {
        var catalog = GameDataCatalog.LoadEmbedded();
        var data = catalog.Data;

        Assert.All(data.Races, race =>
        {
            Assert.False(string.IsNullOrWhiteSpace(race.Id));
            Assert.NotEmpty(race.ValidProfessions);
            Assert.NotEmpty(race.StatBonuses);
        });
        Assert.All(data.Professions, profession =>
        {
            Assert.False(string.IsNullOrWhiteSpace(profession.PrimaryStat));
            Assert.Contains(data.NpcBaselines, baseline => baseline.Profession == profession.Name);
        });
        Assert.All(data.Apprenticeships, apprenticeship =>
            Assert.Contains(data.Professions, profession => profession.Name == apprenticeship.Profession));
        Assert.All(data.Spells, spell =>
            Assert.Contains(data.Actions, action => action.Kind == "spell" && action.ReferenceId == spell.Id));
        Assert.All(data.Weapons, weapon =>
            Assert.Contains(data.Actions, action => action.Kind == "physicalAttack" && action.ReferenceId == weapon.Id));
        Assert.All(data.Armors, armor =>
            Assert.Contains(data.Actions, action => action.Kind == "equipArmor" && action.ReferenceId == armor.Id));
        Assert.All(data.Items, item =>
            Assert.Contains(data.Actions, action => action.Kind == "item" && action.ReferenceId == item.Id));
        Assert.All(data.Maneuvers, maneuver =>
            Assert.Contains(data.Actions, action => action.Kind == "maneuver" && action.ReferenceId == maneuver.Id));
        Assert.All(data.NpcBaselines, baseline =>
        {
            Assert.NotEmpty(baseline.Stats);
            Assert.True(baseline.Hits > 0);
        });
        Assert.All(data.Monsters, monster =>
        {
            Assert.Contains(data.Professions, profession => profession.Name == monster.Profession);
            Assert.False(string.IsNullOrWhiteSpace(monster.Signature));
        });

        var exceptionalNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Lich",
            "Demon",
            "Dragon",
            "Vampire",
        };
        var exceptionalMonsters = data.Monsters.Where(x => exceptionalNames.Contains(x.Name)).ToList();
        Assert.Equal(8, exceptionalMonsters.Count);
        Assert.All(exceptionalMonsters, monster => Assert.Contains("x1.5", monster.OverdrivenStat));
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        var storePath = Path.Combine(Path.GetTempPath(), $"bodyandbrain-engine-{Guid.NewGuid():n}.db");
        services.AddBodyAndBrainEngine(options => options.StorePath = storePath);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task<PlayerCharacterRecord> CreateActorAsync(IDispatcher dispatcher)
    {
        var actorResult = await dispatcher.SendAsync(new CreatePlayerCharacterCommand("Actor", "Human", "Fighter", "Soldier"));
        AssertSuccess(actorResult);
        return actorResult.Value!;
    }

    private static Dictionary<object, object?> ParseYaml(string yaml)
    {
        var parsed = YamlDeserializer.Deserialize<Dictionary<object, object?>>(yaml);
        Assert.NotNull(parsed);
        return parsed;
    }

    private static void AssertRequiredYamlShape(Dictionary<object, object?> yaml)
    {
        Assert.Contains("actionId", yaml.Keys);
        Assert.Contains("actor", yaml.Keys);
        Assert.Contains("target", yaml.Keys);
        Assert.Contains("inputs", yaml.Keys);
        Assert.Contains("rolls", yaml.Keys);
        Assert.Contains("modifiers", yaml.Keys);
        Assert.Contains("outcome", yaml.Keys);
        Assert.Contains("stateChanges", yaml.Keys);
        Assert.Contains("diagnostics", yaml.Keys);
        Assert.Contains("requiresAdjudication", yaml.Keys);
    }

    private static void AssertSuccess<T>(Result<T> result)
        => Assert.True(result.IsSuccess, result.Error ?? result.Exception?.ToString());

    private static bool AsBoolean(object? value)
        => value switch
        {
            bool boolean => boolean,
            string text => bool.Parse(text),
            _ => throw new InvalidOperationException($"Value '{value}' is not a boolean."),
        };
}
