using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SharpNinja.FeatureFlags.Cqrs;

namespace BodyAndBrain.Engine;

public sealed class ActionExecutor(IGameDataCatalog catalog, IGameStore store, IDiceRoller dice, MagicNumbers magic)
{
    private readonly MagicNumbers _magic = magic;
    private readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .DisableAliases()
        .Build();

    public async Task<Result<YamlActionResult>> ExecuteAsync(ExecuteGameActionCommand command, CancellationToken ct)
    {
        var action = catalog.Data.Actions.SingleOrDefault(x => string.Equals(x.Id, command.ActionId, StringComparison.OrdinalIgnoreCase));
        if (action is null)
            return Result.Failure<YamlActionResult>($"Action '{command.ActionId}' was not found.");

        var actor = await LoadCombatantAsync(command.ActorId, ct).ConfigureAwait(false);
        if (actor is null)
            return Result.Failure<YamlActionResult>($"Actor '{command.ActorId}' was not found.");

        var target = command.TargetId is null ? null : await LoadCombatantAsync(command.TargetId, ct).ConfigureAwait(false);
        if (command.TargetId is not null && target is null)
            return Result.Failure<YamlActionResult>($"Target '{command.TargetId}' was not found.");

        var document = new Dictionary<string, object?>
        {
            ["actionId"] = action.Id,
            ["actionName"] = action.Name,
            ["kind"] = action.Kind,
            ["actor"] = Describe(actor),
            ["target"] = target is null ? null : Describe(target),
            ["inputs"] = command.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            ["rolls"] = new Dictionary<string, object?>(),
            ["modifiers"] = new List<object>(),
            ["outcome"] = new Dictionary<string, object?>(),
            ["stateChanges"] = new List<object>(),
            ["diagnostics"] = new List<object>(),
            ["requiresAdjudication"] = false,
        };

        var requiresAdjudication = false;
        string? reason = null;

        switch (action.Kind)
        {
            case "physicalAttack":
                await ExecutePhysicalAttackAsync(action, command, actor, target, document, ct).ConfigureAwait(false);
                break;
            case "spell":
                (requiresAdjudication, reason) = await ExecuteSpellAsync(action, command, actor, target, document, ct).ConfigureAwait(false);
                break;
            case "maneuver":
                ExecuteManeuver(command, document, _magic);
                break;
            case "system" when action.Id == "damage-target":
                if (target is null)
                    (requiresAdjudication, reason) = MarkAdjudication(document, "target-required", "Damage requires a target.");
                else
                    await ApplyHitChangeAsync(command.TargetId!, document, -Amount(command), ct).ConfigureAwait(false);
                break;
            case "system" when action.Id == "heal-target":
                if (target is null)
                    (requiresAdjudication, reason) = MarkAdjudication(document, "target-required", "Healing requires a target.");
                else
                    await ApplyHitChangeAsync(command.TargetId!, document, Amount(command), ct).ConfigureAwait(false);
                break;
            case "system" when action.Id == "defend":
                ((Dictionary<string, object?>)document["outcome"]!)["result"] = $"Defensive posture grants +{_magic.DefenseBonus} defense until the actor's next turn.";
                break;
            case "system" when action.Id == "apply-condition":
                await ApplyConditionAsync(command, actor, target, document, ct).ConfigureAwait(false);
                break;
            case "equipArmor":
                ((Dictionary<string, object?>)document["outcome"]!)["result"] = $"Equipped {action.Name.Replace("Equip ", "", StringComparison.Ordinal)}.";
                break;
            case "item" when action.Name.Contains("Torch", StringComparison.OrdinalIgnoreCase):
                ((Dictionary<string, object?>)document["outcome"]!)["result"] = "Torch is lit or readied.";
                break;
            default:
                (requiresAdjudication, reason) = MarkAdjudication(
                    document,
                    $"adjudication-{Slug.Normalize(action.Kind)}",
                    "The action is defined in the catalog but lacks deterministic execution detail in the canonical manual/workbook sources.");
                break;
        }

        document["requiresAdjudication"] = requiresAdjudication;
        if (reason is not null)
            document["adjudicationReason"] = reason;

        return await PersistResultAsync(action.Id, command.ActorId, command.TargetId, document, requiresAdjudication, reason, ct).ConfigureAwait(false);
    }

