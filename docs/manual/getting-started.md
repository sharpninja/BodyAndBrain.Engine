# Getting Started

This chapter takes you from an empty project to a resolved attack. It assumes
you are comfortable with C# and .NET dependency injection.

## 1. Reference the engine

The engine targets `net10.0`. Add a project reference to
`src/BodyAndBrain.Engine/BodyAndBrain.Engine.csproj`, or reference the
`BodyAndBrain.Engine` package if you consume it as a NuGet artifact.

The engine depends on:

- `SharpNinja.FeatureFlags.Cqrs` (the dispatcher)
- `LiteDB` (persistence)
- `Microsoft.Extensions.DependencyInjection.Abstractions` and
  `Microsoft.Extensions.Logging`
- `YamlDotNet` (catalog loading and result serialization)

## 2. Register services

`AddBodyAndBrainEngine` wires up the catalog, the LiteDB store, the dice roller,
the generators, the action executor, the CQRS dispatcher, and the high-level
`IBodyAndBrainEngine` client.

```csharp
using BodyAndBrain.Engine;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddBodyAndBrainEngine(options =>
{
    // Where the LiteDB file lives. Defaults to bodyandbrain.engine.db
    // next to the running assembly.
    options.StorePath = Path.Combine(AppContext.BaseDirectory, "mygame.db");
});

using var provider = services.BuildServiceProvider();
```

> **Trimming note.** `AddBodyAndBrainEngine` is annotated
> `[RequiresUnreferencedCode]` because the CQRS registration scans engine handler
> types by reflection. If you publish trimmed or AOT, keep the engine assembly
> rooted.

## 3. Two ways to call the engine

There are two entry points. They do the same work; pick whichever fits your
architecture.

### a) The typed client (`IBodyAndBrainEngine`)

A small interface with task-returning methods. It throws an
`InvalidOperationException` if an operation fails. Good for application code that
prefers exceptions.

```csharp
var engine = provider.GetRequiredService<IBodyAndBrainEngine>();

var hero = await engine.CreatePlayerCharacterAsync(
    name: "Mira",
    race: "Human",
    profession: "Fighter",
    apprenticeship: "Soldier");
```

### b) The dispatcher (`IDispatcher`)

Send commands and queries directly and inspect the `Result<T>` (success or
failure) yourself. Good for code that already speaks CQRS, and for the queries
that the typed client does not expose (such as loading a character or rendering
an NPC sheet).

```csharp
using SharpNinja.FeatureFlags.Cqrs;

var dispatcher = provider.GetRequiredService<IDispatcher>();

var result = await dispatcher.SendAsync(
    new CreatePlayerCharacterCommand("Mira", "Human", "Fighter", "Soldier"));

if (result.IsSuccess)
    Console.WriteLine($"Created {result.Value!.Name} ({result.Value.Id})");
else
    Console.WriteLine($"Failed: {result.Error}");
```

Both approaches share the same handlers, so behavior is identical.

## 4. Create a character

```csharp
var hero = await engine.CreatePlayerCharacterAsync("Mira", "Human", "Fighter", "Soldier");
// hero.Id     -> a stable GUID string, use this as the actor/target id
// hero.Stats  -> { Strength: 52, Agility: 51, Constitution: 51, ... }
// hero.MaxHits, hero.CurrentHits
```

The race must allow the profession, and the apprenticeship must belong to the
profession, or creation fails. See [Character Creation](02-character-creation.md).

## 5. Spawn an opponent

```csharp
// An ordinary NPC from a race/profession baseline:
var bandit = await engine.GenerateNpcAsync("Human", "Rogue", level: 5, name: "Bandit");

// Or a monster from the catalog:
var ogre = await engine.GenerateMonsterAsync("ogre-fighter", level: 5, name: "Cave Ogre");
```

Generated NPCs and monsters are persisted by default, so their ids are valid
targets immediately. See [NPCs and Monsters](03-npcs-and-monsters.md).

## 6. Resolve an attack

```csharp
var result = await engine.ExecuteActionAsync(
    actionId: "physical-attack-1h-edge",
    actorId: hero.Id,
    targetId: bandit.Id,
    parameters: new Dictionary<string, string>
    {
        ["armor"] = "soft-leather",   // what the defender is wearing
    });

Console.WriteLine(result.Document);          // the full YAML result
Console.WriteLine(result.RequiresAdjudication); // false for a resolved attack
```

The target's hits are mutated and persisted as part of the call. The returned
`Document` is a YAML string you can log, display, or parse. Its schema is
documented in [Actions and Results](07-actions-and-results.md).

## 7. Make it deterministic

Every randomized action accepts a `rollOverride` (the natural d100). Use it in
tests, replays, or any system that needs reproducible outcomes:

```csharp
var result = await engine.ExecuteActionAsync(
    "physical-attack-1h-edge", hero.Id, bandit.Id,
    rollOverride: 80,                                  // always "Hit + Critical"
    parameters: new Dictionary<string, string> { ["damage"] = "6" });
```

With `rollOverride` set, the engine also derives the critical dice
deterministically, so the same call always yields the same damage. See
[Core Concepts: Determinism](01-concepts.md#determinism-and-roll-overrides).

## 8. Read state back

```csharp
var npc = await dispatcher.QueryAsync(new GetNpcQuery(bandit.Id));
var sheet = await dispatcher.QueryAsync(new RenderNpcMarkdownQuery(bandit.Id));
Console.WriteLine(sheet.Value);  // a Markdown character sheet
```

## Where to go next

- To understand the numbers behind the rolls, read [Core Concepts](01-concepts.md).
- To drive a full combat turn, read [Combat](04-combat.md).
- To cast spells, read [Magic](05-magic.md).
- For the complete list of action ids you can execute, read
  [Actions and Results](07-actions-and-results.md) and the
  [Catalog Reference](09-catalog-reference.md).
