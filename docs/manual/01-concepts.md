# Core Concepts

Read this once and the rest of the manual will make sense. Everything the engine
does is built from the pieces below.

## Stats

Every character, NPC, and monster has six stats, scored 1 to 100:

| Stat | Group | Governs (examples) |
| --- | --- | --- |
| Strength | Body | Melee weapons, Climb |
| Agility | Body | Missile weapons, Sprint, Swim, dodging |
| Constitution | Body | Hits, resisting Bleed/Poison/Disease/Stun |
| Intelligence | Brain | Wizardry, Conjuring, Locks, lore |
| Presence | Brain | Songs, Persuasion, Perception, leadership |
| Piety | Brain | Prayers, resisting Curses |

Strength, Agility, and Constitution are **Body** stats; Intelligence, Presence,
and Piety are **Brain** stats. The split matters for the age modifier (below).

Stat scores are always clamped to the range 1 to 100.

## The stat bonus table

Raw scores are converted to a **bonus** that feeds every roll. The thresholds are
not linear; high scores are disproportionately valuable:

| Score | Bonus |
| ---: | ---: |
| 100 | +10 |
| 95 to 99 | +4 |
| 90 to 94 | +3 |
| 75 to 89 | +2 |
| 50 to 74 | +1 |
| 1 to 49 | 0 |

`Mechanics.StatBonus(score)` implements this. When you see "the governing-stat
bonus" in combat or "the resistance bonus" in status ticks, this is the number.

## Hits

**Hits** are the health pool (`CurrentHits` out of `MaxHits`). A combatant at 0
hits is out of the fight; the engine clamps hits to `[0, MaxHits]` and never
below zero.

`MaxHits` is derived, never entered directly:

- **Player at creation:** `35 + ConstitutionBonus * 5`
- **Player after level-up and all generated NPCs:**
  `max(1, 35 + LevelSignificance(level) * 3 + ConstitutionBonus * 5)`
- **Monsters:** `max(1, baselineHits + LevelSignificance(level) * 3)`

Healing and damage adjust `CurrentHits`; gaining a level restores 5 hits (capped
at `MaxHits`).

## Levels and level significance

Levels run from 1 to 50. The engine treats **level 5 as the baseline** and
measures every other level as a signed "significance" relative to it. The
function is triangular, so power ramps up (and down) faster the further you get
from level 5:

```
LevelSignificance(level) = sign(level - 5) * T(|level - 5|)
where T(n) = n * (n + 1) / 2   (the nth triangular number)
```

Examples:

| Level | Significance |
| ---: | ---: |
| 1 | -10 |
| 2 | -6 |
| 4 | -1 |
| 5 | 0 |
| 6 | +1 |
| 8 | +6 |
| 10 | +15 |
| 20 | +120 |
| 50 | +1035 |

Significance scales NPC and monster stats and hits. A level-1 NPC is meaningfully
frailer than its level-5 baseline; a level-20 NPC is dramatically stronger.

## The age modifier

Tied to level, the **age modifier** nudges Body and Brain stats in opposite
directions to model youth and maturity. It is applied during NPC and monster
generation:

| Level | Body stats | Brain stats |
| --- | ---: | ---: |
| 1 | -10 | -10 |
| 2 to 4 | +5 | -5 |
| 5 to 7 | 0 | 0 |
| 8 and up | -5 | +5 |

Read this as: the very young are weak everywhere; young adults are physically
peaking but mentally green; veterans trade a little Body for Brain.

## Dice and roll outcomes

The engine rolls a **d100** (1 to 100) for attacks, spells where relevant, and
status resistance. It also rolls weapon and effect dice in `NdS` notation
(`1D6`, `2D10`, `1D4`).

The natural d100 plus all applicable modifiers produces a **total**, and the
total selects an outcome band. Two rolls are always special regardless of
modifiers:

| Natural roll | Outcome |
| --- | --- |
| 1 | **Fumble** (`F`) |
| 100 | **2x Hit + Critical** (best possible) |

