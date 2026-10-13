using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Web;
using Akiron.Contracts.Identity;
using Akiron.Modules.Automation.Domain;
using Akiron.Modules.Automation.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Automation.Features;

/// <param name="Target">An e-mail address for <c>email_address</c>, a member's id for <c>notify_user</c>.</param>
internal sealed record RuleCommand(string Name, string Trigger, string Action, string Body, string? Subject = null, string? Target = null, bool Enabled = true);

internal sealed class RuleValidator : AbstractValidator<RuleCommand>
{
    public RuleValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(AutomationRule.NameMaxLength);
        RuleFor(command => command.Trigger).Must(trigger => AutomationCatalog.Triggers.ContainsKey(trigger)).WithErrorCode("validation.invalid_value");
        RuleFor(command => command.Action).Must(action => AutomationCatalog.Actions.Contains(action)).WithErrorCode("validation.invalid_value");
        RuleFor(command => command.Body).NotEmpty().MaximumLength(AutomationRule.BodyMaxLength);
        RuleFor(command => command.Subject).MaximumLength(AutomationRule.SubjectMaxLength);
        RuleFor(command => command.Subject).NotEmpty().When(command => AutomationCatalog.NeedsSubject(command.Action));
        RuleFor(command => command.Target!).NotEmpty().EmailAddress().MaximumLength(AutomationRule.TargetMaxLength).When(command => command.Action == AutomationCatalog.EmailAddress);
        RuleFor(command => command.Target).Must(target => Guid.TryParse(target, out _)).WithErrorCode("validation.invalid_value").When(command => command.Action == AutomationCatalog.NotifyUser);
    }
}

internal sealed record RuleResponse(
    Guid Id,
    string Name,
    string Trigger,
    string Action,
    string? Target,
    string? TargetName,
    string? Subject,
    string Body,
    bool Enabled,
    int RunCount,
    DateTimeOffset? LastRunAt);

internal sealed record RunResponse(DateTimeOffset OccurredAt, string Outcome, string? Detail);

internal sealed record TriggerInfo(string Code, IReadOnlyList<string> Variables, bool HasClient);

internal sealed record CatalogResponse(IReadOnlyList<TriggerInfo> Triggers, IReadOnlyList<string> Actions);

internal static class AutomationErrors
{
    public static readonly Error RuleNotFound = Error.NotFound("automation.rule.not_found", "No such rule in this organisation.");

    public static readonly Error NoClient = Error.Validation("automation.rule.trigger_has_no_client", "This trigger has no client to write to; notify the team instead.");

    public static readonly Error TargetNotMember = Error.Validation("automation.rule.target_not_member", "The person to notify must be a member of this organisation.");
}

/// <summary>The rules a tenant has set up, and what each has done lately.</summary>
internal sealed class AutomationRulesHandler(AutomationDbContext db, IMemberDirectory members)
{
    /// <summary>A new lead is not a client yet: nothing to e-mail or text.</summary>
    private static readonly HashSet<string> TriggersWithoutClient = [AutomationCatalog.LeadCreated];

    public static CatalogResponse Catalog { get; } = new(
        AutomationCatalog.Triggers.Select(pair => new TriggerInfo(pair.Key, pair.Value, !TriggersWithoutClient.Contains(pair.Key))).ToList(),
        AutomationCatalog.Actions);

