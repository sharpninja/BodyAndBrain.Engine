# NPCs and Monsters

The engine generates two kinds of non-player combatants:

- **NPCs**: ordinary people, built from a race + profession at a chosen level.
- **Monsters**: catalog creatures (goblins, ogres, dragons, liches) with fixed
  stat blocks and, for the exceptional ones, an overdrive trait.

Both produce an `NpcRecord` and are persisted (by default) so their ids are
immediately usable as combat actors or targets.

## Generating an NPC

```csharp
var bandit = await engine.GenerateNpcAsync(
    race:       "Human",
    profession: "Rogue",
    level:      5,
    name:       "Bandit");   // optional; auto-named if omitted
```

Level must be 1 to 50. Race and profession accept names or id slugs.

### Baselines and the derived fallback

The engine ships **36 canonical baselines**: one stat block per common
race/profession pairing (for example `human-fighter`, `elf-wizard`). When you
request a combination:

- **An exact baseline exists.** The engine uses its canonical stat block and
  `signature`, then scales by level.
- **No baseline exists** (an unusual but rule-legal pairing). The engine
  **derives** one so that *any* canonical race/profession combination still
  generates. The derived block starts each stat at `50 + racialBonus * 5`, adds
  `+20` to the profession's primary stat, and clamps. The record is flagged
  `Derived = true` and given the signature "Derived <Race> <Profession> baseline".

This guarantees you can spawn any combination your game allows without shipping a
hand-authored block for every one.

### Level scaling

A baseline is a level-5 reference. Generation scales each stat by level
significance and the age modifier, weighted by how important the stat is to the
profession:

```
scaledStat = clamp(baseScore + round(LevelSignificance(level) * weight) + ageMod)

weight = 2.0  if the stat is the profession's primary stat
         1.5  if the stat is Constitution, Agility, Presence, or Piety
         1.0  otherwise

ageMod = the Body or Brain age modifier for the level (see Core Concepts)
```

Hits scale as `max(1, 35 + LevelSignificance(level) * 3 + ConstitutionBonus * 5)`.

The practical effect: primary stats balloon with level, secondary survivability
stats keep pace, and dump stats lag. A level-50 NPC is a monster in its own
right; a level-1 NPC is fragile.

### Auto-naming

If you omit `name`, the engine names the NPC by an age band derived from level:

| Level | Label |
| --- | --- |
| 1 | Adolescent |
| 2 to 4 | Young Adult |
| 5 to 7 | Adult |
| 8 to 10 | Experienced |
| 11 to 20 | Veteran |
| 21 to 30 | Master |
| 31 to 40 | Legendary |
| 41 to 50 | Mythic |

So `GenerateNpcAsync("Orc", "Fighter", 5)` becomes "Adult Orc Fighter".

## Generating a monster

```csharp
var ogre = await engine.GenerateMonsterAsync(
    monsterId: "ogre-fighter",
    level:     5,
    name:      "Cave Ogre");   // optional; defaults to "Ogre (warrior)"
```

Monster ids combine a creature and a profession variant, for example
`goblin-rogue`, `troll-berserker`, `dragon-fighter`. Most creatures ship in two
profession variants. The full list is in the
[Catalog Reference](09-catalog-reference.md#monsters).

A generated monster is an `NpcRecord` with:

- `IsMonster = true`
- `Monster` set to the creature name
- `Race` set to the creature name (a Troll's "race" is Troll)
- the canonical level-5 stat block, scaled by level exactly like an NPC
- `MaxHits = max(1, baselineHits + LevelSignificance(level) * 3)`
- `OverdrivenStat` carried forward for exceptional creatures (below)

Requesting an unknown monster id, or a monster with an empty stat block, fails.

## Overdrive (exceptional monsters)

A handful of legendary creatures are **overdriven**: one governing stat's effect
is multiplied by **1.5** during resolution. This models the manual's "Governed
skill effect x1.5" trait for the apex monsters.

The exceptional creatures and their overdriven stat:

| Monster variant | Overdriven stat | Effect in play |
| --- | --- | --- |
| `dragon-fighter` | Strength | 1H/2H edge attack damage x1.5 |
| `dragon-conjuror` | Intelligence | Conjuring/Wizardry spell magnitude x1.5 |
| `lich-wizard` | Intelligence | Wizardry spell magnitude x1.5 |
| `lich-conjuror` | Intelligence | Conjuring spell magnitude x1.5 |
| `demon-berserker` | Strength | Melee attack damage x1.5 |
| `demon-conjuror` | Intelligence | Conjuring spell magnitude x1.5 |
| `vampire-rogue` | Agility | Missile/finesse attack damage x1.5 |
| `vampire-conjuror` | Intelligence | Conjuring spell magnitude x1.5 |

Overdrive fires only when the action's governing stat matches the overdriven
stat. A Dragon Fighter's overdriven Strength multiplies a Strength-governed edge
attack, but would not multiply an Agility-governed bow attack. When it applies,
the YAML result includes an `overdrive` block:

```yaml
outcome:
  overdrive:
    applied: true
    stat: Strength
    multiplier: 1.5
```

Ordinary monsters and NPCs have `OverdrivenStat = null` and never overdrive. The
mechanics are detailed in [Combat](04-combat.md#overdrive) and
[Magic](05-magic.md#overdrive).

## Rendering a character sheet

NPCs and monsters can be rendered to Markdown:

```csharp
var sheet = await dispatcher.QueryAsync(new RenderNpcMarkdownQuery(ogre.Id));
Console.WriteLine(sheet.Value);
```

produces:

```markdown
# Cave Ogre

*Race:* Ogre
*Profession:* Fighter
*Level:* 5
*Hits:* 52/52
*Signature:* Primary Melee 5

| Stat | Score | Bonus |
| --- | ---: | ---: |
| Agility | 75 | 2 |
| Constitution | 82 | 2 |
| ...
```

The query fails if the id is not a stored NPC. There is no equivalent renderer for
player characters.

## The NPC record

Generated combatants share the `NpcRecord` shape with player characters in combat
(both are wrapped as a "combatant" internally). Key fields:

| Field | Meaning |
| --- | --- |
| `Id` | Target/actor id |
| `Race`, `Profession`, `Level` | Identity |
| `Array` | The baseline array name (`Physical`, `Scholar`, `Devout`, ...) or `derived` / `monster` |
| `Stats` | The six scaled stats |
| `MaxHits`, `CurrentHits` | Health |
| `Signature` | The signature skill rating, for example `Primary Melee 5` |
| `IsMonster`, `Monster` | Monster flag and creature name |
| `OverdrivenStat` | The overdriven stat, or null |
| `Derived` | True when generated from a derived fallback baseline |
| `Statuses` | Active status effects (see [Status Effects](06-status-effects.md)) |

Full field list is in the [API Reference](08-api-reference.md#npcrecord).

## The signature skill

NPCs and monsters do not carry a full skill table. Instead they carry a
**signature** string such as `Primary Melee 5` or `Conjuring 5`. When combat needs
a weapon-skill rating for an NPC or monster and no explicit `weaponSkill`
parameter is given, the engine reads the digits out of the signature (here, 5).
This keeps generated combatants combat-ready without a skill spreadsheet.
