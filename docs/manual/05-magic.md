# Magic

The engine ships 70 spells across 10 spell lists. When you execute a spell action,
the engine **classifies** the spell into a deterministic resolution and applies it,
or flags the spell for adjudication when the source material does not pin down its
mechanics.

## Casting a spell

```csharp
var result = await engine.ExecuteActionAsync(
    actionId: "cast-spell-body-bolts",   // the spell action
    actorId:  caster.Id,
    targetId: enemy.Id);                  // may be null for self/area/no-target spells
```

Every spell has exactly one action whose id is `cast-spell-<list>-<spell>`. The
action's `ReferenceId` points at the spell definition. The caster's **level**
drives magnitude scaling; there is no separate spell-attack roll in the current
resolution model (damage and effect magnitudes come from the spell's modifier,
scaled by level).

## Spell lists

Each list has a casting discipline, a governing stat, and equipment restrictions.

| List | Discipline | Stat | Restrictions |
| --- | --- | --- | --- |
| Body | Wizardry | Intelligence | No Armor/Helms/Shields, Staff |
| Mind | Wizardry | Presence | No Armor/Helms/Shields, Staff |
| Summoning | Conjuring | Intelligence | No Metal/Helms/Shields, Totem |
| Destroy | Conjuring | Presence | No Metal/Helms/Shields, Totem |
| Blessings | Prayers | Piety | No Helms, Sigil |
| Curses | Prayers | Piety | No Helms, Sigil |
| Leadership | Paladin | Piety | Sigil |
| Naturalist | Ranger | Intelligence | No Metal/Helms/Shields, Totem |
| Songs | Bard | Presence | No Helms/Shields, Instrument |
| Necromancer | Conjuring | Intelligence | No Metal/Helms/Shields, Totem, Necromancy Apprenticeship |

The **stat** is what the engine reads for overdrive (below). The restrictions are
descriptive: the engine records them but does not block a cast if a caster is
wearing the wrong gear; enforce equipment rules in your game if you want them.

## Resolution kinds

The spell resolver maps each spell to one of these outcomes:

| Kind | What the engine does |
| --- | --- |
| **Damage** | Rolls the level-scaled modifier; subtracts it from the target's hits |
| **Heal** | Rolls the modifier; adds hits to the target (or the caster for Self spells) |
| **LifeDrain** | Damages the target; heals the caster for half the damage dealt |
| **ApplyStatus** | Imposes a status (Bleed / Poison / Disease) on the target |
| **Cleanse** | Removes a status category from the target (or caster) |
| **Effect** | A deterministic non-mutating buff/debuff: reports the effect text and any amount |
| **Adjudicate** | Hands the spell to your game; see below |

### How classification works

The resolver decides as follows (first match wins):

1. **Named adjudicated spells** (summoning, control, teleport, instant-kill,
   revival, and similar) resolve to **Adjudicate**.
2. **Life Drain** resolves to LifeDrain.
3. **Cleanse spells** (Stop Bleeding, Stop Poison, Antidote, Stop Disease, Cure,
   Remove Curse) remove their mapped status.
4. **Status spells** (Cause Bleeding -> Bleed, Infection -> Disease, Nauseate ->
   Poison) apply their status.
5. **Heal spells** (Heal, Restore, Bandage, or any whose description mentions
   healing) restore hits.
6. **Damage spells** (a dice modifier plus damage wording such as "damage",
   "bolt", "blast", "crushed"; also Turn Undead) deal damage.
7. **Everything else with a description** is a deterministic **Effect** (buffs and
   debuffs such as Hasten, Bless, Focus, Confuse, Weaken, songs, Sleep, Fear).
8. Anything left over resolves to **Adjudicate**.

This means most of the catalog resolves deterministically; only the genuinely
underspecified spells defer.

## Healing, damage, and life drain

- **Damage** and **Heal** amounts come from `RollScaledModifier(spell.Modifier,
  casterLevel, dice)`. A `1D6 per 5 Levels` heal cast at level 10 rolls 1D6 twice.
