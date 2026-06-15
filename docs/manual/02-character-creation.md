# Character Creation

This chapter covers player characters: how to create one, what the engine
derives, and how leveling works. NPCs and monsters are generated differently and
are covered in [NPCs and Monsters](03-npcs-and-monsters.md).

## Creating a player character

```csharp
var pc = await engine.CreatePlayerCharacterAsync(
    name:          "Mira",
    race:          "Human",
    profession:    "Fighter",
    apprenticeship:"Soldier");
```

or via the dispatcher:

```csharp
var result = await dispatcher.SendAsync(
    new CreatePlayerCharacterCommand("Mira", "Human", "Fighter", "Soldier"));
```

Race, profession, and apprenticeship may be passed by **display name**
("Half-Elf", "Fighter", "Soldier") or by **id slug** ("half-elf", "fighter",
"soldier"). Matching is case-insensitive.

### Validation rules

Creation fails (returns a failure `Result`, or throws from the typed client) when:

1. **The race does not allow the profession.** Each race lists its
   `validProfessions`. An Orc may be a Fighter, Berserker, or Cleric, but not a
   Wizard.
2. **The apprenticeship does not belong to the profession.** "Soldier" is a
   Fighter apprenticeship; pairing it with a Cleric fails.

See the [Catalog Reference](09-catalog-reference.md) for the full race,
profession, and apprenticeship tables.

### What the engine derives

| Field | Value at creation |
| --- | --- |
| `Id` | A fresh GUID (32 hex chars). Use it as the actor/target id. |
| `Level` | 1 |
| `Stats` | `clamp(50 + racialBonus)` for each of the six stats |
| `Skills` | One entry: the profession's primary stat, rated 1 |
| `MaxHits` / `CurrentHits` | `35 + ConstitutionBonus * 5` |

A character starts from a baseline of 50 in each stat, adjusted by the race's
stat bonuses, then clamped to 1 to 100. For a **Human Fighter**:

- Human stat bonuses: Strength +2, Agility +1, Constitution +1, others 0.
- Starting stats: Strength 52, Agility 51, Constitution 51, Intelligence 50,
  Presence 50, Piety 50.
- Constitution 51 yields a +1 bonus, so `MaxHits = 35 + 1 * 5 = 40`.

