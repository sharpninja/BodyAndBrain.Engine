# Actions and Results

An **action** is any executable verb in the game: an attack, a spell, a maneuver,
equipping armor, using an item, or a system operation. There are **142** actions
in the catalog. Executing one always returns a structured **YAML action result**
and writes a record to the action log.

## Executing an action

```csharp
YamlActionResult result = await engine.ExecuteActionAsync(
    actionId: "physical-attack-longbow",
    actorId:  attacker.Id,
    targetId: defender.Id,               // optional
    rollOverride: null,                  // optional natural d100
    parameters: new Dictionary<string, string> { ["armor"] = "plate" }); // optional
```

`result.Document` is the YAML; `result.RequiresAdjudication` and
`result.AdjudicationReason` summarize whether the engine deferred the mechanic.

## Discovering actions

```csharp
var actions = await dispatcher.QueryAsync(new EnumerateActionsQuery());
foreach (var a in actions.Value!)
    Console.WriteLine($"{a.Id}\t{a.Kind}\t{a.Name}");
```

Each `ActionDefinition` has an `Id`, a `Name`, a `Kind`, and an optional
`ReferenceId` linking to the weapon, spell, item, maneuver, or armor it drives.

## Action kinds

| Kind | Count | What it does | Key parameters |
| --- | ---: | --- | --- |
| `spell` | 70 | Casts a spell; resolves per [Magic](05-magic.md) | (caster level drives scaling) |
| `item` | 35 | Uses or readies an item; most are descriptive (Torch is lit, etc.) | - |
| `maneuver` | 12 | A skill maneuver; succeeds on a Hit outcome | `rollOverride` |
| `physicalAttack` | 10 | A weapon attack; see [Combat](04-combat.md) | `armor`, `damage`, `weaponSkill` |
| `equipArmor` | 10 | Equips an armor piece; reports the equip | - |
| `system` | 4 | `apply-condition`, `damage-target`, `heal-target`, `defend` | see below |

### System actions

| Action id | Effect | Parameters |
| --- | --- | --- |
| `damage-target` | Subtracts `amount` hits from the target | `amount` (default 1); requires a target |
| `heal-target` | Adds `amount` hits to the target | `amount` (default 1); requires a target |
| `apply-condition` | Applies a status (see [Status Effects](06-status-effects.md)) | `condition`, optional `magnitude`, `duration` |
| `defend` | Reports a +10 defensive posture until the actor's next turn | - |

`damage-target` and `heal-target` are the engine's raw, weapon-independent way to
change hits, useful for traps, environmental damage, scripted events, or out-of-
combat healing.

## Parameters

Parameters are a `Dictionary<string,string>` (all values are strings; the engine
parses numbers as needed). The recognized keys:

| Key | Used by | Meaning |
| --- | --- | --- |
| `armor` | physicalAttack | Defender's armor (id, name, or profile) |
| `damage` | physicalAttack | Fixed base damage, skips the weapon roll |
| `weaponSkill` | physicalAttack | Fixed weapon-skill bonus, skips lookup |
| `amount` | damage-target, heal-target | Hits to change (default 1) |
| `condition` | apply-condition | Status name (canonical) or a free note |
| `magnitude` | apply-condition | Status magnitude (default per status) |
| `duration` | apply-condition | Status rounds (default per status) |

Unrecognized parameters are echoed back in the result's `inputs` block but
otherwise ignored, so you can pass your own metadata through for logging.

## The result schema

Every action result is a YAML document with this fixed top-level shape:

```yaml
actionId: physical-attack-1h-edge      # the action that ran
actionName: 1H Edge Attack
kind: physicalAttack
actor:                                  # who acted
  id: <guid>
  name: Mira
  type: pc                              # pc | npc | monster
  race: Human
  profession: Fighter
  level: 1
  hits: 40/40
target:                                 # who was targeted (null if none)
  id: <guid>
  name: Bandit
  type: npc
  ...
inputs: { armor: soft-leather }         # the parameters you passed
rolls: { d100: 80 }                     # dice the engine rolled
modifiers:                              # contributing sources (weapon, spell, ...)
  - source: 1H Edge
    governingStat: Strength
    damage: 1D6
    ...
outcome: { ... }                        # kind-specific result (see below)
stateChanges:                           # what changed in the world
  - { type: hits, before: 40, after: 36, maximum: 40, delta: -4 }
diagnostics: []                         # adjudication notes, if any
requiresAdjudication: false
# adjudicationReason: <code>            # present only when adjudicated
```