- A **Self** heal (or a heal with no target) heals the caster.
- **Life Drain** subtracts the rolled amount from the target and adds half (integer
  division) to the caster, in one action.

## Status and cleanse spells

Status spells apply a [status effect](06-status-effects.md) with a magnitude from
the scaled modifier (or the status default if the modifier is empty) and the
status's default duration. Cleanse spells remove every matching status from the
target. If there is nothing to clear, the result says so; it is still a success.

## Effect spells

Buffs and debuffs that the manual describes but does not reduce to a hit/heal
number resolve as **Effect**. The engine reports the spell's description as the
outcome and includes the scaled amount when the modifier is numeric. It does
**not** mutate stats or hits; your game applies the narrative effect (for example
"+15 to all actions" from Hasten) through your own buff system. These are
deterministic and never require adjudication.

## Adjudicated spells

The following spells resolve to **Adjudicate**: the engine returns success with
`requiresAdjudication: true` and `adjudicationReason: adjudication-spell`, leaving
the mechanic to your game or a human game master.

| Category | Spells |
| --- | --- |
| Summoning | Hawk, Dog, Cougar, Wolf, Bear, Pegasus, Golem, Dragon |
| Necromancer | Grave Sense, Death Whisper, Bone Servant, Command Undead, Animate Dead, Bind Spirit, Lichdom |
| Mind / utility / control | Read Mind, Mind Control, Meditate, Displace, Break Bonds, Disintegration, Revive Dead, Discover, Reveal, Unseen, Sure Shot, Track |

Why defer these? Conjuring a creature, controlling a mind, teleporting, revival,
and instant-kill all need rules the BaB sources leave open (what is summoned, how
long control lasts, where a target lands). Rather than invent numbers, the engine
marks the boundary. Handle it by:

- prompting a game master,
- routing to your own summon/teleport/mind-control subsystem, or
- applying a house rule.

See [Actions and Results: Adjudication](07-actions-and-results.md#adjudication).

## Overdrive

If the caster is an overdriven monster whose overdriven stat equals the spell
list's stat, the resolved **amount** (damage, heal, drain, or numeric effect) is
multiplied by **1.5**, rounded away from zero, and an `overdrive` block appears in
the result. For example a Lich (overdriven Intelligence) casting from an
Intelligence-stat list hits 1.5x as hard. Adjudicated spells are unaffected (there
is no amount to scale). See
[NPCs and Monsters: Overdrive](03-npcs-and-monsters.md#overdrive-exceptional-monsters).

## Spell catalog

All 70 spells, with list, level, type, range, modifier, and resolution kind, are
in the [Catalog Reference](09-catalog-reference.md#spells). A few representative
spells:

| Action id | Spell | List | Lvl | Resolves as |
| --- | --- | --- | ---: | --- |
| `cast-spell-body-bolts` | Bolts | Body | 3 | Damage (1D10) |
| `cast-spell-body-crush` | Crush | Body | 9 | Damage (2D10) |
| `cast-spell-blessings-heal` | Heal | Blessings | 7 | Heal (1D6 per 5 Levels) |
| `cast-spell-blessings-turn-undead` | Turn Undead | Blessings | 3 | Damage (1D6 per 5 Levels) |
| `cast-spell-blessings-stop-bleeding` | Stop Bleeding | Blessings | 1 | Cleanse (Bleed) |
| `cast-spell-curses-cause-bleeding` | Cause Bleeding | Curses | 6 | ApplyStatus (Bleed) |
| `cast-spell-curses-infection` | Infection | Curses | 30 | ApplyStatus (Disease) |
| `cast-spell-body-nauseate` | Nauseate | Body | 1 | ApplyStatus (Poison) |
| `cast-spell-necromancer-life-drain` | Life Drain | Necromancer | 9 | LifeDrain (1D8 per 5 Levels) |
| `cast-spell-body-hasten` | Hasten | Body | 5 | Effect ("+15 to all actions") |
| `cast-spell-necromancer-animate-dead` | Animate Dead | Necromancer | 12 | Adjudicate |