> **Skill seeding detail.** At creation the engine records a single skill keyed by
> the profession's *primary stat name* (for a Fighter that is `Strength`), rated
> 1. This is a starting marker. Combat reads named weapon skills such as
> `Primary Melee` and `Primary Missile`, which you add through level-up (below) or
> supply per attack via the `weaponSkill` parameter. See
> [Combat: Accuracy](04-combat.md#accuracy).

## Races

Eight races ship with the engine. Each defines stat bonuses, resistances, and the
professions it may take.

| Race | Str | Agi | Con | Int | Pre | Pie | Notable |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Human | +2 | +1 | +1 | 0 | 0 | 0 | All nine professions |
| Half-Elf | +1 | +2 | +1 | +1 | +1 | -2 | Versatile, low Piety |
| Elf | 0 | +3 | 0 | +2 | +2 | -3 | Agile and clever |
| Dwarf | +2 | -1 | +2 | 0 | 0 | +1 | Tough, poison-resistant |
| Halfling | -1 | +2 | 0 | +1 | +2 | 0 | Nimble, curse-resistant |
| Reptilian | +2 | -1 | +1 | 0 | 0 | +2 | Disease-resistant, devout |
| Orc | +3 | 0 | +2 | -3 | -1 | 0 | Brutal, poor Brain stats |
| Feline | -1 | +3 | 0 | 0 | +3 | -1 | Fast and charismatic |

Resistances (Poison, Disease, Wizardry, Conjuring, Curses) are stored on each
race for downstream use. Full numbers are in the
[Catalog Reference](09-catalog-reference.md#races).

## Professions

Nine professions ship with the engine. Each has a **primary stat** (which scales
fastest with level) and a **class bonus** (a narrative special ability your game
applies; the engine stores the text but does not enforce it mechanically).

| Profession | Primary stat | Class bonus (summary) |
| --- | --- | --- |
| Fighter | Strength | Shield wall: groups add +25 defense and provide cover |
| Berserker | Strength | Dual battle-axes: roll twice, best hit, summed damage |
| Paladin | Piety | May pray during combat without using a turn |
| Ranger | Agility | Extra short-bow attack every four levels, up to five |
| Rogue | Agility | Hides in urban areas without failure in normal attire |
| Bard | Presence | Once a day, sway a decision without rolling |
| Wizard | Intelligence | Cast from an unknown scroll at full effect in one turn |
| Conjuror | Intelligence | Once a day, transform into a summonable pet |
| Cleric | Piety | Ritually revive a dead follower (stat-loss cost) |

Class bonuses are intentionally left to your game to implement. The engine
exposes the text on `ProfessionDefinition.ClassBonus` so you can surface or
enforce it yourself.

## Apprenticeships

An apprenticeship is a sub-specialization tied to one profession. Each profession
has two or three. They are required at creation and recorded on the character.

| Profession | Apprenticeships |
| --- | --- |
| Bard | Actor, Fencing, Politician |
| Berserker | Lycanthropy, Wrestling |
| Cleric | Monk, Priesthood, Undead Hunter |
| Conjuror | Illusions, Necromancy |
| Fighter | Soldier, Squire |
| Paladin | Knight |
| Ranger | Animist, Tracker |
| Rogue | Burglar, Pick Pocket |
| Wizard | Alchemy, Scholarship |

> The **Necromancy** apprenticeship (Conjuror) is the gate for the **Necromancer**
> spell list, which lists "Necromancy Apprenticeship" among its restrictions. See
> [Magic](05-magic.md).

## Leveling up

Level-up is an explicit command. Your game decides when a character advances and
which stats and skills improve; the engine applies and clamps the changes.

```csharp
var leveled = await engine.ApplyLevelUpAsync(
    characterId: pc.Id,
    statIncrements:  new Dictionary<string, int> { ["Strength"] = 2 },
    skillIncrements: new Dictionary<string, int> { ["Primary Melee"] = 1 });
```

What happens:

1. **Level increases by 1.** The maximum level is 50; attempting to advance past
   it fails.
2. **Stat increments** are added and clamped to 1 to 100.
3. **Skill increments** are added and clamped to 0 to 10.
4. **MaxHits is recomputed** as
   `max(1, 35 + LevelSignificance(level) * 3 + ConstitutionBonus * 5)`.
5. **CurrentHits gains 5** (capped at the new `MaxHits`).
6. A line is appended to `LevelUpHistory` recording the increment counts.

Both increment dictionaries are optional; pass only what changed. Because hits
depend on level significance (level 5 is the baseline), a character's `MaxHits`
grows steeply as level climbs past 5.

### Skills

Skills are rated 0 to 10 and keyed by name. The names the combat system reads are
`Primary Melee` (Strength-governed) and `Primary Missile` (Agility-governed); the
full skill list (Perception, Climb, Locks, Prayer, Conjuring, and many more) is
in the [Catalog Reference](09-catalog-reference.md#skills). Add skill points
through `ApplyLevelUpCommand`. For a one-off attack you can also bypass stored
skills with the `weaponSkill` parameter (see [Combat](04-combat.md#accuracy)).

## Loading and rendering

```csharp
var loaded = await dispatcher.QueryAsync(new GetPlayerCharacterQuery(pc.Id));
// loaded.Value is the PlayerCharacterRecord, or null if not found
```

There is no built-in Markdown renderer for player characters (only for NPCs and
monsters, via `RenderNpcMarkdownQuery`). Read the `PlayerCharacterRecord` fields
to build your own sheet. The record's shape is documented in the
[API Reference](08-api-reference.md#playercharacterrecord).