    /// <summary>FR-STATUS-001: applies one per-round tick of all active statuses on a target, resolving resistance rolls.</summary>
    public async Task<Result<YamlActionResult>> TickStatusesAsync(TickStatusEffectsCommand command, CancellationToken ct)
    {
        var target = await LoadCombatantAsync(command.TargetId, ct).ConfigureAwait(false);
        if (target is null)
            return Result.Failure<YamlActionResult>($"Target '{command.TargetId}' was not found.");

        var document = new Dictionary<string, object?>
        {
            ["actionId"] = "tick-status-effects",
            ["actionName"] = "Tick Status Effects",
            ["kind"] = "status",
            ["actor"] = Describe(target),
            ["target"] = Describe(target),
            ["inputs"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            ["rolls"] = new Dictionary<string, object?>(),
            ["modifiers"] = new List<object>(),
            ["outcome"] = new Dictionary<string, object?>(),
            ["stateChanges"] = new List<object>(),
            ["diagnostics"] = new List<object>(),
            ["requiresAdjudication"] = false,
        };

        var ticks = new List<object>();
        var totalDamage = 0;
        foreach (var status in target.Statuses.Where(s => s.Active).ToList())
        {
            var applied = 0;
            if (StatusEffects.IsDamaging(status.Type))
            {
                applied = Math.Max(0, status.Magnitude);
                totalDamage += applied;
            }

            var roll = command.RollOverride ?? dice.D100();
            var resistBonus = Mechanics.StatBonus(target.Stats.GetValueOrDefault(status.ResistanceStat)) + status.ResistanceBonus;
            var resistTotal = roll + resistBonus;
            var resisted = Mechanics.OutcomeHits(Mechanics.RollOutcome(roll, resistTotal));

            if (resisted)
            {
                status.Active = false;
            }
            else
            {
                status.RoundsRemaining--;
                if (status.RoundsRemaining <= 0)
                    status.Active = false;
            }

            ticks.Add(new
            {
                type = status.Type,
                applied,
                resistanceStat = status.ResistanceStat,
                resistanceRoll = roll,
                resistanceTotal = resistTotal,
                resisted,
                roundsRemaining = Math.Max(0, status.RoundsRemaining),
                active = status.Active,
            });
        }

        target.Statuses.RemoveAll(s => !s.Active);

        if (totalDamage > 0)
        {
            var before = target.CurrentHits;
            target.CurrentHits = Math.Clamp(target.CurrentHits - totalDamage, 0, target.MaxHits);
            AddHitChange(document, before, target.CurrentHits, target.MaxHits, -totalDamage);
        }

        await PersistCombatantAsync(target, ct).ConfigureAwait(false);

        ((Dictionary<string, object?>)document["outcome"]!)["result"] = ticks.Count == 0 ? "No active statuses." : "Status effects ticked.";
        ((Dictionary<string, object?>)document["outcome"]!)["statusTicks"] = ticks;
        document["status"] = new { active = target.Statuses.Select(s => new { s.Type, s.Magnitude, s.RoundsRemaining }).ToList() };

        return await PersistResultAsync("tick-status-effects", command.TargetId, command.TargetId, document, false, null, ct).ConfigureAwait(false);
    }

    private async Task ExecutePhysicalAttackAsync(
        ActionDefinition action,
        ExecuteGameActionCommand command,
        Combatant actor,
        Combatant? target,
        Dictionary<string, object?> document,
        CancellationToken ct)
    {
        var weapon = catalog.Data.Weapons.Single(x => string.Equals(x.Id, action.ReferenceId, StringComparison.OrdinalIgnoreCase));
        var (armorProfile, protectionPercent) = ResolveArmor(command);
        var armorColumn = CombatTables.ArmorColumnIndex(armorProfile);

        var weaponSkillBonus = WeaponSkillBonus(actor, weapon, command);
        var governingStatBonus = Mechanics.StatBonus(actor.Stats.GetValueOrDefault(weapon.GoverningStat));
        var baseAttackBonus = CombatTables.BaseAttackBonus(weapon.AttackProfile, armorColumn);
        var accuracyTotal = weaponSkillBonus + governingStatBonus + baseAttackBonus;

        var roll = command.RollOverride ?? dice.D100();
        var adjustedRoll = roll + accuracyTotal;
        var outcome = Mechanics.RollOutcome(roll, adjustedRoll);

        var baseDamage = Mechanics.OutcomeHits(outcome) ? Damage(command, weapon) : 0;
        if (Mechanics.OutcomeIsHalf(outcome))
            baseDamage /= 2;
        else if (Mechanics.OutcomeIsDouble(outcome))
            baseDamage *= 2;

        // Critical resolution per the BaB Slash/Crush/Puncture/Claw tables.
        object? criticalBlock = null;
        var critImmediate = 0;
        var critPerRound = 0;
        if (Mechanics.OutcomeIsCritical(outcome))
        {
            var crit = CombatTables.CriticalType(weapon.AttackProfile, armorColumn);
            if (crit is { } c)
            {
                var attackerDie = CritDie(command, roll, attacker: true);
                var defenderDie = CritDie(command, roll, attacker: false);
                var critScore = attackerDie + c.Modifier - defenderDie;
                (critImmediate, critPerRound) = CombatTables.CriticalEffect(c.Type, critScore);
                criticalBlock = new
                {
                    type = c.Type,
                    modifier = c.Modifier,
                    attackerDie,
                    defenderDie,
                    critScore,
                    immediate = critImmediate,
                    perRound = critPerRound,
                };
            }
        }

        var rawDamage = baseDamage + critImmediate;

        // FR-OVERDRIVE-001: monster overdrive multiplies the governed-stat effect (damage) by 1.5.
        var overdriveApplied = actor.IsOverdrivenFor(weapon.GoverningStat);
        if (overdriveApplied)
            rawDamage = (int)Math.Round(rawDamage * 1.5, MidpointRounding.AwayFromZero);

        var reduction = (int)Math.Round(rawDamage * protectionPercent / 100.0, MidpointRounding.AwayFromZero);
        var finalDamage = Math.Max(0, rawDamage - reduction);

        ((Dictionary<string, object?>)document["rolls"]!)["d100"] = roll;
        ((List<object>)document["modifiers"]!).Add(new
        {
            source = weapon.Name,
            weapon.GoverningStat,
            weapon.Damage,
            weapon.Critical,
            weapon.AttackProfile,
        });

        var accuracyMitigation = CombatTables.BaseAttackBonus(weapon.AttackProfile, 0) - baseAttackBonus;
        var outcomeMap = (Dictionary<string, object?>)document["outcome"]!;
        outcomeMap["result"] = outcome;
        outcomeMap["weapon"] = weapon.Name;
        outcomeMap["damage"] = finalDamage;
        outcomeMap["accuracy"] = new
        {
            roll,
            weaponSkillBonus,
            governingStatBonus,
            baseAttackBonus,
            total = accuracyTotal,
            adjustedRoll,
        };
        outcomeMap["critical"] = criticalBlock;
        outcomeMap["protection"] = new
        {
            armorProfile,
            accuracyMitigation,
            protectionPercent,
            damageReduced = reduction,
            model = "canonical-accuracy+derived-percent",
        };
        if (overdriveApplied)
            outcomeMap["overdrive"] = new { applied = true, stat = actor.OverdrivenStat, multiplier = 1.5 };

        if (finalDamage > 0 && command.TargetId is not null)
            await ApplyHitChangeAsync(command.TargetId, document, -finalDamage, ct).ConfigureAwait(false);

        // A bleeding critical imposes an ongoing Bleed status on the target.
        if (critPerRound > 0 && target is not null && command.TargetId is not null)
            await ApplyStatusAsync(command.TargetId, "Bleed", critPerRound, StatusEffects.DefaultRounds("Bleed"), document, ct).ConfigureAwait(false);
    }

    private async Task<(bool requiresAdjudication, string? reason)> ExecuteSpellAsync(
        ActionDefinition action,
        ExecuteGameActionCommand command,
        Combatant actor,
        Combatant? target,
        Dictionary<string, object?> document,
        CancellationToken ct)
    {
        var spell = catalog.Data.Spells.Single(x => string.Equals(x.Id, action.ReferenceId, StringComparison.OrdinalIgnoreCase));
        var resolution = SpellResolver.Resolve(spell, actor.Level, dice);

        var listStat = catalog.Data.SpellLists
            .FirstOrDefault(x => string.Equals(x.Name, spell.List, StringComparison.OrdinalIgnoreCase))?.Stat;
        var overdriveApplied = listStat is not null && actor.IsOverdrivenFor(listStat);

        ((List<object>)document["modifiers"]!).Add(new
        {
            source = spell.Name,
            spell.List,
            spell.Type,
            spell.Modifier,
            spell.Duration,
        });
        var outcomeMap = (Dictionary<string, object?>)document["outcome"]!;
        outcomeMap["spell"] = spell.Name;

        int Scale(int amount) => overdriveApplied
            ? (int)Math.Round(amount * 1.5, MidpointRounding.AwayFromZero)
            : amount;

        switch (resolution.Kind)
        {
            case SpellResolutionKind.Damage:
            {
                var amount = Scale(resolution.Amount);
                outcomeMap["result"] = command.TargetId is null ? "Spell resolved (no target supplied)." : "Spell damage applied.";
                outcomeMap["amount"] = amount;
                if (overdriveApplied) outcomeMap["overdrive"] = new { applied = true, stat = actor.OverdrivenStat, multiplier = 1.5 };
                if (amount > 0 && command.TargetId is not null)
                    await ApplyHitChangeAsync(command.TargetId, document, -amount, ct).ConfigureAwait(false);
                return (false, null);
            }
            case SpellResolutionKind.Heal:
            {
                var amount = Scale(resolution.Amount);
                var healTargetId = resolution.TargetIsSelf || command.TargetId is null ? command.ActorId : command.TargetId;
                outcomeMap["result"] = "Spell healing applied.";
                outcomeMap["amount"] = amount;
                if (amount > 0)
                    await ApplyHitChangeAsync(healTargetId, document, amount, ct).ConfigureAwait(false);
                return (false, null);
            }
            case SpellResolutionKind.LifeDrain:
            {
                var amount = Scale(resolution.Amount);
                outcomeMap["result"] = command.TargetId is null ? "Life drain resolved (no target supplied)." : "Life drain applied.";
                outcomeMap["amount"] = amount;
                if (amount > 0 && command.TargetId is not null)
                {
                    await ApplyHitChangeAsync(command.TargetId, document, -amount, ct).ConfigureAwait(false);
                    await ApplyHitChangeAsync(command.ActorId, document, amount / 2, ct).ConfigureAwait(false);
                }
                return (false, null);
            }
            case SpellResolutionKind.ApplyStatus:
            {
                var statusTargetId = command.TargetId ?? command.ActorId;
                var magnitude = resolution.Amount > 0 ? resolution.Amount : StatusEffects.DefaultMagnitude(resolution.StatusType!);
                outcomeMap["result"] = $"{resolution.StatusType} status applied.";
                await ApplyStatusAsync(statusTargetId, resolution.StatusType!, magnitude, StatusEffects.DefaultRounds(resolution.StatusType!), document, ct).ConfigureAwait(false);
                return (false, null);
            }
            case SpellResolutionKind.Cleanse:
            {
                var cleanseTargetId = resolution.TargetIsSelf || command.TargetId is null ? command.ActorId : command.TargetId;
                var removed = await CleanseStatusAsync(cleanseTargetId, resolution.StatusType!, document, ct).ConfigureAwait(false);
                outcomeMap["result"] = removed > 0 ? $"Cleared {removed} {resolution.StatusType} status(es)." : $"No {resolution.StatusType} status to clear.";
                return (false, null);
            }
            case SpellResolutionKind.Effect:
            {
                outcomeMap["result"] = resolution.EffectText ?? "Spell effect resolved.";
                if (resolution.Amount != 0) outcomeMap["amount"] = Scale(resolution.Amount);
                return (false, null);
            }
            default:
                return MarkAdjudication(document, resolution.Reason ?? "adjudication-spell",
                    "The spell is defined in the catalog but its mechanics are underspecified in the canonical sources.");
        }
    }

    private async Task ApplyConditionAsync(
        ExecuteGameActionCommand command,
        Combatant actor,
        Combatant? target,
        Dictionary<string, object?> document,
        CancellationToken ct)
    {
        var condition = command.Parameters?.GetValueOrDefault("condition") ?? "unspecified";
        var outcomeMap = (Dictionary<string, object?>)document["outcome"]!;

        if (!StatusEffects.IsCanonical(condition))
        {
            ((List<object>)document["stateChanges"]!).Add(new { type = "condition", condition });
            outcomeMap["result"] = "Condition noted.";
            return;
        }

        var type = StatusEffects.Normalize(condition);
        var magnitude = TryInt(command, "magnitude") ?? StatusEffects.DefaultMagnitude(type);
        var rounds = TryInt(command, "duration") ?? StatusEffects.DefaultRounds(type);
        var statusTargetId = command.TargetId ?? command.ActorId;
        await ApplyStatusAsync(statusTargetId, type, magnitude, rounds, document, ct).ConfigureAwait(false);
        outcomeMap["result"] = $"{type} status applied.";
    }

    private static void ExecuteManeuver(ExecuteGameActionCommand command, Dictionary<string, object?> document, MagicNumbers magic)
    {
        var roll = command.RollOverride ?? magic.ManeuverDefaultRoll;
        var outcome = Mechanics.RollOutcome(roll);
        ((Dictionary<string, object?>)document["rolls"]!)["d100"] = roll;
        ((Dictionary<string, object?>)document["outcome"]!)["result"] = outcome;
        ((Dictionary<string, object?>)document["outcome"]!)["effect"] = Mechanics.OutcomeHits(outcome)
            ? "Maneuver succeeds and creates the declared positional or tactical effect."
            : "Maneuver does not create a useful effect.";
    }

    private async Task ApplyStatusAsync(string targetId, string type, int magnitude, int rounds, Dictionary<string, object?> document, CancellationToken ct)
    {
        var combatant = await LoadCombatantAsync(targetId, ct).ConfigureAwait(false);
        if (combatant is null)
            return;

        var resistanceStat = StatusEffects.ResistanceStat(type);
        combatant.Statuses.Add(new StatusEffectState
        {
            Type = type,
            Magnitude = Math.Max(0, magnitude),
            RoundsRemaining = Math.Max(1, rounds),
            ResistanceStat = resistanceStat,
            ResistanceBonus = 0,
            Active = true,
        });
        await PersistCombatantAsync(combatant, ct).ConfigureAwait(false);

        ((List<object>)document["stateChanges"]!).Add(new
        {
            type = "status",
            status = type,
            magnitude = Math.Max(0, magnitude),
            roundsRemaining = Math.Max(1, rounds),
            resistanceStat,
        });
        document["status"] = new
        {
            active = combatant.Statuses.Select(s => new { s.Type, s.Magnitude, s.RoundsRemaining, s.ResistanceStat }).ToList(),
        };
    }

    private async Task<int> CleanseStatusAsync(string targetId, string type, Dictionary<string, object?> document, CancellationToken ct)
    {
        var combatant = await LoadCombatantAsync(targetId, ct).ConfigureAwait(false);
        if (combatant is null)
            return 0;

        var removed = combatant.Statuses.RemoveAll(s => string.Equals(s.Type, type, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            await PersistCombatantAsync(combatant, ct).ConfigureAwait(false);
            ((List<object>)document["stateChanges"]!).Add(new { type = "status-cleared", status = type, count = removed });
        }

        return removed;
    }

    private async Task ApplyHitChangeAsync(string targetId, Dictionary<string, object?> document, int delta, CancellationToken ct)
    {
        var combatant = await LoadCombatantAsync(targetId, ct).ConfigureAwait(false);
        if (combatant is null)
            return;

        var before = combatant.CurrentHits;
        combatant.CurrentHits = Math.Clamp(combatant.CurrentHits + delta, 0, combatant.MaxHits);
        await PersistCombatantAsync(combatant, ct).ConfigureAwait(false);
        AddHitChange(document, before, combatant.CurrentHits, combatant.MaxHits, delta);
    }

    private async Task<Combatant?> LoadCombatantAsync(string id, CancellationToken ct)
    {
        var player = await store.GetPlayerAsync(id, ct).ConfigureAwait(false);
        if (player is not null)
            return new Combatant(player);

        var npc = await store.GetNpcAsync(id, ct).ConfigureAwait(false);
        return npc is null ? null : new Combatant(npc);
    }

    private async Task PersistCombatantAsync(Combatant combatant, CancellationToken ct)
    {
        if (combatant.Pc is not null)
            await store.UpsertPlayerAsync(combatant.Pc, ct).ConfigureAwait(false);
        else
            await store.UpsertNpcAsync(combatant.Npc!, ct).ConfigureAwait(false);
    }

    private (string Profile, int ProtectionPercent) ResolveArmor(ExecuteGameActionCommand command)
    {
        var value = command.Parameters?.GetValueOrDefault("armor");
        if (string.IsNullOrWhiteSpace(value))
            return ("None", 0);

        var armor = catalog.Data.Armors.FirstOrDefault(x =>
            string.Equals(x.Id, value, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Id, Slug.Normalize(value), StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Name, value, StringComparison.OrdinalIgnoreCase));

        var profile = armor?.Profile ?? value;
        var percent = armor?.ProtectionPercent ?? CombatTables.ProtectionPercent(profile);
        return (profile, percent);
    }

    private static int WeaponSkillBonus(Combatant actor, WeaponDefinition weapon, ExecuteGameActionCommand command)
    {
        var explicitSkill = TryInt(command, "weaponSkill");
        if (explicitSkill is not null)
            return explicitSkill.Value;

        var skillName = weapon.AttackProfile.Contains("bow", StringComparison.OrdinalIgnoreCase)
            || weapon.AttackProfile.Contains("Thrown", StringComparison.OrdinalIgnoreCase)
                ? "Primary Missile"
                : "Primary Melee";

        if (actor.Skills is not null && actor.Skills.TryGetValue(skillName, out var skill))
            return skill;

        // Monsters and unclassed NPCs carry a signature skill rating (e.g. "Primary Melee 5").
        if (actor.Npc is not null)
        {
            var digits = new string(actor.Npc.Signature.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var rating))
                return rating;
        }

        return 0;
    }

    private int CritDie(ExecuteGameActionCommand command, int roll, bool attacker)
    {
        if (command.RollOverride is null)
            return dice.Roll("1D4");

        // Deterministic crit dice seeded from the override so the same input yields the same crit.
        var seed = attacker ? roll : roll + 2;
        return (seed % 4 + 4) % 4 + 1;
    }

    private async Task<Result<YamlActionResult>> PersistResultAsync(
        string actionId,
        string actorId,
        string? targetId,
        Dictionary<string, object?> document,
        bool requiresAdjudication,
        string? reason,
        CancellationToken ct)
    {
        var yaml = _serializer.Serialize(document);
        await store.InsertActionLogAsync(new ActionLogRecord
        {
            Id = Guid.NewGuid().ToString("n"),
            ActionId = actionId,
            ActorId = actorId,
            TargetId = targetId,
            Timestamp = DateTimeOffset.UtcNow,
            YamlDocument = yaml,
        }, ct).ConfigureAwait(false);

        return Result.Success(new YamlActionResult
        {
            ActionId = actionId,
            Document = yaml,
            RequiresAdjudication = requiresAdjudication,
            AdjudicationReason = reason,
        });
    }

    private static Dictionary<string, object?> Describe(Combatant combatant)
        => new()
        {
            ["id"] = combatant.Id,
            ["name"] = combatant.Name,
            ["type"] = combatant.Pc is not null ? "pc" : combatant.Npc!.IsMonster ? "monster" : "npc",
            ["race"] = combatant.Race,
            ["profession"] = combatant.Profession,
            ["level"] = combatant.Level,
            ["hits"] = $"{combatant.CurrentHits}/{combatant.MaxHits}",
        };

    private static void AddHitChange(Dictionary<string, object?> document, int before, int after, int maximum, int delta)
    {
        ((List<object>)document["stateChanges"]!).Add(new
        {
            type = "hits",
            before,
            after,
            maximum,
            delta,
        });
        var outcomeMap = (Dictionary<string, object?>)document["outcome"]!;
        if (!outcomeMap.ContainsKey("result"))
            outcomeMap["result"] = delta < 0 ? "Damage applied." : "Healing applied.";
    }

    private static (bool requiresAdjudication, string reason) MarkAdjudication(
        Dictionary<string, object?> document,
        string reason,
        string message)
    {
        ((List<object>)document["diagnostics"]!).Add(new
        {
            code = reason,
            message,
        });
        ((Dictionary<string, object?>)document["outcome"]!)["result"] = "GM adjudication required.";
        return (true, reason);
    }

    private static int Amount(ExecuteGameActionCommand command)
        => TryInt(command, "amount") is { } amount ? Math.Max(0, amount) : 1;

    private static int? TryInt(ExecuteGameActionCommand command, string key)
        => command.Parameters is not null
           && command.Parameters.TryGetValue(key, out var text)
           && int.TryParse(text, out var value)
            ? value
            : null;

    private int Damage(ExecuteGameActionCommand command, WeaponDefinition weapon)
    {
        if (TryInt(command, "damage") is { } damage)
            return Math.Max(0, damage);

        var parsed = dice.Roll(weapon.Damage);
        return parsed > 0 ? parsed : 1;
    }

    /// <summary>Wraps either a player or NPC record so combat resolution can read stats and persist uniformly.</summary>
    private sealed class Combatant
    {
        public Combatant(PlayerCharacterRecord pc) => Pc = pc;
        public Combatant(NpcRecord npc) => Npc = npc;

        public PlayerCharacterRecord? Pc { get; }
        public NpcRecord? Npc { get; }

        public string Id => Pc?.Id ?? Npc!.Id;
        public string Name => Pc?.Name ?? Npc!.Name;
        public string Race => Pc?.Race ?? Npc!.Race;
        public string Profession => Pc?.Profession ?? Npc!.Profession;
        public int Level => Pc?.Level ?? Npc!.Level;
        public Dictionary<string, int> Stats => Pc?.Stats ?? Npc!.Stats;
        public Dictionary<string, int>? Skills => Pc?.Skills;
        public List<StatusEffectState> Statuses => Pc?.Statuses ?? Npc!.Statuses;
        public string? OverdrivenStat => Npc?.OverdrivenStat;

        public int CurrentHits
        {
            get => Pc?.CurrentHits ?? Npc!.CurrentHits;
            set { if (Pc is not null) Pc.CurrentHits = value; else Npc!.CurrentHits = value; }
        }

        public int MaxHits => Pc?.MaxHits ?? Npc!.MaxHits;

        public bool IsOverdrivenFor(string stat)
            => !string.IsNullOrWhiteSpace(OverdrivenStat)
               && string.Equals(OverdrivenStat, stat, StringComparison.OrdinalIgnoreCase);
    }
}
