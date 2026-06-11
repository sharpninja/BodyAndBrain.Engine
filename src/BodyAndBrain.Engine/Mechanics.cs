using System.Text.RegularExpressions;

namespace BodyAndBrain.Engine;

public interface IDiceRoller
{
    int D100();
    int Roll(string notation);
}

public sealed class RandomDiceRoller : IDiceRoller
{
    private readonly Random _random = new();

    public int D100() => _random.Next(1, 101);

    public int Roll(string notation)
    {
        var match = Regex.Match(notation ?? "", @"(?<count>\d+)D(?<sides>\d+)", RegexOptions.IgnoreCase);
        if (!match.Success)
            return 0;

        var count = int.Parse(match.Groups["count"].Value);
        var sides = int.Parse(match.Groups["sides"].Value);
        var total = 0;
        for (var i = 0; i < count; i++)
            total += _random.Next(1, sides + 1);
        return total;
    }
}

public static class Mechanics
{
    public static int ClampStat(int value) => Math.Clamp(value, 1, 100);

    public static int StatBonus(int score)
        => score switch
        {
            >= 100 => 10,
            >= 95 => 4,
            >= 90 => 3,
            >= 75 => 2,
            >= 50 => 1,
            _ => 0,
        };

    public static int LevelSignificance(int level)
    {
        var offset = level - 5;
        var magnitude = Math.Abs(offset);
        var triangular = magnitude * (magnitude + 1) / 2;
        return Math.Sign(offset) * triangular;
    }

    public static (int body, int brain) AgeModifier(int level)
        => level switch
        {
            1 => (-10, -10),
            >= 2 and <= 4 => (5, -5),
            >= 8 => (-5, 5),
            _ => (0, 0),
        };

    public static bool IsBodyStat(string stat)
        => string.Equals(stat, "Strength", StringComparison.OrdinalIgnoreCase)
           || string.Equals(stat, "Agility", StringComparison.OrdinalIgnoreCase)
           || string.Equals(stat, "Constitution", StringComparison.OrdinalIgnoreCase);

    public static string RollOutcome(int naturalRoll)
        => naturalRoll switch
        {
            1 => "F",
            100 => "2x Hit + Critical",
            <= 25 => "Miss",
            <= 49 => "Hit / 2",
            <= 74 => "Hit",
            _ => "Hit + Critical",
        };

    /// <summary>
    /// Resolves the attack outcome from the natural roll and the bonus-adjusted total per the BaB
    /// Physical/Spell Attack Roll Outcome tables. A natural 1 is always a fumble; a natural 100 is
    /// always the best result; otherwise the bonus-adjusted total selects the band.
    /// </summary>
    public static string RollOutcome(int naturalRoll, int total)
        => naturalRoll switch
        {
            1 => "F",
            100 => "2x Hit + Critical",
            _ => total switch
            {
                <= 25 => "Miss",
                <= 49 => "Hit / 2",
                <= 74 => "Hit",
                _ => "Hit + Critical",
            },
        };

    public static bool OutcomeHits(string outcome)
        => outcome.Contains("Hit", StringComparison.OrdinalIgnoreCase);

    public static bool OutcomeIsCritical(string outcome)
        => outcome.Contains("Critical", StringComparison.OrdinalIgnoreCase);

    public static bool OutcomeIsHalf(string outcome)
        => outcome.Contains("/ 2", StringComparison.Ordinal);

    public static bool OutcomeIsDouble(string outcome)
        => outcome.StartsWith("2x", StringComparison.OrdinalIgnoreCase);

    private static readonly Regex PerLevelPattern =
        new(@"per\s+(?<n>\d+)\s+levels?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DicePattern =
        new(@"(?<count>\d+)\s*[dD]\s*(?<sides>\d+)", RegexOptions.Compiled);
    private static readonly Regex FlatPattern =
        new(@"(?<![dD])(?<![\d])(?<val>[+-]?\d+)", RegexOptions.Compiled);

    /// <summary>
    /// Rolls a BaB spell/effect modifier with optional per-level scaling, e.g. <c>1D10</c>,
    /// <c>1D6 per 5 Levels</c>, <c>1D8 per 10 Levels</c>, or <c>+5 per 5 Levels</c>. Returns 0 for
    /// a missing modifier ("-" or empty). Dice are rolled via <paramref name="dice"/>; flat values
    /// are taken literally. The per-level multiplier is <c>max(1, level / N)</c>.
    /// </summary>
    public static int RollScaledModifier(string? modifier, int level, IDiceRoller dice)
    {
        if (string.IsNullOrWhiteSpace(modifier) || modifier.Trim() == "-")
            return 0;

        var text = modifier.Replace("/target", "", StringComparison.OrdinalIgnoreCase)
                           .Replace("/round", "", StringComparison.OrdinalIgnoreCase)
                           .Trim();

        var multiplier = 1;
        var levelMatch = PerLevelPattern.Match(text);
        if (levelMatch.Success)
        {
            var n = int.Parse(levelMatch.Groups["n"].Value);
            multiplier = Math.Max(1, n <= 0 ? 1 : level / n);
            text = text[..levelMatch.Index].Trim();
        }

        var diceMatch = DicePattern.Match(text);
        if (diceMatch.Success)
        {
            var rolled = dice.Roll($"{diceMatch.Groups["count"].Value}D{diceMatch.Groups["sides"].Value}");
            return Math.Max(0, rolled) * multiplier;
        }

        var flatMatch = FlatPattern.Match(text);
        if (flatMatch.Success && int.TryParse(flatMatch.Groups["val"].Value, out var flat))
            return flat * multiplier;

        return 0;
    }
}

/// <summary>
/// BaB-canonical status effects applied by spells, conditions, and critical bleed. Bleed, Poison,
/// Disease, and the control statuses (Stun, Move, Curse) tick per round and resolve a resistance roll.
/// </summary>
public static class StatusEffects
{
    public static readonly string[] Canonical = ["Bleed", "Poison", "Disease", "Stun", "Move", "Curse"];

    public static bool IsCanonical(string type)
        => Canonical.Contains(Normalize(type), StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string type)
        => Canonical.FirstOrDefault(x => string.Equals(x, type?.Trim(), StringComparison.OrdinalIgnoreCase))
           ?? (type ?? "").Trim();

    /// <summary>The stat that governs the resistance roll to shrug off a status.</summary>
    public static string ResistanceStat(string type)
        => Normalize(type) switch
        {
            "Bleed" => "Constitution",
            "Poison" => "Constitution",
            "Disease" => "Constitution",
            "Stun" => "Constitution",
            "Move" => "Agility",
            "Curse" => "Piety",
            _ => "Constitution",
        };

    /// <summary>True for statuses that deal damage on tick (Bleed, Poison, Disease).</summary>
    public static bool IsDamaging(string type)
        => Normalize(type) is "Bleed" or "Poison" or "Disease";

    public static int DefaultMagnitude(string type)
        => Normalize(type) switch
        {
            "Bleed" => 2,
            "Poison" => 3,
            "Disease" => 2,
            _ => 0,
        };

    public static int DefaultRounds(string type)
        => Normalize(type) switch
        {
            "Stun" => 1,
            "Move" => 1,
            _ => 3,
        };
}