    public async Task<IReadOnlyList<RuleResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var rules = await db.Rules.AsNoTracking().OrderBy(rule => rule.CreatedAt).ToListAsync(cancellationToken);
        var names = await NamesAsync(rules, cancellationToken);
        return rules.Select(rule => ToResponse(rule, names)).ToList();
    }

    public async Task<Result<RuleResponse>> CreateAsync(RuleCommand command, CancellationToken cancellationToken)
    {
        if (await CheckAsync(command, cancellationToken) is { } refused)
        {
            return refused;
        }

        var rule = AutomationRule.Create(ToDefinition(command));
        db.Rules.Add(rule);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(rule, await NamesAsync([rule], cancellationToken));
    }

    public async Task<Result<RuleResponse>> UpdateAsync(Guid id, RuleCommand command, CancellationToken cancellationToken)
    {
        var rule = await FindAsync(id, cancellationToken);
        if (rule is null)
        {
            return AutomationErrors.RuleNotFound;
        }

        if (await CheckAsync(command, cancellationToken) is { } refused)
        {
            return refused;
        }

        rule.Update(ToDefinition(command));
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(rule, await NamesAsync([rule], cancellationToken));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var rule = await FindAsync(id, cancellationToken);
        if (rule is null)
        {
            return AutomationErrors.RuleNotFound;
        }

        db.Rules.Remove(rule);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Result<IReadOnlyList<RunResponse>>> RunsAsync(Guid id, CancellationToken cancellationToken)
    {
        var ruleId = AutomationRuleId.From(id);
        if (!await db.Rules.AnyAsync(rule => rule.Id == ruleId, cancellationToken))
        {
            return AutomationErrors.RuleNotFound;
        }

        var runs = await db.Runs.AsNoTracking().Where(run => run.RuleId == ruleId).OrderByDescending(run => run.OccurredAt).Take(20).ToListAsync(cancellationToken);
        return runs.Select(run => new RunResponse(run.OccurredAt, run.Outcome.ToString().ToLowerInvariant(), run.Detail)).ToList();
    }

    private static RuleDefinition ToDefinition(RuleCommand command) =>
        new(
            command.Name,
            command.Trigger,
            command.Action,
            command.Action is AutomationCatalog.EmailAddress or AutomationCatalog.NotifyUser ? command.Target : null,
            AutomationCatalog.NeedsSubject(command.Action) ? command.Subject : null,
            command.Body,
            command.Enabled);

    private static RuleResponse ToResponse(AutomationRule rule, IReadOnlyDictionary<Guid, MemberSummary> names) => new(
        rule.Id.Value,
        rule.Name,
        rule.Trigger,
        rule.Action,
        rule.Target,
        rule.Action == AutomationCatalog.NotifyUser && Guid.TryParse(rule.Target, out var userId) ? names.GetValueOrDefault(userId)?.FullName : null,
        rule.Subject,
        rule.Body,
        rule.Enabled,
        rule.RunCount,
        rule.LastRunAt);

    private async Task<Error?> CheckAsync(RuleCommand command, CancellationToken cancellationToken)
    {
        if (AutomationCatalog.WritesToClient(command.Action) && TriggersWithoutClient.Contains(command.Trigger))
        {
            return AutomationErrors.NoClient;
        }

        if (command.Action == AutomationCatalog.NotifyUser)
        {
            var userId = Guid.Parse(command.Target!);
            if (!(await members.FindAsync([userId], cancellationToken)).ContainsKey(userId))
            {
                return AutomationErrors.TargetNotMember;
            }
        }

        return null;
    }

    private Task<IReadOnlyDictionary<Guid, MemberSummary>> NamesAsync(IEnumerable<AutomationRule> rules, CancellationToken cancellationToken) =>
        members.FindAsync(
            rules.Where(rule => rule.Action == AutomationCatalog.NotifyUser && Guid.TryParse(rule.Target, out _)).Select(rule => Guid.Parse(rule.Target!)).Distinct(),
            cancellationToken);

    private Task<AutomationRule?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var ruleId = AutomationRuleId.From(id);
        return db.Rules.FirstOrDefaultAsync(rule => rule.Id == ruleId, cancellationToken);
    }
}

internal static class AutomationEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/catalog", () => TypedResults.Ok(AutomationRulesHandler.Catalog))
            .RequirePermission(AutomationPermissions.Manage)
            .Produces<CatalogResponse>()
            .WithSummary("What a rule can start from and do, with the variables each trigger offers");

        var rules = endpoints.MapGroup("/rules");

        rules.MapGet("/", async (AutomationRulesHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.ListAsync(cancellationToken)))
            .RequirePermission(AutomationPermissions.Manage)
            .Produces<IReadOnlyList<RuleResponse>>()
            .WithSummary("The organisation's rules");

        rules.MapPost("/", async (RuleCommand command, AutomationRulesHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.CreateAsync(command, cancellationToken);
                return result.IsSuccess ? Results.Created($"/api/v1/automation/rules/{result.Value.Id}", result.Value) : result.Error.ToProblem();
            })
            .RequirePermission(AutomationPermissions.Manage)
            .Validate<RuleCommand>()
            .Produces<RuleResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Add a rule: when this happens, do that");

        rules.MapPut("/{id:guid}", async (Guid id, RuleCommand command, AutomationRulesHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.UpdateAsync(id, command, cancellationToken)))
            .RequirePermission(AutomationPermissions.Manage)
            .Validate<RuleCommand>()
            .Produces<RuleResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Change a rule, or switch it on or off");

        rules.MapDelete("/{id:guid}", async (Guid id, AutomationRulesHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.DeleteAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
            })
            .RequirePermission(AutomationPermissions.Manage)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove a rule");

        rules.MapGet("/{id:guid}/runs", async (Guid id, AutomationRulesHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.RunsAsync(id, cancellationToken)))
            .RequirePermission(AutomationPermissions.Manage)
            .Produces<IReadOnlyList<RunResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("What a rule did the last twenty times it fired");
    }

    private static IResult Ok<T>(Result<T> result) => result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
}
