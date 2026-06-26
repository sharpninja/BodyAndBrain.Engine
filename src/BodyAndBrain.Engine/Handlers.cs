using SharpNinja.FeatureFlags.Cqrs;

namespace BodyAndBrain.Engine;

public sealed class GetGameDataQueryHandler(IGameDataCatalog catalog) : IQueryHandler<GetGameDataQuery, GameData>
{
    public Task<Result<GameData>> HandleAsync(GetGameDataQuery query, CallContext context)
        => Task.FromResult(Result.Success(catalog.Data));
}

public sealed class ValidateGameDataQueryHandler(IGameDataCatalog catalog) : IQueryHandler<ValidateGameDataQuery, GameDataValidationResult>
{
    public Task<Result<GameDataValidationResult>> HandleAsync(ValidateGameDataQuery query, CallContext context)
        => Task.FromResult(Result.Success(catalog.Validate()));
}

public sealed class EnumerateActionsQueryHandler(IGameDataCatalog catalog) : IQueryHandler<EnumerateActionsQuery, IReadOnlyList<ActionDefinition>>
{
    public Task<Result<IReadOnlyList<ActionDefinition>>> HandleAsync(EnumerateActionsQuery query, CallContext context)
        => Task.FromResult(Result.Success<IReadOnlyList<ActionDefinition>>(catalog.Data.Actions));
}

public sealed class GetPlayerCharacterQueryHandler(IGameStore store) : IQueryHandler<GetPlayerCharacterQuery, PlayerCharacterRecord?>
{
    public async Task<Result<PlayerCharacterRecord?>> HandleAsync(GetPlayerCharacterQuery query, CallContext context)
        => Result.Success(await store.GetPlayerAsync(query.CharacterId, context.CancellationToken).ConfigureAwait(false));
}

public sealed class GetNpcQueryHandler(IGameStore store) : IQueryHandler<GetNpcQuery, NpcRecord?>
{
    public async Task<Result<NpcRecord?>> HandleAsync(GetNpcQuery query, CallContext context)
        => Result.Success(await store.GetNpcAsync(query.NpcId, context.CancellationToken).ConfigureAwait(false));
}

public sealed class RenderNpcMarkdownQueryHandler(IGameStore store, NpcMarkdownRenderer renderer) : IQueryHandler<RenderNpcMarkdownQuery, string>
{
    public async Task<Result<string>> HandleAsync(RenderNpcMarkdownQuery query, CallContext context)
    {
        var npc = await store.GetNpcAsync(query.NpcId, context.CancellationToken).ConfigureAwait(false);
        return npc is null
            ? Result.Failure<string>($"NPC '{query.NpcId}' was not found.")
            : Result.Success(renderer.Render(npc));
    }
}

public sealed class CreatePlayerCharacterCommandHandler(IGameDataCatalog catalog, IGameStore store, MagicNumbers magic)
    : ICommandHandler<CreatePlayerCharacterCommand, PlayerCharacterRecord>
{
    public async Task<Result<PlayerCharacterRecord>> HandleAsync(CreatePlayerCharacterCommand command, CallContext context)
    {
        try
        {
            var race = catalog.GetRace(command.Race);
            var profession = catalog.GetProfession(command.Profession);
            if (!race.ValidProfessions.Contains(profession.Name, StringComparer.OrdinalIgnoreCase))
                return Result.Failure<PlayerCharacterRecord>($"{race.Name} cannot be a {profession.Name}.");

            var apprenticeship = catalog.Data.Apprenticeships.SingleOrDefault(x =>
                string.Equals(x.Name, command.Apprenticeship, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Id, Slug.Normalize(command.Apprenticeship), StringComparison.OrdinalIgnoreCase));
            if (apprenticeship is null || !string.Equals(apprenticeship.Profession, profession.Name, StringComparison.OrdinalIgnoreCase))
                return Result.Failure<PlayerCharacterRecord>($"Apprenticeship '{command.Apprenticeship}' is not valid for {profession.Name}.");

            var stats = race.StatBonuses.ToDictionary(x => x.Key, x => Mechanics.ClampStat(magic.BaseStat + x.Value), StringComparer.OrdinalIgnoreCase);
            var maxHits = magic.BaseHits + Mechanics.StatBonus(stats.GetValueOrDefault("Constitution")) * 5;
            var record = new PlayerCharacterRecord
            {
                Id = Guid.NewGuid().ToString("n"),
                Name = command.Name,
                Race = race.Name,
                Profession = profession.Name,
                Apprenticeship = apprenticeship.Name,
                Level = 1,
                Stats = stats,
                Skills = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    [profession.PrimaryStat] = 1,
                },
                MaxHits = maxHits,
                CurrentHits = maxHits,
            };

            await store.UpsertPlayerAsync(record, context.CancellationToken).ConfigureAwait(false);
            return Result.Success(record);
        }
        catch (Exception ex)
        {
            return Result.Failure<PlayerCharacterRecord>(ex);
        }
    }
}

