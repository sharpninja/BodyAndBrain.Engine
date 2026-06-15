# API Reference

The complete public surface of `BodyAndBrain.Engine`. Namespace is
`BodyAndBrain.Engine` throughout. CQRS types come from
`SharpNinja.FeatureFlags.Cqrs`.

## Registration

### `ServiceCollectionExtensions.AddBodyAndBrainEngine`

```csharp
IServiceCollection AddBodyAndBrainEngine(
    this IServiceCollection services,
    Action<BodyAndBrainEngineOptions>? configure = null)
```

Registers the catalog, the LiteDB store, the dice roller, the NPC and monster
generators, the markdown renderer, the action executor, the `IBodyAndBrainEngine`
client, and the CQRS handlers. Annotated `[RequiresUnreferencedCode]` (reflection
handler scan).

### `BodyAndBrainEngineOptions`

| Member | Type | Default | Meaning |
| --- | --- | --- | --- |
| `StorePath` | `string` | `bodyandbrain.engine.db` next to the assembly | LiteDB file path |

## The typed client: `IBodyAndBrainEngine`

A convenience wrapper over the dispatcher. Each method throws
`InvalidOperationException` on failure (with the underlying error and exception).

```csharp
Task<PlayerCharacterRecord> CreatePlayerCharacterAsync(
    string name, string race, string profession, string apprenticeship,
    CancellationToken ct = default);

Task<PlayerCharacterRecord> ApplyLevelUpAsync(
    string characterId,
    Dictionary<string,int>? statIncrements = null,
    Dictionary<string,int>? skillIncrements = null,
    CancellationToken ct = default);

Task<NpcRecord> GenerateNpcAsync(
    string race, string profession, int level,
    string? name = null, CancellationToken ct = default);

Task<NpcRecord> GenerateMonsterAsync(
    string monsterId, int level,
    string? name = null, CancellationToken ct = default);

Task<YamlActionResult> ExecuteActionAsync(
    string actionId, string actorId, string? targetId = null,
    int? rollOverride = null,
    Dictionary<string,string>? parameters = null,
    CancellationToken ct = default);

Task<YamlActionResult> TickStatusEffectsAsync(
    string targetId, int? rollOverride = null,
    CancellationToken ct = default);
```

The client does not expose the read queries (get character, get NPC, render
sheet, enumerate actions, get/validate data). Use the dispatcher for those.

## Commands

Dispatch with `dispatcher.SendAsync(command)`; each returns `Result<T>`.

| Command | Returns | Notes |
| --- | --- | --- |
| `CreatePlayerCharacterCommand(Name, Race, Profession, Apprenticeship)` | `PlayerCharacterRecord` | Fails on illegal race/profession or apprenticeship |
| `ApplyLevelUpCommand(CharacterId, StatIncrements?, SkillIncrements?)` | `PlayerCharacterRecord` | Fails past level 50 or if not found |
| `GenerateNpcCommand(Race, Profession, Level, Name?, Persist = true)` | `NpcRecord` | Level 1 to 50; any combo generates (derived fallback) |
| `GenerateMonsterCommand(MonsterId, Level, Name?, Persist = true)` | `NpcRecord` | Fails on unknown monster id |
| `ExecuteGameActionCommand(ActionId, ActorId, TargetId?, RollOverride?, Parameters?)` | `YamlActionResult` | Fails on unknown action/actor/target |
| `TickStatusEffectsCommand(TargetId, RollOverride?)` | `YamlActionResult` | Advances all active statuses one round |

`Persist = false` on the generate commands returns the record without writing it
to the store, useful for previews and tests.

## Queries

Dispatch with `dispatcher.QueryAsync(query)`; each returns `Result<T>`.

| Query | Returns | Notes |
| --- | --- | --- |
| `GetGameDataQuery()` | `GameData` | The whole catalog |
| `ValidateGameDataQuery()` | `GameDataValidationResult` | Integrity diagnostics |
| `EnumerateActionsQuery()` | `IReadOnlyList<ActionDefinition>` | All 142 actions |
| `GetPlayerCharacterQuery(CharacterId)` | `PlayerCharacterRecord?` | Null if not found |
| `GetNpcQuery(NpcId)` | `NpcRecord?` | Null if not found |
| `RenderNpcMarkdownQuery(NpcId)` | `string` | Fails if the NPC is not stored |

