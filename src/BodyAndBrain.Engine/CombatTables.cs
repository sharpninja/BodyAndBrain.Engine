namespace BodyAndBrain.Engine;

/// <summary>
/// BaB-canonical combat tables transcribed from <c>7-Combat.md</c> (manual repo). Covers the
/// Base Attack Bonus table (weapon attack profile x defender armor profile), the Critical Type
/// table (weapon x armor -> crit type and numeric modifier), and the Critical and Failure
/// Resolution table (crit score x crit type -> immediate extra and per-round bleed).
/// </summary>
public static class CombatTables
{
    /// <summary>Physical-attack armor columns, in table order.</summary>
    public static readonly string[] ArmorColumns = ["None", "Robes", "Soft", "Rigid", "Chain", "Plate"];

    // Base Attack Bonus: weapon attack profile -> [None, Robes, Soft, Rigid, Chain, Plate].
    private static readonly Dictionary<string, int[]> BaseAttack = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1H Edge"] = [10, 5, 0, 0, -10, -20],
        ["2H Edge"] = [4, 10, 10, 0, 0, -10],
        ["1H Conc"] = [0, 0, 0, 0, -5, 5],
        ["2H Conc"] = [0, 0, 0, 0, 0, 10],
        ["Thrown"] = [5, 0, 0, 0, 0, -20],
        ["Crossbow"] = [15, 10, 5, 0, 0, -10],
        ["Shortbow"] = [10, 5, 0, 0, 0, -15],
        ["Longbow"] = [20, 15, 5, 0, 0, -5],
        ["Fist"] = [0, 0, -5, -5, -10, -15],
        ["Claws"] = [10, 5, 0, -5, -15, -25],
    };

    // Critical Type: weapon attack profile -> [None, Robes, Soft, Rigid, Chain, Plate] as "Type+/-mod".
    // Fist has no critical type ("None").
    private static readonly Dictionary<string, string[]> CritType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1H Edge"] = ["Slash+1", "Slash", "Slash-1", "Slash-1", "Slash-2", "Slash-4"],
        ["2H Edge"] = ["Slash+2", "Slash+1", "Slash", "Slash+1", "Slash-1", "Slash-2"],
        ["1H Conc"] = ["Crush", "Crush", "Crush", "Crush", "Crush-1", "Crush"],
        ["2H Conc"] = ["Crush+2", "Crush+2", "Crush+1", "Crush", "Crush-1", "Crush"],
        ["Thrown"] = ["Puncture", "Puncture", "Puncture", "Puncture", "Puncture", "Puncture-1"],
        ["Crossbow"] = ["Puncture+1", "Puncture+1", "Puncture", "Puncture", "Puncture", "Puncture"],
        ["Shortbow"] = ["Puncture", "Puncture", "Puncture", "Puncture", "Puncture", "Puncture-1"],
        ["Longbow"] = ["Puncture+1", "Puncture+1", "Puncture", "Puncture", "Puncture", "Puncture"],
        ["Claws"] = ["Claw+1", "Claw", "Claw-1", "Claw-2", "Claw-3", "Claw-4"],
    };

    // Critical and Failure Resolution: crit-score step index 0..9 maps to crit/fum value
    // [Less-3, -3, -2, -1, 0, 1, 2, 3, 4, Greater4]. Each cell is (immediate extra, per-round bleed).
    private static readonly Dictionary<string, (int Immediate, int PerRound)[]> CritResolution =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Slash"] = [(0, 0), (1, 0), (2, 0), (1, 1), (2, 2), (3, 2), (3, 3), (4, 3), (5, 3), (6, 4)],
            ["Crush"] = [(0, 0), (1, 0), (2, 0), (3, 0), (4, 0), (5, 0), (6, 0), (7, 0), (8, 0), (10, 0)],
            ["Puncture"] = [(0, 0), (1, 0), (2, 0), (2, 1), (3, 1), (4, 1), (4, 2), (5, 2), (5, 3), (6, 4)],
            ["Claw"] = [(0, 0), (1, 0), (2, 0), (1, 1), (2, 1), (3, 1), (3, 2), (4, 2), (5, 2), (6, 3)],
        };

    /// <summary>Maps a defender armor profile (id-derived or profile name) to its attack-table column index.</summary>
    public static int ArmorColumnIndex(string? armorProfile)
        => (armorProfile ?? "None").Trim().ToLowerInvariant() switch
        {
            "robes" or "cloth" or "heavy clothing/robes" => 1,
            "soft" or "soft leather" => 2,
            "rigid" or "rigid leather" => 3,
            "chain" or "scale" => 4,
            "plate" => 5,
            _ => 0, // None, Helm, Shield, unknown -> None column for the body attack table
        };

    /// <summary>Base Attack Bonus for a weapon attack profile against a defender armor column.</summary>
    public static int BaseAttackBonus(string attackProfile, int armorColumn)
        => BaseAttack.TryGetValue(attackProfile, out var row) ? row[Math.Clamp(armorColumn, 0, 5)] : 0;

    /// <summary>
    /// Resolves the critical type and numeric modifier for a weapon attack profile against an armor column.
    /// Returns null when the weapon has no critical type (e.g. Fist).
    /// </summary>
    public static (string Type, int Modifier)? CriticalType(string attackProfile, int armorColumn)
    {
        if (!CritType.TryGetValue(attackProfile, out var row))
            return null;
        return ParseCritType(row[Math.Clamp(armorColumn, 0, 5)]);
    }

    private static (string Type, int Modifier) ParseCritType(string cell)
    {
        var plus = cell.IndexOf('+');
        var minus = cell.IndexOf('-');
        var split = plus >= 0 ? plus : minus;
        if (split < 0)
            return (cell.Trim(), 0);
        var type = cell[..split].Trim();
        var modifier = int.Parse(cell[split..].Trim());
        return (type, modifier);
    }

    /// <summary>
    /// Resolves the critical effect for a crit score and crit type. The crit score is
    /// (attacker 1D4 + critical modifier - defender 1D4); it is clamped to the table range.
    /// </summary>
    public static (int Immediate, int PerRound) CriticalEffect(string critType, int critScore)
    {
        if (!CritResolution.TryGetValue(critType, out var row))
            return (0, 0);
        return row[CritStepIndex(critScore)];
    }

    /// <summary>Maps a crit score to a resolution-table row index (0 = Less-3 .. 9 = Greater 4).</summary>
    public static int CritStepIndex(int critScore)
    {
        if (critScore < -3) return 0;       // Less -3
        if (critScore > 4) return 9;        // Greater 4
        return critScore + 4;               // -3 -> 1, ... , 4 -> 8
    }

    /// <summary>
    /// Engine-derived armor damage-reduction percent overlay (NOT canonical BaB). Provided for
    /// downstream consumers that expect a protection percent; BaB itself models armor as accuracy
    /// mitigation via <see cref="BaseAttackBonus"/>.
    /// </summary>
    public static int ProtectionPercent(string? armorProfile)
        => (armorProfile ?? "None").Trim().ToLowerInvariant() switch
        {
            "robes" or "cloth" or "heavy clothing/robes" => 5,
            "soft" or "soft leather" => 10,
            "rigid" or "rigid leather" => 15,
            "chain" or "scale" => 25,
            "plate" => 40,
            "helm" => 5,
            "shield" => 10,
            _ => 0,
        };
}
