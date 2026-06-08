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

    public static bool OutcomeHits(string outcome)
        => outcome.Contains("Hit", StringComparison.OrdinalIgnoreCase);
}
