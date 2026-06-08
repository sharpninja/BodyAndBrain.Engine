using BodyAndBrain.Engine;
using Microsoft.Extensions.DependencyInjection;
using SharpNinja.FeatureFlags.Cqrs;
using YamlDotNet.Serialization;

namespace BodyAndBrain.Engine.Tests;

/// <summary>
/// Dispatch-level requirement coverage for the BodyAndBrain.Engine MCP requirements set.
/// </summary>
/// <remarks>
/// Requirement coverage matrix:
/// FR-ENGINE-001, TR-ENGINE-DI-001, TR-ENGINE-NET-001, TEST-CQRS-001 are covered by CQRS query/command tests.
/// FR-DATA-001, TR-DATA-YAML-001, TEST-COVERAGE-001 are covered by catalog load, validation, and inventory tests.
/// FR-CHARACTER-001, TR-DATA-LITEDB-001, TEST-PERSISTENCE-001 are covered by PC/NPC LiteDB round-trip and action mutation tests.
/// FR-NPC-001 and TEST-NPC-001 are covered by NPC markdown and all valid race/profession boundary-level generation tests.
/// FR-ACTIONS-001, FR-ADJUDICATION-001, TEST-ACTION-001, and TEST-CQRS-001 are covered by action execution and adjudication tests.
/// FR-MONSTERS-001 is covered by monster inventory and exceptional-overdrive assertions.
/// FR-CI-001, TR-CI-AZURE-001, and TEST-CI-001 are covered by the Azure pipeline validation test.
/// </remarks>
public sealed class EngineTests
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder().Build();

    /// <summary>
    /// Verifies FR-DATA-001, TR-DATA-YAML-001, TEST-COVERAGE-001, and TEST-CQRS-001.
    /// </summary>
    /// <remarks>
    /// The catalog must load through <see cref="IDispatcher.QueryAsync{TResult}(IQuery{TResult}, CancellationToken)"/>,
    /// include canonical workbook/manual entities, and pass catalog validation diagnostics.
    /// </remarks>
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

    /// <summary>
    /// Verifies FR-ENGINE-001, FR-CHARACTER-001, TR-DATA-LITEDB-001, TEST-CQRS-001, and TEST-PERSISTENCE-001.
    /// </summary>
    /// <remarks>
    /// Player character creation, level-up mutation, and load queries must dispatch through CQRS and persist structured LiteDB state.
    /// </remarks>
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

    /// <summary>
    /// Verifies FR-CHARACTER-001, FR-NPC-001, TR-ENGINE-DI-001, TEST-CQRS-001, and TEST-NPC-001.
    /// </summary>
    /// <remarks>
    /// Persisted NPC generation and markdown sheet rendering must be available through CQRS commands and queries.
    /// </remarks>
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

    /// <summary>
    /// Verifies FR-ACTIONS-001, FR-CHARACTER-001, TR-DATA-LITEDB-001, TEST-ACTION-001, and TEST-PERSISTENCE-001.
    /// </summary>
    /// <remarks>
    /// A deterministic physical attack must emit the required YAML result and mutate target hit state through the command handler.
    /// </remarks>
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

    /// <summary>
    /// Verifies FR-ADJUDICATION-001, FR-ACTIONS-001, TR-DATA-YAML-001, TEST-ACTION-001, and TEST-CQRS-001.
    /// </summary>
    /// <remarks>
    /// Underspecified spell mechanics must still dispatch successfully and return YAML with a stable adjudication reason.
    /// </remarks>
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

    /// <summary>
    /// Verifies FR-ACTIONS-001, FR-ENGINE-001, TR-ENGINE-DI-001, TEST-ACTION-001, and TEST-CQRS-001.
    /// </summary>
    /// <remarks>
    /// Every catalog action must dispatch through <see cref="IDispatcher.SendAsync{TResult}(ICommand{TResult}, CancellationToken)"/>
    /// and emit the required YAML action-result shape.
    /// </remarks>
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

    /// <summary>
    /// Verifies FR-NPC-001, FR-CHARACTER-001, TEST-NPC-001, and TEST-COVERAGE-001.
    /// </summary>
    /// <remarks>
    /// Every valid race/profession combination must generate at representative boundary levels from the 1-50 NPC scale.
    /// </remarks>
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

    /// <summary>
    /// Verifies FR-DATA-001, FR-MONSTERS-001, FR-ACTIONS-001, TR-DATA-YAML-001, TEST-COVERAGE-001, and TEST-NPC-001.
    /// </summary>
    /// <remarks>
    /// The embedded catalog must provide test-enforced inventory coverage for canonical races, professions, apprenticeships,
    /// spells, weapons, armor, items, maneuvers, NPC baselines, monsters, exceptional overdrive, and action references.
    /// </remarks>
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

    /// <summary>
    /// Verifies FR-CI-001, TR-CI-AZURE-001, and TEST-CI-001.
    /// </summary>
    /// <remarks>
    /// The Azure DevOps pipeline must use the self-hosted Default pool and execute the solution-level test gate.
    /// </remarks>
    [Fact]
    public void AzurePipelineUsesDefaultPoolAndSolutionTestGate()
    {
        var pipelinePath = Path.Combine(FindRepositoryRoot(), "azure-pipelines.yml");
        var yaml = File.ReadAllText(pipelinePath);
        var pipeline = ParseYaml(yaml);

        var pool = Assert.IsType<Dictionary<object, object?>>(pipeline["pool"]);
        Assert.Equal("Default", pool["name"]);

        var steps = Assert.IsAssignableFrom<IList<object>>(pipeline["steps"]);
        var testStep = steps
            .OfType<Dictionary<object, object?>>()
            .SingleOrDefault(step => step.TryGetValue("displayName", out var displayName)
                                     && string.Equals(displayName?.ToString(), "Test", StringComparison.Ordinal));

        Assert.NotNull(testStep);
        Assert.Equal("dotnet test BodyAndBrain.Engine.slnx", testStep!["pwsh"]);
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

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BodyAndBrain.Engine.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root containing BodyAndBrain.Engine.slnx was not found.");
    }
}
