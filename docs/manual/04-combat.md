# Combat

Physical combat is the engine's most detailed subsystem. This chapter walks the
full attack pipeline, the canonical Base Attack Bonus and critical tables, and the
armor model, then lists every weapon.

## Executing a physical attack

```csharp
var result = await engine.ExecuteActionAsync(
    actionId: "physical-attack-1h-edge",
    actorId:  attacker.Id,
    targetId: defender.Id,
    rollOverride: null,   // omit for a real d100, or pin it for determinism
    parameters: new Dictionary<string, string>
    {
        ["armor"]       = "chain",  // what the DEFENDER wears (id, name, or profile)
        // optional overrides:
        // ["damage"]     = "6",     // skip the weapon damage roll
        // ["weaponSkill"]= "5",     // skip stored-skill / signature lookup
    });
```

There is one attack action per weapon profile. The action's `ReferenceId` points
at the weapon definition that supplies the damage dice, governing stat, and attack
profile. See the [weapon table](#weapons) below for the action ids.

## The attack pipeline

Each physical attack runs these steps, all surfaced in the YAML result.

### 1. Accuracy

```
accuracyTotal = weaponSkillBonus + governingStatBonus + baseAttackBonus
adjustedRoll  = naturalRoll + accuracyTotal
```

- **weaponSkillBonus** comes from, in order of precedence:
  1. the `weaponSkill` parameter, if provided;
  2. the actor's stored skill (`Primary Missile` for bows and thrown weapons,
     otherwise `Primary Melee`);
  3. for NPCs and monsters with no stored skill, the digits parsed from their
     signature (for example `Primary Melee 5` yields 5);
  4. otherwise 0.
- **governingStatBonus** is `StatBonus(actor's score in the weapon's governing
  stat)` (Strength for melee, Agility for missiles; see the weapon table).
- **baseAttackBonus** comes from the Base Attack Bonus table: the weapon's attack
  profile against the defender's armor column (next section).

### 2. Outcome