## Records

### `PlayerCharacterRecord`

| Field | Type | Meaning |
| --- | --- | --- |
| `Id` | `string` | GUID; actor/target id |
| `Name` | `string` | Display name |
| `Race`, `Profession`, `Apprenticeship` | `string` | Identity |
| `Level` | `int` | 1 to 50 |
| `Stats` | `Dictionary<string,int>` | The six stat scores |
| `Skills` | `Dictionary<string,int>` | Named skills, 0 to 10 |
| `MaxHits`, `CurrentHits` | `int` | Health |
| `LevelUpHistory` | `List<string>` | One line per level gained |
| `Statuses` | `List<StatusEffectState>` | Active status effects |

### `NpcRecord`

| Field | Type | Meaning |
| --- | --- | --- |
| `Id` | `string` | GUID; actor/target id |
| `Name` | `string` | Display name |
| `Race`, `Profession` | `string` | Identity (creature name for monsters) |
| `Level` | `int` | 1 to 50 |
| `Array` | `string` | Baseline array name, or `derived` / `monster` |
| `Stats` | `Dictionary<string,int>` | The six stat scores |
| `MaxHits`, `CurrentHits` | `int` | Health |
| `Signature` | `string` | Signature skill rating, e.g. `Primary Melee 5` |
| `IsMonster` | `bool` | True for catalog monsters |
| `Monster` | `string?` | Creature name when a monster |
| `OverdrivenStat` | `string?` | Overdriven stat, or null |
| `Derived` | `bool` | True when from a derived fallback baseline |
| `Statuses` | `List<StatusEffectState>` | Active status effects |

### `StatusEffectState`

| Field | Type | Meaning |
| --- | --- | --- |
| `Type` | `string` | Bleed, Poison, Disease, Stun, Move, or Curse |
| `Magnitude` | `int` | Per-round damage (damaging) or penalty magnitude (control) |
| `RoundsRemaining` | `int` | Rounds left |
| `ResistanceStat` | `string` | Stat governing the resistance roll |
| `ResistanceBonus` | `int` | Extra resistance added on tick |
| `Active` | `bool` | False once resisted or expired |

### `YamlActionResult`

| Field | Type | Meaning |
| --- | --- | --- |
| `ActionId` | `string` | The action that ran |
| `Document` | `string` | The YAML result document |
| `RequiresAdjudication` | `bool` | True when the engine deferred the mechanic |
| `AdjudicationReason` | `string?` | Stable reason code when adjudicated |

### `ActionLogRecord`

| Field | Type | Meaning |
| --- | --- | --- |
| `Id` | `string` | Log entry id |
| `ActionId` | `string` | Action that ran |
| `ActorId` | `string` | Actor |
| `TargetId` | `string?` | Target, if any |
| `Timestamp` | `DateTimeOffset` | UTC time of execution |
| `YamlDocument` | `string` | The full result document |

## Catalog types

`GameData` aggregates the catalog. Its definition types are read-only data your
content tooling can enumerate.