The ten top-level keys (`actionId`, `actor`, `target`, `inputs`, `rolls`,
`modifiers`, `outcome`, `stateChanges`, `diagnostics`, `requiresAdjudication`) are
always present, so you can parse defensively. `outcome` varies by kind.

### Physical attack result

`outcome` carries the weapon, the final damage, and three sub-blocks:

```yaml
outcome:
  result: Hit + Critical
  weapon: 1H Edge
  damage: 4
  accuracy:
    roll: 80
    weaponSkillBonus: 5
    governingStatBonus: 1
    baseAttackBonus: -10
    total: -4
    adjustedRoll: 76
  critical:                 # null when no critical
    type: Slash
    modifier: -2
    attackerDie: 3
    defenderDie: 1
    critScore: 0
    immediate: 2
    perRound: 2
  protection:
    armorProfile: Chain
    accuracyMitigation: 20  # how much armor degraded accuracy vs None
    protectionPercent: 25
    damageReduced: 2
    model: canonical-accuracy+derived-percent
  overdrive:                # present only for an overdriven monster attacker
    applied: true
    stat: Strength
    multiplier: 1.5
```

### Spell result

```yaml
outcome:
  spell: Bolts
  result: Spell damage applied.
  amount: 7
  overdrive: { applied: true, stat: Intelligence, multiplier: 1.5 }  # if overdriven
```

For cleanse spells `result` reports how many statuses were cleared; for effect
spells `result` is the spell's description text.

### Status tick result

```yaml
outcome:
  result: Status effects ticked.
  statusTicks:
    - type: Bleed
      applied: 2
      resistanceStat: Constitution
      resistanceRoll: 30
      resistanceTotal: 31
      resisted: false
      roundsRemaining: 2
      active: true
status:
  active:
    - { type: Bleed, magnitude: 2, roundsRemaining: 2 }
```

## Adjudication

When the engine cannot resolve an action deterministically from the canonical
sources, it returns **success** with:

- `requiresAdjudication: true`
- a stable `adjudicationReason` code
- an `outcome.result` of "GM adjudication required."
- a `diagnostics` entry with the code and a human-readable message

Common reason codes:

| Code | Raised when |
| --- | --- |
| `adjudication-spell` | A spell with no deterministic rule (summoning, control, teleport, revival, instant-kill) |
| `adjudication-<kind>` | An action whose kind has no deterministic executor in the canonical sources |
| `target-required` | `damage-target` or `heal-target` was sent with no target |

Treat adjudication as a contract boundary, not a failure: the engine is telling
you exactly where it will not invent rules. Your game resolves it (game master
prompt, house rule, or a custom subsystem) and proceeds. See
[Magic: Adjudicated Spells](05-magic.md#adjudicated-spells).

## The action log

Every executed action is appended to the `action_logs` collection in the store
with the action id, actor id, target id, a UTC timestamp, and the full YAML
document. Read it back through the store:

```csharp
var store = provider.GetRequiredService<IGameStore>();
var logs = await store.ListActionLogsAsync();
```

This gives you a complete, replayable audit trail of everything that happened,
useful for combat logs, debugging, and deterministic replays.

## Validating the catalog

Before shipping content changes, validate the catalog:

```csharp
var validation = await dispatcher.QueryAsync(new ValidateGameDataQuery());
if (!validation.Value!.IsValid)
    foreach (var d in validation.Value.Diagnostics) Console.WriteLine(d);
```

Validation checks for duplicate ids, races referencing unknown professions, NPC
baselines referencing unknown races or professions, spells referencing unknown
lists, and actions referencing missing weapons, spells, items, maneuvers, or
armor. A valid catalog returns zero diagnostics.
