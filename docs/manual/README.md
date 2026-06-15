# Body and Brain Engine: Creator Manual

A practical guide for **CRPG creators** who want to build a game on top of
`BodyAndBrain.Engine`: a CQRS-backed rules engine that implements the Body and
Brain (BaB) tabletop role-playing system.

This manual is written for the developer integrating the engine into a game
(client, server, tool, or bot), not for the end player. It explains what the
engine does, the rules it enforces, the data it ships with, and the exact API
you call to drive a game.

> **What the engine is.** A deterministic .NET library. You send it commands and
> queries; it creates characters, generates NPCs and monsters, resolves attacks
> and spells, ticks status effects, and persists everything to a local database.
> Every action returns a structured **YAML action result** describing exactly what
> happened, including the dice rolled and the modifiers applied.

> **What the engine is not.** It is not a game loop, a renderer, a turn scheduler,
> an AI, or a networking layer. It does not decide whose turn it is, draw a map,
> or run an encounter. You own the game; the engine owns the rules.

## Read this first

1. [Getting Started](getting-started.md): install, wire up dependency injection,
   create your first character, resolve your first attack.
2. [Core Concepts](01-concepts.md): stats, bonuses, hits, levels, dice, the CQRS
   model, determinism, and adjudication.

## Reference chapters

| Chapter | Covers |
| --- | --- |
| [01 - Core Concepts](01-concepts.md) | Stat model, bonus table, hits, level significance, roll-outcome bands, CQRS, determinism, YAML results, adjudication |
| [02 - Character Creation](02-character-creation.md) | Races, professions, apprenticeships, starting stats, skills, level-up |
| [03 - NPCs and Monsters](03-npcs-and-monsters.md) | NPC generation, baselines, derived fallback, the monster catalog, overdrive, sheet rendering |
| [04 - Combat](04-combat.md) | Physical attack pipeline, accuracy, base-attack tables, criticals, armor, weapons |
| [05 - Magic](05-magic.md) | Spell lists, spell resolution kinds, healing, damage, life drain, adjudicated spells, overdrive |
| [06 - Status Effects](06-status-effects.md) | Bleed, Poison, Disease, Stun, Move, Curse, applying, ticking, resistance |
| [07 - Actions and Results](07-actions-and-results.md) | The action catalog, action kinds, parameters, the YAML result schema, adjudication codes |
| [08 - API Reference](08-api-reference.md) | Every public command, query, record, and service |
| [09 - Catalog Reference](09-catalog-reference.md) | Full enumeration of all shipped game data |
| [10 - Glossary](10-glossary.md) | Terms used throughout the manual |

## Engine at a glance

- **Target framework:** .NET 10 (`net10.0`)
- **Package id:** `BodyAndBrain.Engine`
- **Persistence:** LiteDB (embedded, single file)
- **Data source:** an embedded YAML catalog transcribed from the BaB manual and
  the `BodyAndBrain.xlsm` workbook
- **Dispatch:** all operations flow through `SharpNinja.FeatureFlags.Cqrs.IDispatcher`
- **Determinism:** every randomized action accepts a roll override, so the same
  inputs always produce the same result (essential for tests, replays, and
  deterministic netcode)

## Shipped content

| Category | Count | Notes |
| --- | ---: | --- |
| Races | 8 | Human, Half-Elf, Elf, Dwarf, Halfling, Reptilian, Orc, Feline |
| Professions | 9 | Fighter, Berserker, Paladin, Ranger, Rogue, Bard, Wizard, Conjuror, Cleric |
| Apprenticeships | 19 | Two or three per profession |
| Spell lists | 10 | Body, Mind, Summoning, Destroy, Blessings, Curses, Leadership, Naturalist, Songs, Necromancer |
| Spells | 70 | Across all lists |
| Weapons | 10 | By attack profile (edge, concussion, thrown, bows, fist, claws) |
| Armors | 10 | None through Plate, plus Helm and Shield |
| Items | 35 | Weapons, armor, and travel gear |
| Maneuvers | 12 | Running, Swimming, Climbing, Traps, Locks, and more |
| NPC baselines | 36 | Canonical race/profession stat blocks |
| Monsters | 36 | 18 creatures, most with two profession variants |
| Actions | 142 | The full set of executable verbs |

Exact lists are in the [Catalog Reference](09-catalog-reference.md).

## Building and testing

From the repository root:

```pwsh
dotnet build BodyAndBrain.Engine.slnx
dotnet test BodyAndBrain.Engine.slnx
```

`dotnet test BodyAndBrain.Engine.slnx` is the required validation gate. The test
suite exercises every race/profession combination, every monster, every weapon,
and every catalog action through the dispatcher.

## A note on fidelity and adjudication

The engine implements the rules that the BaB manual and workbook specify
**unambiguously**. Where the source material leaves a mechanic underspecified
(summoning a creature, mind control, teleportation, instant-kill, revival), the
engine does not invent a rule. Instead it returns a successful result flagged
`requiresAdjudication: true` with a stable reason code, handing the decision to
your game (or a human game master). See
[Actions and Results](07-actions-and-results.md#adjudication) and
[Magic](05-magic.md#adjudicated-spells).
