using System.Security.Cryptography;
using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Contracts.Crm;
using Akiron.Contracts.Identity;
using Akiron.Modules.Crm.Domain;
using Akiron.Modules.Crm.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Crm.Features.Leads;

internal sealed record LeadFormCommand(bool Enabled, string? RedirectUrl = null, Guid? OwnerUserId = null);

internal sealed class LeadFormValidator : AbstractValidator<LeadFormCommand>
{
    public LeadFormValidator() =>
        RuleFor(command => command.RedirectUrl!)
            .MaximumLength(500)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            .WithErrorCode("validation.invalid_value")
            .When(command => !string.IsNullOrWhiteSpace(command.RedirectUrl));
}

/// <param name="Configured">False until someone saves the form settings once; the key exists from then on.</param>
internal sealed record LeadFormResponse(bool Configured, string? Key, bool Enabled, string? RedirectUrl, Guid? OwnerUserId, string? OwnerName);

/// <summary>What a visitor sends through the public form; all optional, but someone has to be reachable.</summary>
internal sealed record WebLead(string? Name, string? Email, string? Phone, string? Company, string? Message, string? Honeypot);

/// <summary>The tenant's web form settings: on/off, where visitors land, who gets the leads, and the embed key.</summary>
internal sealed class LeadFormsHandler(CrmDbContext db, IMemberDirectory members)
{
    public async Task<LeadFormResponse> GetAsync(CancellationToken cancellationToken)
    {
        var form = await db.LeadForms.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return ToResponse(form);
    }

    public async Task<Result<LeadFormResponse>> SaveAsync(LeadFormCommand command, CancellationToken cancellationToken)
    {
        string? ownerName = null;
        if (command.OwnerUserId is { } ownerId)
        {
            var found = await members.FindAsync([ownerId], cancellationToken);
            if (!found.TryGetValue(ownerId, out var owner))
            {
                return LeadErrors.OwnerNotMember;
            }

            ownerName = owner.FullName;
        }

        var form = await db.LeadForms.FirstOrDefaultAsync(cancellationToken);
        if (form is null)
        {
            form = LeadForm.Create(NewKey());
            db.LeadForms.Add(form);
        }

        form.Configure(command.Enabled, command.RedirectUrl, command.OwnerUserId, ownerName);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(form);
    }

    /// <summary>A new key: forms embedded with the old one stop working.</summary>
    public async Task<Result<LeadFormResponse>> RotateAsync(CancellationToken cancellationToken)
    {
        var form = await db.LeadForms.FirstOrDefaultAsync(cancellationToken);
        if (form is null)
        {
            return Error.NotFound("crm.lead_form.not_found", "Save the form settings first.");
        }

        form.Rotate(NewKey());
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(form);
    }

    internal static string NewKey() => RandomNumberGenerator.GetHexString(32, lowercase: true);

    private static LeadFormResponse ToResponse(LeadForm? form) =>
        form is null
            ? new LeadFormResponse(false, null, false, null, null, null)
            : new LeadFormResponse(true, form.Key, form.Enabled, form.RedirectUrl, form.OwnerUserId, form.OwnerName);
}

/// <summary>
/// The public side of the web form: no account, the key names the tenant. The form is found by its
/// key across tenants, then the scope is bound to that tenant before anything is written (ADR-0002).
/// </summary>
internal sealed class PublicLeadFormHandler(CrmDbContext db, ITenantContext tenantContext, TimeProvider timeProvider)
{
    public const int MaxField = 200;
    public const int MaxMessage = 2000;

    /// <returns>Where to send the visitor (null: Akiron's thank-you page), or an error for an unknown or disabled form.</returns>
    public async Task<Result<string?>> CaptureAsync(string key, WebLead input, CancellationToken cancellationToken)
    {
        if (key.Length != 32)
        {
            return LeadFormErrors.Invalid;
        }

        var form = await db.LeadForms.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Key == key && candidate.Enabled, cancellationToken);
        if (form is null)
        {
            return LeadFormErrors.Invalid;
        }

        tenantContext.Bind(form.TenantId);

        // Bots fill every field, people never see this one: pretend it worked and keep nothing.
        if (!string.IsNullOrWhiteSpace(input.Honeypot))
        {
            return form.RedirectUrl;
        }

        var name = Clip(input.Name, MaxField);
        var email = Clip(input.Email, 254);
        var phone = Clip(input.Phone, 30);
        var company = Clip(input.Company, MaxField);
        if (name is null && email is null && phone is null)
        {
            return Error.Validation("crm.lead_form.contact_required", "Leave a name, an e-mail address or a phone number.");
        }

        if (email is not null && !System.Net.Mail.MailAddress.TryCreate(email, out _))
        {
            email = null;
        }

        var others = await db.Leads.Where(lead => lead.Stage == LeadStage.New).OrderBy(lead => lead.Rank).Take(500).ToListAsync(cancellationToken);
        var lead = Lead.Create(
            new LeadDetails(
                Clip(company ?? name ?? email ?? phone, Lead.TitleMaxLength)!,
                company,
                name,
                email,
                phone,
                LeadSource.WebForm,
                null,
                Currency.Try,
                null,
                form.OwnerUserId,
                form.OwnerName,
                null,
                null,
                Clip(input.Message, MaxMessage)),
            partyId: null,
            LeadRanks.At(others, null));
        db.Leads.Add(lead);
        db.Publish(new LeadCreated(
            form.TenantId, timeProvider.GetUtcNow(), lead.Id.Value, lead.Title, lead.CompanyName, lead.ContactName,
            LeadApi.Source(lead.Source), null, lead.OwnerUserId, null, null));
        await db.SaveChangesAsync(cancellationToken);
        return form.RedirectUrl;
    }

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}

internal static class LeadFormErrors
{
    public static readonly Error Invalid = Error.NotFound("crm.lead_form.invalid", "This form is not accepting submissions.");
}