The natural roll and the adjusted total select an outcome band (from
[Core Concepts](01-concepts.md#dice-and-roll-outcomes)):

| Natural | Total | Outcome | Damage effect |
| --- | --- | --- | --- |
| 1 | any | Fumble (`F`) | no damage |
| 100 | any | 2x Hit + Critical | double base, plus critical |
| other | <= 25 | Miss | no damage |
| other | 26 to 49 | Hit / 2 | half base |
| other | 50 to 74 | Hit | full base |
| other | 75+ | Hit + Critical | full base, plus critical |

### 3. Base damage

If the outcome hits, base damage is the `damage` parameter if provided, otherwise
a roll of the weapon's damage dice (`1D6`, `2D6`, ...). It is then halved (Hit / 2)
or doubled (2x) per the band.

### 4. Critical resolution

On a critical outcome the engine consults the canonical critical tables:

1. **Critical type** is looked up from the weapon profile against the armor
   column, yielding a type (Slash, Crush, Puncture, Claw) and a numeric modifier.
   The Fist has no critical type and never crits.
2. **Critical score** = `attackerDie(1D4) + critModifier - defenderDie(1D4)`. With
   a `rollOverride` set, both `1D4` values are derived deterministically from the
   roll.
3. **Critical effect** maps the score to `(immediate, perRound)`:
   - `immediate` extra damage is added to base damage this attack.
   - `perRound` (Slash, Puncture, Claw only) imposes an ongoing **Bleed** status
     on the target. Crush criticals are pure burst with no bleed.

### 5. Overdrive

If the attacker is an overdriven monster whose overdriven stat matches the
weapon's governing stat, the running damage (base + immediate critical) is
multiplied by **1.5**, rounded away from zero. See
[NPCs and Monsters: Overdrive](03-npcs-and-monsters.md#overdrive-exceptional-monsters).

### 6. Armor protection (damage reduction)

After overdrive, the engine applies a **derived** percent damage reduction based
on the defender's armor profile:

```
reduction   = round(rawDamage * protectionPercent / 100)
finalDamage = max(0, rawDamage - reduction)
```

> **Two armor models, on purpose.** Canonical BaB models armor as *accuracy*
> mitigation through the Base Attack Bonus table (heavier armor makes you harder
> to hit well). The percent reduction is an **engine-added overlay** for consumers
> that expect armor to soak damage. Both are reported in the result so you can use
> whichever your game wants. The protection percents are not from the manual.

### 7. State changes

`finalDamage` is subtracted from the target's `CurrentHits` (clamped at 0), the
change is persisted, and a `stateChanges` entry records before/after/delta. A
bleeding critical adds a Bleed status entry as well.

## The Base Attack Bonus table

Rows are weapon attack profiles; columns are the defender's armor profile. Values
are the accuracy modifier added to the attack roll. Negative numbers mean the
armor makes a clean hit harder.

| Profile | None | Robes | Soft | Rigid | Chain | Plate |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 1H Edge | 10 | 5 | 0 | 0 | -10 | -20 |
| 2H Edge | 4 | 10 | 10 | 0 | 0 | -10 |
| 1H Conc | 0 | 0 | 0 | 0 | -5 | 5 |
| 2H Conc | 0 | 0 | 0 | 0 | 0 | 10 |
| Thrown | 5 | 0 | 0 | 0 | 0 | -20 |
| Crossbow | 15 | 10 | 5 | 0 | 0 | -10 |
| Shortbow | 10 | 5 | 0 | 0 | 0 | -15 |
| Longbow | 20 | 15 | 5 | 0 | 0 | -5 |
| Fist | 0 | 0 | -5 | -5 | -10 | -15 |
| Claws | 10 | 5 | 0 | -5 | -15 | -25 |

Note the texture: edged and missile weapons are punished by heavy metal armor,
while concussion weapons (maces, hammers) actually do *better* against Plate.

### Armor columns

The defender's armor (the `armor` parameter) maps to a column:

| Armor input | Column | Protection overlay |
| --- | --- | ---: |
| (none / unknown / Helm / Shield) | None | 0% |
| Robes, Cloth, "Heavy Clothing/Robes" | Robes | 5% |
| Soft, Soft Leather | Soft | 10% |
| Rigid, Rigid Leather | Rigid | 15% |
| Chain, Scale | Chain | 25% |
| Plate | Plate | 40% |

Helm and Shield, used as the `armor` value, fall in the None *body* column for the
attack table but carry their own overlay percents (Helm 5%, Shield 10%) when looked
up directly. The `armor` parameter accepts an armor **id** (`soft-leather`),
**name** ("Soft Leather"), or bare **profile** ("Soft").

## The critical tables

### Critical type (weapon profile x armor column)

Each cell is the critical type and an accuracy-like modifier folded into the
critical score. For example a 1H Edge against None is "Slash+1"; against Plate it
is "Slash-4". Concussion weapons yield Crush, thrown/bows yield Puncture, claws
yield Claw, and the Fist yields nothing.

### Critical effect (critical score)

The critical score (clamped to the table range, roughly -3 and below up to +4 and
above) selects `(immediate, perRound)`:

- **Crush** is the hardest immediate hitter and never bleeds.
- **Slash**, **Puncture**, and **Claw** deal less burst but inflict ongoing Bleed
  at higher scores.

The exact per-step values live in `CombatTables.CriticalEffect`; the takeaway for
your game is that a higher critical score means more immediate damage and, for
bleeding types, a stronger Bleed.

## Weapons

| Action id | Weapon | Profile | Damage | Governing stat |
| --- | --- | --- | --- | --- |
| `physical-attack-1h-edge` | 1H Edge | 1H Edge | 1D6 | Strength |
| `physical-attack-2h-edge` | 2H Edge | 2H Edge | 2D6 | Strength |
| `physical-attack-1h-conc` | 1H Conc | 1H Conc | 1D6 | Strength |
| `physical-attack-2h-conc` | 2H Conc | 2H Conc | 2D6 | Strength |
| `physical-attack-thrown` | Thrown | Thrown | 1D4 | Agility |
| `physical-attack-crossbow` | Crossbow | Crossbow | 1D8 | Agility |
| `physical-attack-shortbow` | Shortbow | Shortbow | 1D6 | Agility |
| `physical-attack-longbow` | Longbow | Longbow | 1D10 | Agility |
| `physical-attack-fist` | Fist | Fist | 1D4 | Strength |
| `physical-attack-claws` | Claws | Claws | 1D6 | Agility |

"Edge" is bladed, "Conc" is concussion (blunt). The named items in the catalog
(Short Sword, Broadsword, Feline Claws) map onto these profiles; your game decides
which inventory item drives which attack action.

## A worked example

A Human Fighter (Strength 52, `Primary Melee` 5) attacks a defender in chain with
a 1H Edge, natural roll 80:

1. Accuracy: weaponSkill 5 + StatBonus(52)=1 + BaseAttack(1H Edge vs Chain)=-10
   gives accuracyTotal -4. Adjusted roll = 80 - 4 = 76.
2. Outcome: natural 80, total 76 is in the 75+ band: **Hit + Critical**.
3. Base damage: roll 1D6 (say 4), full because it is a plain Hit band.
4. Critical: type for 1H Edge vs Chain is "Slash-2"; score =
   attacker 1D4 + (-2) - defender 1D4; suppose the effect is (2, 2). Immediate +2,
   and a Bleed(2) status is applied.
5. No overdrive (the Fighter is not a monster).
6. Armor reduction: Chain overlay 25% of (4 + 2 = 6) is 2 (rounded), final = 4.
7. The target loses 4 hits now and starts bleeding 2 per round.

Pin `rollOverride: 80` and provide `damage` to make this fully reproducible. The
YAML result for an attack is dissected in
[Actions and Results](07-actions-and-results.md#physical-attack-result).

## Defending and conditions

Two related actions are not weapon attacks:

- `defend` (a system action): reports that a defensive posture grants +10 defense
  until the actor's next turn. The engine records the intent; your turn scheduler
  applies the bonus.
- `apply-condition` (a system action): imposes a status effect directly. See
  [Status Effects](06-status-effects.md).
