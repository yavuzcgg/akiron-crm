using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Web;
using Akiron.Modules.Crm.Domain;
using Akiron.Modules.Crm.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Crm.Features.CustomFields;

internal sealed record CustomFieldCommand(string Label, string Type, IReadOnlyList<string>? Options = null, bool IsRequired = false);

internal sealed class CustomFieldValidator : AbstractValidator<CustomFieldCommand>
{
    public static readonly string[] Types = ["text", "number", "date", "select", "checkbox"];

    public CustomFieldValidator()
    {
        RuleFor(command => command.Label).NotEmpty().MaximumLength(CustomField.LabelMaxLength);
        RuleFor(command => command.Type).Must(type => Types.Contains(type)).WithErrorCode("validation.invalid_value");
        RuleFor(command => command.Options).NotEmpty().When(command => command.Type == "select")
            .WithErrorCode("crm.custom_field.options_required").WithMessage("A choice field needs its choices.");
        RuleFor(command => command.Options!.Count).LessThanOrEqualTo(CustomField.MaxOptions).When(command => command.Options is not null)
            .OverridePropertyName(nameof(CustomFieldCommand.Options));
        RuleForEach(command => command.Options).MaximumLength(100);
    }
}

internal sealed record ReorderCustomFieldsCommand(IReadOnlyList<Guid> FieldIds);

internal sealed class ReorderCustomFieldsValidator : AbstractValidator<ReorderCustomFieldsCommand>
{
    public ReorderCustomFieldsValidator() => RuleFor(command => command.FieldIds).NotEmpty();
}

internal sealed record CustomFieldResponse(Guid Id, string Key, string Label, string Type, IReadOnlyList<string> Options, bool IsRequired, int Position);

/// <summary>The tenant's party card fields: add, relabel, reorder, remove (values are kept).</summary>
internal sealed class CustomFieldsHandler(CrmDbContext db)
{
    public async Task<IReadOnlyList<CustomFieldResponse>> ListAsync(CancellationToken cancellationToken) =>
        (await db.CustomFields.AsNoTracking().OrderBy(field => field.Position).ToListAsync(cancellationToken)).Select(ToResponse).ToList();

    public async Task<CustomFieldResponse> CreateAsync(CustomFieldCommand command, CancellationToken cancellationToken)
    {
        // Keys stay taken after a field is removed, so its old values never attach to a new field.
        var taken = await db.CustomFields.IncludingArchived().Select(field => field.Key).ToListAsync(cancellationToken);
        var baseKey = CustomField.KeyFrom(command.Label);
        var key = baseKey;
        for (var suffix = 2; taken.Contains(key); suffix++)
        {
            key = $"{baseKey}_{suffix}";
        }

        var position = await db.CustomFields.CountAsync(cancellationToken);
        var field = CustomField.Create(key, Enum.Parse<CustomFieldType>(command.Type, ignoreCase: true), command.Label, command.Options ?? [], command.IsRequired, position);
        db.CustomFields.Add(field);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(field);
    }

    /// <summary>The type is fixed: changing it would silently invalidate stored values.</summary>
    public async Task<Result<CustomFieldResponse>> UpdateAsync(Guid id, CustomFieldCommand command, CancellationToken cancellationToken)
    {
        var fieldId = CustomFieldId.From(id);
        var field = await db.CustomFields.FirstOrDefaultAsync(candidate => candidate.Id == fieldId, cancellationToken);
        if (field is null)
        {
            return CrmErrors.CustomFieldNotFound;
        }

        field.Update(command.Label, command.Options ?? [], command.IsRequired);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(field);
    }

    public async Task<Result<bool>> RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        var fieldId = CustomFieldId.From(id);
        var field = await db.CustomFields.FirstOrDefaultAsync(candidate => candidate.Id == fieldId, cancellationToken);
        if (field is null)
        {
            return CrmErrors.CustomFieldNotFound;
        }