| Type | Key fields |
| --- | --- |
| `GameData` | `Source`, `Races`, `Professions`, `Apprenticeships`, `Skills`, `SpellLists`, `Spells`, `Weapons`, `Armors`, `Items`, `Maneuvers`, `NpcBaselines`, `Monsters`, `Actions`, `KnownGaps` |
| `RaceDefinition` | `Id`, `Name`, `StatBonuses`, `Resistances`, `ValidProfessions` |
| `ProfessionDefinition` | `Id`, `Name`, `PrimaryStat`, `ClassBonus` |
| `ApprenticeshipDefinition` | `Id`, `Name`, `Profession` |
| `SkillDefinition` | `Id`, `Name`, `Stat?` |
| `SpellListDefinition` | `Id`, `Name`, `Discipline`, `Stat`, `Restrictions` |
| `SpellDefinition` | `Id`, `List`, `Level`, `Name`, `Type`, `Targets`, `Range`, `Area`, `Modifier`, `Duration`, `Description` |
| `WeaponDefinition` | `Id`, `Name`, `AttackProfile`, `Damage`, `Critical`, `GoverningStat` |
| `ArmorDefinition` | `Id`, `Name`, `Profile`, `Metal`, `ProtectionPercent?` |
| `ItemDefinition` | `Id`, `Name`, `Kind` |
| `ManeuverDefinition` | `Id`, `Name` |
| `NpcBaselineDefinition` | `Id`, `Race`, `Profession`, `Array`, `Stats` (`StatValue` = `Score`, `Bonus`), `Hits`, `Signature` |
| `MonsterDefinition` | `Id`, `Name`, `Profession`, `Role`, `OverdrivenStat?`, `OverdriveEffect`, `Stats`, `Hits`, `Signature` |
| `ActionDefinition` | `Id`, `Name`, `Kind`, `ReferenceId?` |
| `KnownGapDefinition` | `Id`, `Description` |
| `GameDataValidationResult` | `Diagnostics`, `IsValid` |
| `SourceManifest` | `ManualRepo`, `ManualCommit`, `Workbook`, `Policy` |

## Services you can resolve

| Service | Purpose |
| --- | --- |
| `IBodyAndBrainEngine` | The typed client |
| `IDispatcher` | CQRS dispatch (from FeatureFlags.Cqrs) |
| `IGameDataCatalog` | `Data`, `Validate()`, `GetRace`, `GetProfession`, `GetAction` |
| `IGameStore` | Persistence: upsert/get players and NPCs, insert/list action logs |
| `IDiceRoller` | `D100()` and `Roll("NdS")`; replace for a seeded source |
| `NpcGenerator`, `MonsterGenerator` | Generation (normally used via commands) |
| `NpcMarkdownRenderer` | `Render(NpcRecord)` to Markdown |

### `IGameStore`

```csharp
Task UpsertPlayerAsync(PlayerCharacterRecord character, CancellationToken ct = default);
Task<PlayerCharacterRecord?> GetPlayerAsync(string id, CancellationToken ct = default);
Task UpsertNpcAsync(NpcRecord npc, CancellationToken ct = default);
Task<NpcRecord?> GetNpcAsync(string id, CancellationToken ct = default);
Task InsertActionLogAsync(ActionLogRecord log, CancellationToken ct = default);
Task<IReadOnlyList<ActionLogRecord>> ListActionLogsAsync(CancellationToken ct = default);
```

The shipped implementation is `LiteDbGameStore` (a single LiteDB file with
`players`, `npcs`, and `action_logs` collections). It is `IDisposable`; the DI
container owns its lifetime.

### `IDiceRoller`

```csharp
int D100();              // 1..100
int Roll(string notation); // "NdS", e.g. "2D6"; 0 if unparseable
```

Replace the registered `IDiceRoller` to inject a seeded or deterministic source
for lockstep multiplayer or reproducible content tests. Per-call determinism is
otherwise available through every command's `RollOverride`.

## `Mechanics` helpers

`Mechanics` is a public static utility you can reuse to mirror engine math in your
own UI or tooling:

| Method | Returns |
| --- | --- |
| `ClampStat(value)` | value clamped to 1..100 |
| `StatBonus(score)` | the bonus from the [stat bonus table](01-concepts.md#the-stat-bonus-table) |
| `LevelSignificance(level)` | signed triangular significance vs level 5 |
| `AgeModifier(level)` | `(body, brain)` age modifier |
| `IsBodyStat(stat)` | true for Strength/Agility/Constitution |
| `RollOutcome(roll[, total])` | the outcome band string |
| `OutcomeHits/IsCritical/IsHalf/IsDouble(outcome)` | band predicates |
| `RollScaledModifier(modifier, level, dice)` | a rolled, level-scaled magnitude |

`StatusEffects` exposes the canonical status list and its `ResistanceStat`,
`IsDamaging`, `DefaultMagnitude`, and `DefaultRounds` helpers.