For every other natural roll, the bonus-adjusted **total** chooses the band:

| Total | Outcome |
| ---: | --- |
| 25 or less | Miss |
| 26 to 49 | Hit / 2 (half damage) |
| 50 to 74 | Hit |
| 75 or more | Hit + Critical |

This single table drives physical attacks and, reused, status resistance rolls.
`Mechanics.RollOutcome(naturalRoll, total)` returns the band; helper predicates
(`OutcomeHits`, `OutcomeIsCritical`, `OutcomeIsHalf`, `OutcomeIsDouble`) read it.

## Scaled modifiers

Spell and effect magnitudes are written in the catalog as text such as `1D10`,
`1D6 per 5 Levels`, `2D10 per 5 Levels`, or `+5 per 5 Levels`. The engine parses
these into a rolled value:

- A missing modifier (`-` or empty) yields 0.
- `per N Levels` multiplies the rolled dice by `max(1, casterLevel / N)`.
- Flat values (`+5`) are taken literally, then scaled the same way.

`Mechanics.RollScaledModifier(modifier, level, dice)` does this. It is why a
high-level caster's `1D8 per 5 Levels` spell hits far harder than a novice's.

## The CQRS model

Every operation is either a **command** (mutates state) or a **query**
(read-only), dispatched through `IDispatcher`:

- **Commands** return a `Result<T>`: create a character, level up, generate an
  NPC or monster, execute an action, tick statuses.
- **Queries** return a `Result<T>`: get the game data, validate it, enumerate
  actions, load a character or NPC, render an NPC sheet.

You never construct handlers or a "game loop" object yourself. You build the
command or query, dispatch it, and read the result. This keeps the rules engine
free of orchestration: your game decides *when* to send each command.

See [API Reference](08-api-reference.md) for the full list.

## Determinism and roll overrides

The engine is deterministic by design. The only source of randomness is
`IDiceRoller`, and every randomized command accepts a `RollOverride` (the natural
d100):

- Pass `RollOverride` and the outcome is fixed.
- When overridden, even the critical dice (attacker and defender `1D4`) are
  derived from the override rather than rolled, so the same inputs always produce
  the same damage.

This makes outcomes reproducible for unit tests, combat replays, and lockstep or
server-authoritative multiplayer. If you need your own randomness source (a
seeded PRNG for deterministic netcode), replace the registered `IDiceRoller`.

## YAML action results

Executing an action returns a `YamlActionResult` whose `Document` is a YAML string
with a fixed top-level shape (`actionId`, `actor`, `target`, `inputs`, `rolls`,
`modifiers`, `outcome`, `stateChanges`, `diagnostics`, `requiresAdjudication`).
Every executed action is also written to an append-only action log in the store.

The result is meant to be machine-readable (parse it to update your UI) and
human-readable (log it for a game master). Its full schema is in
[Actions and Results](07-actions-and-results.md).

## Adjudication

When an action is defined in the catalog but the canonical sources do not specify
deterministic mechanics, the engine does **not** guess. It returns a *successful*
result with:

- `requiresAdjudication: true`
- a stable `adjudicationReason` code (for example `adjudication-spell`)
- an `outcome.result` of "GM adjudication required."

Your game decides what happens next: prompt a human game master, apply a
house rule, or run your own subsystem. Adjudication is a feature, not an error:
it marks the precise boundary of what the engine will commit to. See
[Magic: Adjudicated Spells](05-magic.md#adjudicated-spells).

## Persistence

State lives in a single LiteDB file at `BodyAndBrainEngineOptions.StorePath`.
Three collections are maintained:

- `players` (player characters, keyed by `Id`)
- `npcs` (NPCs and monsters, keyed by `Id`)
- `action_logs` (every executed action, append-only)

Records are stored as structured documents, so you can inspect them with any
LiteDB tooling. The catalog itself is read-only embedded YAML and is never
written back.
