namespace BodyAndBrain.Engine;

public enum SpellResolutionKind
{
    Damage,
    Heal,
    LifeDrain,
    ApplyStatus,
    Cleanse,
    Effect,
    Adjudicate,
}

/// <summary>Deterministic resolution plan for a catalog spell, derived from its BaB definition.</summary>
public sealed record SpellResolution(
    SpellResolutionKind Kind,
    int Amount = 0,
    string? StatusType = null,
    string? EffectText = null,
    string? Reason = null,
    bool TargetIsSelf = false);

/// <summary>
/// Classifies BaB catalog spells into deterministic resolutions (damage, heal, life drain, status,
/// cleanse, or non-mutating effect) per <c>6-Magic.md</c>. Spells whose mechanics Body and Brain
/// leaves underspecified (summoning, animation, control, teleport, utility, instant-kill, revive)
/// remain adjudicated so the engine does not invent rules.
/// </summary>
public static class SpellResolver
{
    private static readonly HashSet<string> Adjudicated = new(StringComparer.OrdinalIgnoreCase)
    {
        // Summoning list (conjures creatures)
        "Hawk", "Dog", "Cougar", "Wolf", "Bear", "Pegasus", "Golem", "Dragon",
        // Necromancer summon/control/utility
        "Grave Sense", "Death Whisper", "Bone Servant", "Command Undead", "Animate Dead",
        "Bind Spirit", "Lichdom",
        // Mind/utility/control and teleport/instant effects without deterministic combat rules
        "Read Mind", "Mind Control", "Meditate", "Displace", "Break Bonds", "Disintegration",
        "Revive Dead", "Discover", "Reveal", "Unseen", "Sure Shot", "Track",
    };

    private static readonly HashSet<string> HealNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Heal", "Restore", "Bandage",
    };

    // Status-clearing prayers/leadership spells -> which status category they remove.
    private static readonly Dictionary<string, string> CleanseMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Stop Bleeding"] = "Bleed",
        ["Stop Poison"] = "Poison",
        ["Antidote"] = "Poison",
        ["Stop Disease"] = "Disease",
        ["Cure"] = "Disease",
        ["Remove Curse"] = "Curse",
    };

    // Damage-over-time / status-applying spells -> the status they impose.
    private static readonly Dictionary<string, string> StatusMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Cause Bleeding"] = "Bleed",
        ["Infection"] = "Disease",
        ["Nauseate"] = "Poison",
    };

    public static SpellResolution Resolve(SpellDefinition spell, int casterLevel, IDiceRoller dice)
    {
        var name = spell.Name.Trim();

        if (Adjudicated.Contains(name))
            return new SpellResolution(SpellResolutionKind.Adjudicate, Reason: "adjudication-spell");

        if (string.Equals(name, "Life Drain", StringComparison.OrdinalIgnoreCase))
        {
            var dmg = Mechanics.RollScaledModifier(spell.Modifier, casterLevel, dice);
            return new SpellResolution(SpellResolutionKind.LifeDrain, dmg);
        }

        if (CleanseMap.TryGetValue(name, out var cleanseType))
            return new SpellResolution(SpellResolutionKind.Cleanse, StatusType: cleanseType, EffectText: spell.Description);

        if (StatusMap.TryGetValue(name, out var statusType))
        {
            var mag = Mechanics.RollScaledModifier(spell.Modifier, casterLevel, dice);
            return new SpellResolution(SpellResolutionKind.ApplyStatus, mag, statusType, spell.Description);
        }

        var isSelf = string.Equals(spell.Type, "Self", StringComparison.OrdinalIgnoreCase);

        if (HealNames.Contains(name) || ContainsAny(spell.Description, "heals", "recovers health", "healed"))
        {
            var heal = Mechanics.RollScaledModifier(spell.Modifier, casterLevel, dice);
            return new SpellResolution(SpellResolutionKind.Heal, heal, TargetIsSelf: isSelf);
        }

        var hasDamageDice = HasDice(spell.Modifier)
            && ContainsAny(spell.Description, "damage", "damages", "crushed", "bolt", "blast");
        var turnUndead = string.Equals(name, "Turn Undead", StringComparison.OrdinalIgnoreCase);
        if (hasDamageDice || turnUndead)
        {
            var dmg = Mechanics.RollScaledModifier(spell.Modifier, casterLevel, dice);
            return new SpellResolution(SpellResolutionKind.Damage, dmg);
        }

        // Buffs/debuffs and other deterministic non-mutating effects (Hasten, Bless, Focus, Confuse,
        // Curse, Embolden, Weaken, Will/Mind/Body reductions, Songs, Sleep, Fear, Cause Fear).
        if (!string.IsNullOrWhiteSpace(spell.Description))
        {
            var amount = Mechanics.RollScaledModifier(spell.Modifier, casterLevel, dice);
            return new SpellResolution(SpellResolutionKind.Effect, amount, EffectText: spell.Description, TargetIsSelf: isSelf);
        }

        return new SpellResolution(SpellResolutionKind.Adjudicate, Reason: "adjudication-spell");
    }

    private static bool HasDice(string? modifier)
        => !string.IsNullOrWhiteSpace(modifier) && modifier.Contains('D', StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string? text, params string[] needles)
        => !string.IsNullOrWhiteSpace(text) && needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));
}
