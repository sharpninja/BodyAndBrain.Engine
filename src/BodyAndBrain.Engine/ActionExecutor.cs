using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SharpNinja.FeatureFlags.Cqrs;

namespace BodyAndBrain.Engine;

public sealed class ActionExecutor(IGameDataCatalog catalog, IGameStore store, IDiceRoller dice)
{
    private readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .DisableAliases()
        .Build();

    public async Task<Result<YamlActionResult>> ExecuteAsync(ExecuteGameActionCommand command, CancellationToken ct)
    {
        var action = catalog.Data.Actions.SingleOrDefault(x => string.Equals(x.Id, command.ActionId, StringComparison.OrdinalIgnoreCase));
        if (action is null)
            return Result.Failure<YamlActionResult>($"Action '{command.ActionId}' was not found.");

        var actor = await LoadActorAsync(command.ActorId, ct).ConfigureAwait(false);
        if (actor is null)
            return Result.Failure<YamlActionResult>($"Actor '{command.ActorId}' was not found.");

        var target = command.TargetId is null ? null : await LoadActorAsync(command.TargetId, ct).ConfigureAwait(false);
        if (command.TargetId is not null && target is null)
            return Result.Failure<YamlActionResult>($"Target '{command.TargetId}' was not found.");

        var document = new Dictionary<string, object?>
        {
            ["actionId"] = action.Id,
            ["actionName"] = action.Name,
            ["kind"] = action.Kind,
            ["actor"] = actor,
            ["target"] = target,
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
                var damage = ExecutePhysicalAttack(action, command, document);
                if (damage > 0 && command.TargetId is not null)
                    await ApplyHitChangeAsync(command.TargetId, document, -damage, ct).ConfigureAwait(false);
                break;
            case "maneuver":
                ExecuteManeuver(command, document);
                break;
            case "system" when action.Id == "damage-target":
                if (target is null)
                {
                    (requiresAdjudication, reason) = MarkAdjudication(document, "target-required", "Damage requires a target.");
                }
                else
                {
                    await ApplyHitChangeAsync(command.TargetId!, document, -Amount(command), ct).ConfigureAwait(false);
                }
                break;
            case "system" when action.Id == "heal-target":
                if (target is null)
                {
                    (requiresAdjudication, reason) = MarkAdjudication(document, "target-required", "Healing requires a target.");
                }
                else
                {
                    await ApplyHitChangeAsync(command.TargetId!, document, Amount(command), ct).ConfigureAwait(false);
                }
                break;
            case "system" when action.Id == "defend":
                ((Dictionary<string, object?>)document["outcome"]!)["result"] = "Defensive posture grants +10 defense until the actor's next turn.";
                break;
            case "system" when action.Id == "apply-condition":
                ((List<object>)document["stateChanges"]!).Add(new
                {
                    type = "condition",
                    condition = command.Parameters?.GetValueOrDefault("condition") ?? "unspecified",
                });
                ((Dictionary<string, object?>)document["outcome"]!)["result"] = "Condition noted.";
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

        var yaml = _serializer.Serialize(document);
        await store.InsertActionLogAsync(new ActionLogRecord
        {
            Id = Guid.NewGuid().ToString("n"),
            ActionId = action.Id,
            ActorId = command.ActorId,
            TargetId = command.TargetId,
            Timestamp = DateTimeOffset.UtcNow,
            YamlDocument = yaml,
        }, ct).ConfigureAwait(false);

        return Result.Success(new YamlActionResult
        {
            ActionId = action.Id,
            Document = yaml,
            RequiresAdjudication = requiresAdjudication,
            AdjudicationReason = reason,
        });
    }

    private int ExecutePhysicalAttack(ActionDefinition action, ExecuteGameActionCommand command, Dictionary<string, object?> document)
    {
        var weapon = catalog.Data.Weapons.Single(x => string.Equals(x.Id, action.ReferenceId, StringComparison.OrdinalIgnoreCase));
        var roll = command.RollOverride ?? dice.D100();
        var outcome = Mechanics.RollOutcome(roll);
        var damage = Mechanics.OutcomeHits(outcome) ? Damage(command, weapon) : 0;

        ((Dictionary<string, object?>)document["rolls"]!)["d100"] = roll;
        ((List<object>)document["modifiers"]!).Add(new
        {
            source = weapon.Name,
            weapon.GoverningStat,
            weapon.Damage,
            weapon.Critical,
        });
        ((Dictionary<string, object?>)document["outcome"]!)["result"] = outcome;
        ((Dictionary<string, object?>)document["outcome"]!)["weapon"] = weapon.Name;
        ((Dictionary<string, object?>)document["outcome"]!)["damage"] = damage;
        return damage;
    }

    private static void ExecuteManeuver(ExecuteGameActionCommand command, Dictionary<string, object?> document)
    {
        var roll = command.RollOverride ?? 50;
        var outcome = Mechanics.RollOutcome(roll);
        ((Dictionary<string, object?>)document["rolls"]!)["d100"] = roll;
        ((Dictionary<string, object?>)document["outcome"]!)["result"] = outcome;
        ((Dictionary<string, object?>)document["outcome"]!)["effect"] = Mechanics.OutcomeHits(outcome)
            ? "Maneuver succeeds and creates the declared positional or tactical effect."
            : "Maneuver does not create a useful effect.";
    }

    private async Task ApplyHitChangeAsync(string targetId, Dictionary<string, object?> document, int delta, CancellationToken ct)
    {
        var player = await store.GetPlayerAsync(targetId, ct).ConfigureAwait(false);
        if (player is not null)
        {
            var before = player.CurrentHits;
            player.CurrentHits = Math.Clamp(player.CurrentHits + delta, 0, player.MaxHits);
            await store.UpsertPlayerAsync(player, ct).ConfigureAwait(false);
            AddHitChange(document, before, player.CurrentHits, player.MaxHits, delta);
            return;
        }

        var npc = await store.GetNpcAsync(targetId, ct).ConfigureAwait(false);
        if (npc is null)
            return;

        var npcBefore = npc.CurrentHits;
        npc.CurrentHits = Math.Clamp(npc.CurrentHits + delta, 0, npc.MaxHits);
        await store.UpsertNpcAsync(npc, ct).ConfigureAwait(false);
        AddHitChange(document, npcBefore, npc.CurrentHits, npc.MaxHits, delta);
    }

    private async Task<Dictionary<string, object?>?> LoadActorAsync(string id, CancellationToken ct)
    {
        var player = await store.GetPlayerAsync(id, ct).ConfigureAwait(false);
        if (player is not null)
        {
            return new Dictionary<string, object?>
            {
                ["id"] = player.Id,
                ["name"] = player.Name,
                ["type"] = "pc",
                ["race"] = player.Race,
                ["profession"] = player.Profession,
                ["level"] = player.Level,
                ["hits"] = $"{player.CurrentHits}/{player.MaxHits}",
            };
        }

        var npc = await store.GetNpcAsync(id, ct).ConfigureAwait(false);
        if (npc is null)
            return null;

        return new Dictionary<string, object?>
        {
            ["id"] = npc.Id,
            ["name"] = npc.Name,
            ["type"] = npc.IsMonster ? "monster" : "npc",
            ["race"] = npc.Race,
            ["profession"] = npc.Profession,
            ["level"] = npc.Level,
            ["hits"] = $"{npc.CurrentHits}/{npc.MaxHits}",
        };
    }

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
        ((Dictionary<string, object?>)document["outcome"]!)["result"] = delta < 0 ? "Damage applied." : "Healing applied.";
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
        => command.Parameters is not null
           && command.Parameters.TryGetValue("amount", out var text)
           && int.TryParse(text, out var amount)
            ? Math.Max(0, amount)
            : 1;

    private int Damage(ExecuteGameActionCommand command, WeaponDefinition weapon)
    {
        if (command.Parameters is not null
            && command.Parameters.TryGetValue("damage", out var text)
            && int.TryParse(text, out var damage))
        {
            return Math.Max(0, damage);
        }

        var parsed = dice.Roll(weapon.Damage);
        return parsed > 0 ? parsed : 1;
    }
}
