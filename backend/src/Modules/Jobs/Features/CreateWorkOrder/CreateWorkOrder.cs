using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Contracts.Crm;
using Akiron.Contracts.Identity;
using Akiron.Contracts.Jobs;
using Akiron.Contracts.Sales;
using Akiron.Modules.Jobs.Domain;
using Akiron.Modules.Jobs.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Jobs.Features.CreateWorkOrder;

/// <summary>The fields create and update share.</summary>
internal interface IWorkOrderInput
{
    string Title { get; }

    string? Description { get; }

    Guid? PartyId { get; }

    string? Priority { get; }

    DateOnly? DueDate { get; }

    IReadOnlyList<Guid>? AssigneeIds { get; }
}

internal static class WorkOrderInputRules
{
    public static void Apply<T>(AbstractValidator<T> validator)
        where T : IWorkOrderInput
    {
        validator.RuleFor(input => input.Title).NotEmpty().MaximumLength(WorkOrder.TitleMaxLength);
        validator.RuleFor(input => input.Description).MaximumLength(WorkOrder.DescriptionMaxLength);
        validator.RuleFor(input => input.Priority).Must(priority => priority is null || JobsApi.Priorities.Contains(priority))
            .WithErrorCode("validation.invalid_value");
        validator.RuleFor(input => input.AssigneeIds!.Count).LessThanOrEqualTo(20).When(input => input.AssigneeIds is not null)
            .OverridePropertyName(nameof(IWorkOrderInput.AssigneeIds));
    }
}

/// <summary>Checks the client and the people against their own modules before a work order points at them.</summary>
internal sealed class WorkOrderReferences(IPartyDirectory parties, IMemberDirectory members)
{
    public async Task<Result<(PartySummary? Party, IReadOnlyList<Guid> Assignees)>> ResolveAsync(IWorkOrderInput input, CancellationToken cancellationToken)
    {
        PartySummary? party = null;
        if (input.PartyId is { } partyId)
        {
            party = await parties.FindAsync(partyId, cancellationToken);
            if (party is null)
            {
                return JobsErrors.PartyNotFound;
            }
        }

        var assignees = (input.AssigneeIds ?? []).Distinct().ToList();
        var found = await members.FindAsync(assignees, cancellationToken);
        if (found.Count != assignees.Count)
        {
            return JobsErrors.AssigneeNotMember;
        }

        return (party, assignees);
    }

    public static WorkOrderDetails Details(IWorkOrderInput input, PartySummary? party) =>
        new(input.Title, input.Description, party?.PartyId, party?.Name, JobsApi.ParsePriority(input.Priority), input.DueDate);
}

internal sealed record CreateWorkOrderCommand(
    string Title,
    string? Description = null,
    Guid? PartyId = null,
    Guid? StageId = null,
    string? Priority = null,
    DateOnly? DueDate = null,
    IReadOnlyList<Guid>? AssigneeIds = null,
    Guid? TemplateId = null,
    Guid? QuoteId = null) : IWorkOrderInput;

internal sealed class CreateWorkOrderValidator : AbstractValidator<CreateWorkOrderCommand>
{
    public CreateWorkOrderValidator() => WorkOrderInputRules.Apply(this);
}

internal sealed class CreateWorkOrderHandler(
    JobsDbContext db,
    StageBoard board,
    WorkOrderReferences references,
    WorkOrderReader reader,
    IAcceptedQuotes acceptedQuotes,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public const string NumberSeries = "IS";

    public async Task<Result<WorkOrderResponse>> HandleAsync(CreateWorkOrderCommand command, CancellationToken cancellationToken)
    {
        // From an accepted quote: its client, its net total as the budget, its lines as the checklist.
        AcceptedQuote? quote = null;
        if (command.QuoteId is { } quoteId)
        {
            quote = await acceptedQuotes.FindAsync(quoteId, cancellationToken);
            if (quote is null)
            {
                return JobsErrors.QuoteNotAccepted;
            }

            command = command with { PartyId = quote.PartyId };
        }

        var resolved = await references.ResolveAsync(command, cancellationToken);
        if (!resolved.IsSuccess)
        {
            return resolved.Error;
        }

        var (party, assignees) = resolved.Value;

        // A template adds its checklist, and its lead time when no due date was chosen.
        WorkOrderTemplate? template = null;
        if (command.TemplateId is { } templateId)
        {
            template = await db.Templates.FirstOrDefaultAsync(candidate => candidate.Id == WorkOrderTemplateId.From(templateId), cancellationToken);
            if (template is null)
            {
                return Templates.TemplatesHandler.NotFound;
            }
        }

        var stages = await board.StagesAsync(cancellationToken);
        var stage = command.StageId is { } stageId
            ? stages.FirstOrDefault(candidate => candidate.Id == Domain.StageId.From(stageId))
            : stages.First(candidate => candidate.Category != StageCategory.Done);
        if (stage is null)
        {
            return JobsErrors.StageNotFound;
        }

        var now = timeProvider.GetUtcNow();
        var userId = currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var sequence = await db.NextDocumentSequenceAsync(NumberSeries, now.Year, cancellationToken);
        var rank = await board.RankAtAsync(stage.Id, null, null, cancellationToken);
        var details = WorkOrderReferences.Details(command, party);
        if (details.DueDate is null && template?.DueInDays is { } days)
        {
            details = details with { DueDate = DateOnly.FromDateTime(now.UtcDateTime).AddDays(days) };
        }

        var workOrder = WorkOrder.Create(DocumentNumber.Format(NumberSeries, now.Year, sequence), stage, rank, details, now);
        workOrder.AssignExactly(assignees);

        db.WorkOrders.Add(workOrder);
        if (quote is not null)
        {
            workOrder.FromQuote(quote.QuoteId, quote.NetTotalTry);
        }

        var checklist = template?.Tasks ?? quote?.LineNames ?? [];
        db.Tasks.AddRange(checklist.Select((title, position) => WorkOrderTask.Create(workOrder.Id, title, position)));
        db.Publish(new WorkOrderCreated(
            tenantContext.TenantId, now, workOrder.Id.Value, workOrder.Number, workOrder.Title, workOrder.PartyId, workOrder.PartyName, assignees, userId.Value, currentUser.DisplayName));
        if (assignees.Count > 0)
        {
            db.Publish(new WorkOrderAssigned(tenantContext.TenantId, now, workOrder.Id.Value, workOrder.Number, workOrder.Title, assignees, userId.Value, currentUser.DisplayName));
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await reader.DetailAsync(workOrder, cancellationToken);
    }
}
