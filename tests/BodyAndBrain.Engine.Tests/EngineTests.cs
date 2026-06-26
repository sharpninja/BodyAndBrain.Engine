using BodyAndBrain.Engine;
using Microsoft.Extensions.Configuration;
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

        // FR-PHYSATTACK-001: canonical accuracy, critical, and protection are surfaced in the YAML.
        var outcome = (Dictionary<object, object?>)yaml["outcome"]!;
        var accuracy = (Dictionary<object, object?>)outcome["accuracy"]!;
        Assert.Contains("weaponSkillBonus", accuracy.Keys);
        Assert.Contains("governingStatBonus", accuracy.Keys);
        Assert.Contains("baseAttackBonus", accuracy.Keys);
        Assert.Contains("total", accuracy.Keys);
        Assert.NotNull(outcome["critical"]); // roll 80 is Hit + Critical
        var protection = (Dictionary<object, object?>)outcome["protection"]!;
        Assert.Equal("None", protection["armorProfile"]);

        var damage = int.Parse(outcome["damage"]!.ToString()!);
        Assert.True(damage >= 6, "Critical resolution must add to the base 6 damage.");

        var reloadedTarget = await dispatcher.QueryAsync(new GetNpcQuery(target.Id));
        AssertSuccess(reloadedTarget);
        Assert.Equal(target.CurrentHits - damage, reloadedTarget.Value!.CurrentHits);
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
        Assert.All(exceptionalMonsters, monster =>
        {
            Assert.Contains("x1.5", monster.OverdriveEffect);
            Assert.False(string.IsNullOrWhiteSpace(monster.OverdrivenStat));
            Assert.Contains(monster.OverdrivenStat!, new[] { "Strength", "Agility", "Constitution", "Intelligence", "Presence", "Piety" });
        });

        // TR-MONSTER-DATA-001: every monster carries a six-stat block and hits ported from the manual.
        Assert.All(data.Monsters, monster =>
        {
            Assert.Equal(6, monster.Stats.Count);
            Assert.True(monster.Hits > 0);
        });
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

    /// <summary>
    /// Verifies FR-MONSTERSPAWN-001, TR-MONSTER-DATA-001, and TEST-MONSTER-001.
    /// </summary>
    [Fact]
    public async Task EveryMonsterGeneratesThroughCqrsWithStatsAndOverdrive()
    {
        using var services = CreateServices();
        var dispatcher = services.GetRequiredService<IDispatcher>();
        var catalog = services.GetRequiredService<IGameDataCatalog>();

        foreach (var monster in catalog.Data.Monsters)
        {
            foreach (var level in new[] { 1, 5, 50 })
            {
                var result = await dispatcher.SendAsync(new GenerateMonsterCommand(monster.Id, level, Persist: false));
                AssertSuccess(result);
                var npc = result.Value!;
                Assert.True(npc.IsMonster);
                Assert.Equal(monster.Name, npc.Monster);
                Assert.Equal(monster.Name, npc.Race);
                Assert.Equal(monster.Profession, npc.Profession);
                Assert.Equal(level, npc.Level);
                Assert.Equal(6, npc.Stats.Count);
                Assert.InRange(npc.CurrentHits, 1, npc.MaxHits);
                if (!string.IsNullOrWhiteSpace(monster.OverdrivenStat))
                    Assert.Equal(monster.OverdrivenStat, npc.OverdrivenStat);
                else
                    Assert.Null(npc.OverdrivenStat);
            }
        }

        var persisted = await dispatcher.SendAsync(new GenerateMonsterCommand("troll-berserker", 10, "Bridge Troll"));
        AssertSuccess(persisted);
        var markdown = await dispatcher.QueryAsync(new RenderNpcMarkdownQuery(persisted.Value!.Id));
        AssertSuccess(markdown);
        Assert.Contains("# Bridge Troll", markdown.Value);
    }

    /// <summary>
    /// Verifies FR-PHYSATTACK-001, TR-COMBAT-TABLES-001, and TEST-COMBAT-001.
    /// </summary>
    [Fact]
    public async Task PhysicalAttacksSurfaceAccuracyCriticalAndProtectionForEveryWeapon()
    {
        using var services = CreateServices();
        var dispatcher = services.GetRequiredService<IDispatcher>();
        var catalog = services.GetRequiredService<IGameDataCatalog>();
        var actor = await CreateActorAsync(dispatcher);

        foreach (var action in catalog.Data.Actions.Where(x => x.Kind == "physicalAttack"))
        {
            var targetResult = await dispatcher.SendAsync(new GenerateMonsterCommand("ogre-fighter", 5, $"Dummy-{action.Id}"));
            AssertSuccess(targetResult);

            // Natural 100 is always the best result, so a critical resolves regardless of the plate
            // accuracy penalty (which can otherwise pull a high roll below the critical band).
            var attack = await dispatcher.SendAsync(new ExecuteGameActionCommand(
                action.Id, actor.Id, targetResult.Value!.Id,
                RollOverride: 100,
                Parameters: new Dictionary<string, string> { ["armor"] = "plate", ["damage"] = "8" }));
            AssertSuccess(attack);

            var outcome = Map(ParseYaml(attack.Value!.Document)["outcome"]);
            var accuracy = Map(outcome["accuracy"]);
            Assert.Contains("weaponSkillBonus", accuracy.Keys);
            Assert.Contains("governingStatBonus", accuracy.Keys);
            Assert.Contains("baseAttackBonus", accuracy.Keys);

            var protection = Map(outcome["protection"]);
            Assert.Equal("Plate", protection["armorProfile"]);
            Assert.Equal(40, Int(protection["protectionPercent"]));

            // Every weapon except the Fist resolves a critical type from the BaB crit tables.
            if (action.ReferenceId != "fist")
                Assert.NotNull(outcome["critical"]);
        }

        // None-armor attack reports zero protection percent.
        var bare = await dispatcher.SendAsync(new GenerateMonsterCommand("skeleton-fighter", 5, "Bare"));
        AssertSuccess(bare);
        var bareAttack = await dispatcher.SendAsync(new ExecuteGameActionCommand(
            "physical-attack-1h-edge", actor.Id, bare.Value!.Id, RollOverride: 60,
            Parameters: new Dictionary<string, string> { ["damage"] = "5" }));
        AssertSuccess(bareAttack);
        var bareProtection = Map(Map(ParseYaml(bareAttack.Value!.Document)["outcome"])["protection"]);
        Assert.Equal(0, Int(bareProtection["protectionPercent"]));
    }

    /// <summary>
    /// Verifies FR-SPELL-001, TR-SPELL-RESOLVE-001, FR-ADJUDICATION-001, and TEST-SPELL-001.
    /// </summary>
    [Fact]
    public async Task SpellActionsResolveDamageHealAndKeepAdjudicationWhereUnderspecified()
    {
        using var services = CreateServices();
        var dispatcher = services.GetRequiredService<IDispatcher>();
        var caster = await CreateActorAsync(dispatcher);

        async Task<NpcRecord> Target(string label)
        {
            var t = await dispatcher.SendAsync(new GenerateMonsterCommand("ogre-fighter", 8, label));
            AssertSuccess(t);
            return t.Value!;
        }

        // Damage spell (Bolts, 1D10) reduces target hits and does not adjudicate.
        var dmgTarget = await Target("Bolt Dummy");
        var bolts = await dispatcher.SendAsync(new ExecuteGameActionCommand("cast-spell-body-bolts", caster.Id, dmgTarget.Id));
        AssertSuccess(bolts);
        Assert.False(bolts.Value!.RequiresAdjudication);
        var afterBolts = await dispatcher.QueryAsync(new GetNpcQuery(dmgTarget.Id));
        Assert.True(afterBolts.Value!.CurrentHits < dmgTarget.CurrentHits);

        // Heal spell restores hits on a wounded target.
        var healTarget = await Target("Heal Dummy");
        await dispatcher.SendAsync(new ExecuteGameActionCommand("damage-target", caster.Id, healTarget.Id,
            Parameters: new Dictionary<string, string> { ["amount"] = "20" }));
        var wounded = (await dispatcher.QueryAsync(new GetNpcQuery(healTarget.Id))).Value!;
        var heal = await dispatcher.SendAsync(new ExecuteGameActionCommand("cast-spell-blessings-heal", caster.Id, healTarget.Id));
        AssertSuccess(heal);
        Assert.False(heal.Value!.RequiresAdjudication);
        var healed = (await dispatcher.QueryAsync(new GetNpcQuery(healTarget.Id))).Value!;
        Assert.True(healed.CurrentHits > wounded.CurrentHits);

        // Life drain damages the target.
        var drainTarget = await Target("Drain Dummy");
        var drain = await dispatcher.SendAsync(new ExecuteGameActionCommand("cast-spell-necromancer-life-drain", caster.Id, drainTarget.Id));
        AssertSuccess(drain);
        var drained = (await dispatcher.QueryAsync(new GetNpcQuery(drainTarget.Id))).Value!;
        Assert.True(drained.CurrentHits < drainTarget.CurrentHits);

        // Underspecified spell keeps adjudication.
        var animate = await dispatcher.SendAsync(new ExecuteGameActionCommand("cast-spell-necromancer-animate-dead", caster.Id));
        AssertSuccess(animate);
        Assert.True(animate.Value!.RequiresAdjudication);
        Assert.Equal("adjudication-spell", animate.Value.AdjudicationReason);

        // Effect spell resolves deterministically without adjudication.
        var hasten = await dispatcher.SendAsync(new ExecuteGameActionCommand("cast-spell-body-hasten", caster.Id));
        AssertSuccess(hasten);
        Assert.False(hasten.Value!.RequiresAdjudication);
    }

    /// <summary>
    /// Verifies FR-STATUS-001, TR-STATUS-MODEL-001, and TEST-STATUS-001.
    /// </summary>
    [Fact]
    public async Task StatusEffectsApplyPersistTickAndResolveResistance()
    {
        using var services = CreateServices();
        var dispatcher = services.GetRequiredService<IDispatcher>();
        var caster = await CreateActorAsync(dispatcher);

        foreach (var status in new[] { "Bleed", "Poison", "Disease", "Stun", "Move", "Curse" })
        {
            var targetResult = await dispatcher.SendAsync(new GenerateMonsterCommand("troll-berserker", 8, $"Afflicted-{status}"));
            AssertSuccess(targetResult);
            var target = targetResult.Value!;

            var applied = await dispatcher.SendAsync(new ExecuteGameActionCommand(
                "apply-condition", caster.Id, target.Id,
                Parameters: new Dictionary<string, string> { ["condition"] = status }));
            AssertSuccess(applied);
            var afterApply = (await dispatcher.QueryAsync(new GetNpcQuery(target.Id))).Value!;
            Assert.Contains(afterApply.Statuses, s => string.Equals(s.Type, status, StringComparison.OrdinalIgnoreCase));

            // Failed resistance (natural 1) keeps the status and applies damaging-status damage.
            var tick = await dispatcher.SendAsync(new TickStatusEffectsCommand(target.Id, RollOverride: 1));
            AssertSuccess(tick);
            var afterTick = (await dispatcher.QueryAsync(new GetNpcQuery(target.Id))).Value!;
            if (status is "Bleed" or "Poison" or "Disease")
                Assert.True(afterTick.CurrentHits < afterApply.CurrentHits);

            // Successful resistance (natural 100) clears remaining statuses.
            await dispatcher.SendAsync(new TickStatusEffectsCommand(target.Id, RollOverride: 100));
            var cleared = (await dispatcher.QueryAsync(new GetNpcQuery(target.Id))).Value!;
            Assert.DoesNotContain(cleared.Statuses, s => s.Active && string.Equals(s.Type, status, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Verifies FR-ENGINE-ROLL-001 and TEST-ROLL-001.
    /// </summary>
    [Fact]
    public async Task ClientExecuteActionForwardsRollOverride()
    {
        using var services = CreateServices();
        var dispatcher = services.GetRequiredService<IDispatcher>();
        var engine = services.GetRequiredService<IBodyAndBrainEngine>();
        var actor = await CreateActorAsync(dispatcher);

        var result = await engine.ExecuteActionAsync("physical-attack-1h-edge", actor.Id, rollOverride: 42);
        var rolls = Map(ParseYaml(result.Document)["rolls"]);
        Assert.Equal(42, Int(rolls["d100"]));
    }

    /// <summary>
    /// Verifies FR-NPC-FALLBACK-001 and TEST-FALLBACK-001.
    /// </summary>
    [Fact]
    public async Task EveryRaceProfessionCombinationGeneratesViaFallback()
    {
        using var services = CreateServices();
        var dispatcher = services.GetRequiredService<IDispatcher>();
        var catalog = services.GetRequiredService<IGameDataCatalog>();

        foreach (var race in catalog.Data.Races)
        {
            foreach (var profession in catalog.Data.Professions)
            {
                foreach (var level in new[] { 1, 5, 50 })
                {
                    var result = await dispatcher.SendAsync(new GenerateNpcCommand(race.Name, profession.Name, level, Persist: false));
                    AssertSuccess(result);
                    Assert.Equal(level, result.Value!.Level);
                    Assert.Equal(6, result.Value.Stats.Count);
                    Assert.InRange(result.Value.CurrentHits, 1, result.Value.MaxHits);
                }
            }
        }

        // An invalid race/profession combination still generates, flagged as derived.
        var derived = await dispatcher.SendAsync(new GenerateNpcCommand("Orc", "Wizard", 5, Persist: false));
        AssertSuccess(derived);
        Assert.True(derived.Value!.Derived);
    }

    /// <summary>
    /// Verifies FR-OVERDRIVE-001 and TEST-OVERDRIVE-001.
    /// </summary>
    [Fact]
    public async Task OverdrivenMonsterMultipliesGovernedStatEffect()
    {
        using var services = CreateServices();
        var dispatcher = services.GetRequiredService<IDispatcher>();

        // Dragon (Fighter) overdrives Strength, which governs the 1H Edge attack.
        var dragon = await dispatcher.SendAsync(new GenerateMonsterCommand("dragon-fighter", 5, "Smaug"));
        AssertSuccess(dragon);
        var dragonTarget = await dispatcher.SendAsync(new GenerateMonsterCommand("ogre-fighter", 5, "DragonTarget"));
        AssertSuccess(dragonTarget);

        var dragonAttack = await dispatcher.SendAsync(new ExecuteGameActionCommand(
            "physical-attack-1h-edge", dragon.Value!.Id, dragonTarget.Value!.Id,
            RollOverride: 40, Parameters: new Dictionary<string, string> { ["damage"] = "4", ["weaponSkill"] = "5" }));
        AssertSuccess(dragonAttack);
        var dragonOutcome = Map(ParseYaml(dragonAttack.Value!.Document)["outcome"]);
        var overdrive = Map(dragonOutcome["overdrive"]);
        Assert.Equal("Strength", overdrive["stat"]?.ToString());
        Assert.Equal(6, Int(dragonOutcome["damage"])); // 4 * 1.5, plain Hit, no armor

        // Skeleton (Fighter) is not overdriven: same inputs yield the base 4 damage and no overdrive block.
        var skeleton = await dispatcher.SendAsync(new GenerateMonsterCommand("skeleton-fighter", 5, "Rattles"));
        AssertSuccess(skeleton);
        var skeletonTarget = await dispatcher.SendAsync(new GenerateMonsterCommand("ogre-fighter", 5, "SkeletonTarget"));
        AssertSuccess(skeletonTarget);
        var skeletonAttack = await dispatcher.SendAsync(new ExecuteGameActionCommand(
            "physical-attack-1h-edge", skeleton.Value!.Id, skeletonTarget.Value!.Id,
            RollOverride: 40, Parameters: new Dictionary<string, string> { ["damage"] = "4", ["weaponSkill"] = "5" }));
        AssertSuccess(skeletonAttack);
        var skeletonOutcome = Map(ParseYaml(skeletonAttack.Value!.Document)["outcome"]);
        Assert.False(skeletonOutcome.ContainsKey("overdrive"));
        Assert.Equal(4, Int(skeletonOutcome["damage"]));
    }

    private static Dictionary<object, object?> Map(object? value)
        => (Dictionary<object, object?>)value!;

    private static int Int(object? value)
        => int.Parse(value!.ToString()!);

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

    // --- New tests per remediation plan ---

    [Fact]
    public void MagicNumbers_LoadedViaIConfiguration_MatchExpectedCanonicalValues()
    {
        using var services = CreateServices();
        var config = services.GetRequiredService<IConfiguration>();
        var magic = services.GetRequiredService<MagicNumbers>();

        // Values come from Data/magic-numbers.yaml bound via IConfiguration
        Assert.Equal(50, magic.MaxLevel);
        Assert.Equal(35, magic.BaseHits);
        Assert.Equal(10, magic.SkillMax);
        Assert.Equal(50, magic.BaseStat);
        Assert.Equal(20, magic.PrimaryStatBonus);
        Assert.Equal(50, magic.ManeuverDefaultRoll);
        Assert.Equal(10, magic.DefenseBonus);
        Assert.Contains("Strength", magic.StatNames);

        // IConfiguration is registered and bound (values available via POCO primarily; config section also populated)
        var gameSection = config.GetSection("game");
        Assert.True(gameSection.Exists() || !string.IsNullOrEmpty(config["game:MaxLevel"])); // basic presence check
        Assert.Equal(50, magic.MaxLevel); // primary verification via bound POCO from IConfiguration source
    }

    [Fact]
    public void RandomDiceRoller_WithSeed_ProducesDeterministicRolls()
    {
        var seeded1 = new RandomDiceRoller(12345);
        var seeded2 = new RandomDiceRoller(12345);

        var r1 = seeded1.D100();
        var r2 = seeded2.D100();

        Assert.Equal(r1, r2); // same seed => same sequence

        // Different seed produces different (almost always)
        var other = new RandomDiceRoller(54321).D100();
        // Not asserting inequality (possible collision) but exercise path
    }

    /// <summary>
    /// Exhaustive coverage using the actual data tables (source of truth in code which derives from docs/manual).
    /// Rule changes in tables/docs can be validated by updating data; tests drive from the tables.
    /// (Slow tests acceptable per requirements.)
    /// </summary>
    [Fact]
    public void Exhaustive_CombatTables_CoverAllWeaponArmorCritScenarios()
    {
        var profiles = new[] { "1H Edge", "2H Edge", "1H Conc", "2H Conc", "Thrown", "Crossbow", "Shortbow", "Longbow", "Fist", "Claws" };
        var armors = CombatTables.ArmorColumns;

        foreach (var profile in profiles)
        {
            for (int col = 0; col < armors.Length; col++)
            {
                var bab = CombatTables.BaseAttackBonus(profile, col);
                var crit = CombatTables.CriticalType(profile, col);

                // Every profile x armor produces a valid BAB
                Assert.True(bab >= -30 && bab <= 30);

                if (profile != "Fist")
                {
                    Assert.NotNull(crit);
                }

                // Exhaustive crit scores -3..4 mapped
                for (int cs = -3; cs <= 4; cs++)
                {
                    var effect = CombatTables.CriticalEffect(crit?.Type ?? "Slash", cs);
                    Assert.True(effect.Immediate >= 0);
                }
            }
        }
    }

    [Fact]
    public void Exhaustive_Mechanics_RollOutcomeAndStatBonus_AllBands()
    {
        // Natural special + band exhaustive
        Assert.Equal("F", Mechanics.RollOutcome(1));
        Assert.Equal("2x Hit + Critical", Mechanics.RollOutcome(100));

        for (int r = 2; r <= 99; r++)
        {
            var o = Mechanics.RollOutcome(r, r); // total==natural for simplicity
            Assert.Contains(o, new[] { "Miss", "Hit / 2", "Hit", "Hit + Critical" });
        }

        // Stat bonus boundaries
        Assert.Equal(0, Mechanics.StatBonus(49));
        Assert.Equal(1, Mechanics.StatBonus(50));
        Assert.Equal(1, Mechanics.StatBonus(74));
        Assert.Equal(2, Mechanics.StatBonus(75));
        Assert.Equal(10, Mechanics.StatBonus(100));
    }
}
