using Akiron.BuildingBlocks.Domain;
using Akiron.Modules.Jobs.Domain;
using Akiron.Modules.Jobs.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Jobs.Features.Templates;

internal sealed record TemplateCommand(
    string Name,
    string? Title = null,
    string? Description = null,
    string? Priority = null,
    int? DueInDays = null,
    IReadOnlyList<string>? Tasks = null);

internal sealed class TemplateValidator : AbstractValidator<TemplateCommand>
{
    public TemplateValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(WorkOrderTemplate.NameMaxLength);
        RuleFor(command => command.Title).MaximumLength(WorkOrder.TitleMaxLength);
        RuleFor(command => command.Description).MaximumLength(WorkOrder.DescriptionMaxLength);
        RuleFor(command => command.Priority).Must(priority => priority is null || JobsApi.Priorities.Contains(priority)).WithErrorCode("validation.invalid_value");
        RuleFor(command => command.DueInDays).InclusiveBetween(0, 365);
        RuleFor(command => command.Tasks!.Count).LessThanOrEqualTo(WorkOrderTemplate.MaxTasks).When(command => command.Tasks is not null)
            .OverridePropertyName(nameof(TemplateCommand.Tasks));
        RuleForEach(command => command.Tasks).MaximumLength(WorkOrderTask.TitleMaxLength);
    }
}

internal sealed record TemplateResponse(Guid Id, string Name, string? Title, string? Description, string Priority, int? DueInDays, IReadOnlyList<string> Tasks);

internal sealed class TemplatesHandler(JobsDbContext db)
{
    public static readonly Error NotFound = Error.NotFound("jobs.template.not_found", "No such work order template.");

    public async Task<IReadOnlyList<TemplateResponse>> ListAsync(CancellationToken cancellationToken) =>
        (await db.Templates.AsNoTracking().OrderBy(template => template.Name).ToListAsync(cancellationToken)).Select(ToResponse).ToList();

    public async Task<TemplateResponse> CreateAsync(TemplateCommand command, CancellationToken cancellationToken)
    {
        var template = WorkOrderTemplate.Create(Details(command));
        db.Templates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(template);
    }

    public async Task<Result<TemplateResponse>> UpdateAsync(Guid id, TemplateCommand command, CancellationToken cancellationToken)
    {
        var templateId = WorkOrderTemplateId.From(id);
        var template = await db.Templates.FirstOrDefaultAsync(candidate => candidate.Id == templateId, cancellationToken);
        if (template is null)
        {
            return NotFound;
        }

        template.Update(Details(command));
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(template);
    }

    public async Task<Result<bool>> RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        var templateId = WorkOrderTemplateId.From(id);
        var template = await db.Templates.FirstOrDefaultAsync(candidate => candidate.Id == templateId, cancellationToken);
        if (template is null)
        {
            return NotFound;
        }

        db.Templates.Remove(template);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static WorkOrderTemplateDetails Details(TemplateCommand command) =>
        new(command.Name, command.Title, command.Description, JobsApi.ParsePriority(command.Priority), command.DueInDays, command.Tasks ?? []);

    public static TemplateResponse ToResponse(WorkOrderTemplate template) => new(
        template.Id.Value, template.Name, template.Title, template.Description, JobsApi.Priority(template.Priority), template.DueInDays, template.Tasks);
}