public sealed class ApplyLevelUpCommandHandler(IGameStore store, MagicNumbers magic) : ICommandHandler<ApplyLevelUpCommand, PlayerCharacterRecord>
{
    public async Task<Result<PlayerCharacterRecord>> HandleAsync(ApplyLevelUpCommand command, CallContext context)
    {
        var character = await store.GetPlayerAsync(command.CharacterId, context.CancellationToken).ConfigureAwait(false);
        if (character is null)
            return Result.Failure<PlayerCharacterRecord>($"Character '{command.CharacterId}' was not found.");
        if (character.Level >= magic.MaxLevel)
            return Result.Failure<PlayerCharacterRecord>($"Character is already at the maximum level {magic.MaxLevel}.");

        // Validate increments use known stats (from magic config)
        foreach (var (stat, _) in command.StatIncrements ?? [])
        {
            if (!magic.StatNames.Contains(stat, StringComparer.OrdinalIgnoreCase))
                return Result.Failure<PlayerCharacterRecord>($"Unknown stat '{stat}' in level-up increments.");
        }

        character.Level++;
        foreach (var (stat, amount) in command.StatIncrements ?? [])
            character.Stats[stat] = Mechanics.ClampStat(character.Stats.GetValueOrDefault(stat) + amount);
        foreach (var (skill, amount) in command.SkillIncrements ?? [])
            character.Skills[skill] = Math.Clamp(character.Skills.GetValueOrDefault(skill) + amount, 0, magic.SkillMax);

        character.MaxHits = Math.Max(1, magic.BaseHits + Mechanics.LevelSignificance(character.Level) * 3 + Mechanics.StatBonus(character.Stats.GetValueOrDefault("Constitution")) * 5);
        character.CurrentHits = Math.Min(character.MaxHits, character.CurrentHits + 5);
        character.LevelUpHistory.Add($"Level {character.Level}: stat increments={command.StatIncrements?.Count ?? 0}; skill increments={command.SkillIncrements?.Count ?? 0}");
        await store.UpsertPlayerAsync(character, context.CancellationToken).ConfigureAwait(false);
        return Result.Success(character);
    }
}

public sealed class GenerateNpcCommandHandler(NpcGenerator generator, IGameStore store) : ICommandHandler<GenerateNpcCommand, NpcRecord>
{
    public async Task<Result<NpcRecord>> HandleAsync(GenerateNpcCommand command, CallContext context)
    {
        try
        {
            var npc = generator.Generate(command.Race, command.Profession, command.Level, command.Name);
            if (command.Persist)
                await store.UpsertNpcAsync(npc, context.CancellationToken).ConfigureAwait(false);
            return Result.Success(npc);
        }
        catch (Exception ex)
        {
            return Result.Failure<NpcRecord>(ex);
        }
    }
}

public sealed class ExecuteGameActionCommandHandler(ActionExecutor executor) : ICommandHandler<ExecuteGameActionCommand, YamlActionResult>
{
    public Task<Result<YamlActionResult>> HandleAsync(ExecuteGameActionCommand command, CallContext context)
        => executor.ExecuteAsync(command, context.CancellationToken);
}

public sealed class GenerateMonsterCommandHandler(MonsterGenerator generator, IGameStore store)
    : ICommandHandler<GenerateMonsterCommand, NpcRecord>
{
    public async Task<Result<NpcRecord>> HandleAsync(GenerateMonsterCommand command, CallContext context)
    {
        try
        {
            var monster = generator.Generate(command.MonsterId, command.Level, command.Name);
            if (command.Persist)
                await store.UpsertNpcAsync(monster, context.CancellationToken).ConfigureAwait(false);
            return Result.Success(monster);
        }
        catch (Exception ex)
        {
            return Result.Failure<NpcRecord>(ex);
        }
    }
}

public sealed class TickStatusEffectsCommandHandler(ActionExecutor executor)
    : ICommandHandler<TickStatusEffectsCommand, YamlActionResult>
{
    public Task<Result<YamlActionResult>> HandleAsync(TickStatusEffectsCommand command, CallContext context)
        => executor.TickStatusesAsync(command, context.CancellationToken);
}