        db.CustomFields.Remove(field);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Result<IReadOnlyList<CustomFieldResponse>>> ReorderAsync(ReorderCustomFieldsCommand command, CancellationToken cancellationToken)
    {
        var fields = await db.CustomFields.ToListAsync(cancellationToken);
        if (command.FieldIds.Count != fields.Count || command.FieldIds.Distinct().Count() != fields.Count
            || fields.Any(field => !command.FieldIds.Contains(field.Id.Value)))
        {
            return CrmErrors.CustomFieldOrderMismatch;
        }

        var ordered = command.FieldIds.Select(id => fields.First(field => field.Id.Value == id)).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].MoveTo(index);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ordered.Select(ToResponse).ToList();
    }

    private static CustomFieldResponse ToResponse(CustomField field) =>
        new(field.Id.Value, field.Key, field.Label, field.Type.ToString().ToLowerInvariant(), field.Options, field.IsRequired, field.Position);
}

/// <summary>Checks submitted custom values against the tenant's fields and returns what to store.</summary>
internal sealed class CustomValuesCheck(CrmDbContext db)
{
    /// <param name="submitted">Null keeps <paramref name="current"/> unchanged.</param>
    /// <param name="current">Values of removed fields are kept as they were.</param>
    public async Task<Result<IReadOnlyDictionary<string, string>>> CheckAsync(
        IReadOnlyDictionary<string, string?>? submitted,
        IReadOnlyDictionary<string, string> current,
        CancellationToken cancellationToken)
    {
        var fields = await db.CustomFields.AsNoTracking().ToListAsync(cancellationToken);
        if (submitted is null)
        {
            var missing = fields.FirstOrDefault(field => field.IsRequired && !current.ContainsKey(field.Key));
            if (missing is not null)
            {
                return CrmErrors.CustomFieldRequired(missing.Label);
            }

            return new Dictionary<string, string>(current, StringComparer.Ordinal);
        }

        var byKey = fields.ToDictionary(field => field.Key, StringComparer.Ordinal);
        if (submitted.Keys.FirstOrDefault(key => !byKey.ContainsKey(key)) is { } unknown)
        {
            return CrmErrors.CustomFieldUnknown(unknown);
        }

        var result = current.Where(pair => !byKey.ContainsKey(pair.Key)).ToDictionary(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (!field.TryNormalize(submitted.GetValueOrDefault(field.Key), out var value))
            {
                return CrmErrors.CustomFieldInvalid(field.Label);
            }

            if (value is null)
            {
                if (field.IsRequired)
                {
                    return CrmErrors.CustomFieldRequired(field.Label);
                }

                continue;
            }

            result[field.Key] = value;
        }

        return result;
    }
}

internal static class CustomFieldEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var fields = endpoints.MapGroup("/custom-fields");

        fields.MapGet("/", async (CustomFieldsHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.ListAsync(cancellationToken)))
            .RequirePermission(CrmPermissions.PartiesRead)
            .Produces<IReadOnlyList<CustomFieldResponse>>()
            .WithSummary("The custom fields of party cards, in order");

        fields.MapPost("/", async (CustomFieldCommand command, CustomFieldsHandler handler, CancellationToken cancellationToken) =>
            {
                var field = await handler.CreateAsync(command, cancellationToken);
                return TypedResults.Created($"/api/v1/crm/custom-fields/{field.Id}", field);
            })
            .RequirePermission(CrmPermissions.CustomFieldsManage)
            .Validate<CustomFieldCommand>()
            .Produces<CustomFieldResponse>(StatusCodes.Status201Created)
            .WithSummary("Add a field to every party card");

        fields.MapPut("/{id:guid}", async (Guid id, CustomFieldCommand command, CustomFieldsHandler handler, CancellationToken cancellationToken) =>
                ToResult(await handler.UpdateAsync(id, command, cancellationToken)))
            .RequirePermission(CrmPermissions.CustomFieldsManage)
            .Validate<CustomFieldCommand>()
            .Produces<CustomFieldResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Relabel a field, change its choices or whether it is required (not its type)");

        fields.MapDelete("/{id:guid}", async (Guid id, CustomFieldsHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.RemoveAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
            })
            .RequirePermission(CrmPermissions.CustomFieldsManage)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove a field from the cards; stored values are kept");

        fields.MapPut("/order", async (ReorderCustomFieldsCommand command, CustomFieldsHandler handler, CancellationToken cancellationToken) =>
                ToResult(await handler.ReorderAsync(command, cancellationToken)))
            .RequirePermission(CrmPermissions.CustomFieldsManage)
            .Validate<ReorderCustomFieldsCommand>()
            .Produces<IReadOnlyList<CustomFieldResponse>>()
            .WithSummary("Put the fields in a new order");
    }

    private static IResult ToResult<T>(Result<T> result) => result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
}
