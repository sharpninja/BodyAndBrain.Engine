# BodyAndBrain.Engine

A deterministic, CQRS-backed rules engine for the **Body and Brain (BaB)**
role-playing system, built for CRPG creators. Send it commands and queries; it
creates characters, generates NPCs and monsters, resolves attacks and spells,
ticks status effects, and persists everything, returning a structured YAML result
for every action.

- **Target framework:** .NET 10 (`net10.0`)
- **Persistence:** LiteDB (embedded, single file)
- **Dispatch:** `SharpNinja.FeatureFlags.Cqrs.IDispatcher`
- **Data:** an embedded YAML catalog transcribed from the BaB manual and the
  `BodyAndBrain.xlsm` workbook
- **Configuration:** Magic numbers (base hits, level caps, etc.) loaded from `Data/magic-numbers.yaml` via `IConfiguration`
- **Determinism:** Per-action `rollOverride` + optional `RandomSeed` on `BodyAndBrainEngineOptions` for full run reproducibility

## What it is, and is not

**It is** a .NET library that owns the *rules*: character creation, generation,
combat math, spell resolution, status effects, and a replayable action log. Every
randomized operation accepts a roll override, so identical inputs always produce
identical results (ideal for tests, replays, and server-authoritative netcode).

**It is not** a game loop, renderer, turn scheduler, AI, or network layer. It does
not decide whose turn it is or draw anything. You own the game; the engine owns the
rules. Where the canonical sources leave a mechanic underspecified (summoning, mind
control, teleport, revival), the engine does not invent rules: it returns a success
flagged `requiresAdjudication: true` with a stable reason code, handing the
decision to your game or a human game master.

## Quick start

Register the engine and drive it through the typed client or the dispatcher:

```csharp
using BodyAndBrain.Engine;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddBodyAndBrainEngine(o => o.StorePath = "mygame.db");
using var provider = services.BuildServiceProvider();

var engine = provider.GetRequiredService<IBodyAndBrainEngine>();

var hero  = await engine.CreatePlayerCharacterAsync("Mira", "Human", "Fighter", "Soldier");
var ogre  = await engine.GenerateMonsterAsync("ogre-fighter", level: 5, name: "Cave Ogre");

var result = await engine.ExecuteActionAsync(
    "physical-attack-1h-edge", hero.Id, ogre.Id,
    parameters: new Dictionary<string, string> { ["armor"] = "soft-leather" });

Console.WriteLine(result.Document);            // full YAML action result
Console.WriteLine(result.RequiresAdjudication); // false for a resolved attack
```

See [docs/manual/getting-started.md](docs/manual/getting-started.md) for the full
walkthrough.

## Documentation

The complete creator manual lives in **[docs/manual/](docs/manual/README.md)**:

| Chapter | Covers |
| --- | --- |
| [Getting Started](docs/manual/getting-started.md) | Install, DI setup, first character to first attack |
| [Core Concepts](docs/manual/01-concepts.md) | Stats, bonuses, hits, level significance, dice, CQRS, determinism, adjudication |
| [Character Creation](docs/manual/02-character-creation.md) | Races, professions, apprenticeships, stats, level-up |
| [NPCs and Monsters](docs/manual/03-npcs-and-monsters.md) | Generation, baselines, derived fallback, monsters, overdrive |
| [Combat](docs/manual/04-combat.md) | Attack pipeline, base-attack and critical tables, armor, weapons |
| [Magic](docs/manual/05-magic.md) | Spell lists, resolution kinds, adjudicated spells, overdrive |
| [Status Effects](docs/manual/06-status-effects.md) | Bleed, Poison, Disease, Stun, Move, Curse; applying and ticking |
| [Actions and Results](docs/manual/07-actions-and-results.md) | Action catalog, parameters, YAML result schema, adjudication codes |
| [API Reference](docs/manual/08-api-reference.md) | Every public command, query, record, and service |
| [Catalog Reference](docs/manual/09-catalog-reference.md) | Full enumeration of all shipped game data |
| [Glossary](docs/manual/10-glossary.md) | Terms |

## Shipped content

| Category | Count | Category | Count |
| --- | ---: | --- | ---: |
| Races | 8 | Spells | 70 |
| Professions | 9 | Weapons | 10 |
| Apprenticeships | 19 | Armors | 10 |
| Spell lists | 10 | Items | 35 |
| Maneuvers | 12 | NPC baselines | 36 |
| Monsters | 36 | Actions | 142 |

## Building and testing

From the repository root:

```pwsh
dotnet build BodyAndBrain.Engine.slnx
dotnet test  BodyAndBrain.Engine.slnx
```

`dotnet test BodyAndBrain.Engine.slnx` is the required validation gate. The suite
exercises every race/profession combination, every monster, every weapon, and
every catalog action through the dispatcher.

## Repository layout

```
src/BodyAndBrain.Engine/          The engine library
  Data/bodyandbrain.game.yaml     Embedded canonical game catalog
tests/BodyAndBrain.Engine.Tests/  Dispatch-level requirement coverage
docs/manual/                      Creator manual (start here)
docs/Project/wiki/                Functional, technical, and testing requirements
BodyAndBrain.Engine.slnx          Solution
```

## Canonical sources and fidelity

The engine implements only what the BaB manual and `BodyAndBrain.xlsm` workbook
specify unambiguously. Where the workbook supplies a number and the manual supplies
intent, the manual's labels and effects win on conflict. Mechanics the sources
leave open are surfaced as adjudication rather than guessed. See the
[manual's note on adjudication](docs/manual/07-actions-and-results.md#adjudication).

## Authors

Gateway Programming School; SharpNinja.
