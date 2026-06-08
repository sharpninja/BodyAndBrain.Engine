using SharpNinja.FeatureFlags.Cqrs;

namespace BodyAndBrain.Engine;

public sealed record GetGameDataQuery : IQuery<GameData>;

public sealed record ValidateGameDataQuery : IQuery<GameDataValidationResult>;

public sealed record EnumerateActionsQuery : IQuery<IReadOnlyList<ActionDefinition>>;

public sealed record GetPlayerCharacterQuery(string CharacterId) : IQuery<PlayerCharacterRecord?>;

public sealed record GetNpcQuery(string NpcId) : IQuery<NpcRecord?>;

public sealed record RenderNpcMarkdownQuery(string NpcId) : IQuery<string>;

public sealed record CreatePlayerCharacterCommand(
    string Name,
    string Race,
    string Profession,
    string Apprenticeship) : ICommand<PlayerCharacterRecord>;

public sealed record ApplyLevelUpCommand(
    string CharacterId,
    Dictionary<string, int>? StatIncrements = null,
    Dictionary<string, int>? SkillIncrements = null) : ICommand<PlayerCharacterRecord>;

public sealed record GenerateNpcCommand(
    string Race,
    string Profession,
    int Level,
    string? Name = null,
    bool Persist = true) : ICommand<NpcRecord>;

public sealed record ExecuteGameActionCommand(
    string ActionId,
    string ActorId,
    string? TargetId = null,
    int? RollOverride = null,
    Dictionary<string, string>? Parameters = null) : ICommand<YamlActionResult>;
