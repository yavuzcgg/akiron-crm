using System.Globalization;
using Akiron.BuildingBlocks.Email;
using Akiron.BuildingBlocks.Events;
using Akiron.BuildingBlocks.Sms;
using Akiron.Contracts;
using Akiron.Contracts.Automation;
using Akiron.Contracts.Crm;
using Akiron.Contracts.Finance;
using Akiron.Contracts.Identity;
using Akiron.Contracts.Jobs;
using Akiron.Contracts.Sales;
using Akiron.Contracts.Social;
using Akiron.Modules.Automation.Domain;
using Akiron.Modules.Automation.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Akiron.Modules.Automation.Features;

/// <summary>Something happened that rules may react to, reduced to what a rule needs: who it concerns and the words for its message.</summary>
internal sealed record AutomationEvent(
    string Trigger,
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid? PartyId,
    IReadOnlyDictionary<string, string> Variables,
    string? SubjectType = null,
    Guid? SubjectId = null);

/// <summary>
/// Runs the tenant's enabled rules for an event. Each rule fires at most once per event (a run row
/// per rule and event), so a redelivered event sends nothing twice. A rule that cannot act (no
/// client on the event, no address on the client) records why instead of failing the delivery.
/// </summary>
internal sealed class AutomationEngine(
    AutomationDbContext db,
    IPartyDirectory parties,
    IWorkspaceDirectory workspace,
    IEmailSender emailSender,
    ISmsSender smsSender,
    TimeProvider timeProvider)
{
    public async Task RunAsync(AutomationEvent automationEvent, CancellationToken cancellationToken)
    {
        var rules = await db.Rules.Where(rule => rule.Trigger == automationEvent.Trigger && rule.Enabled).ToListAsync(cancellationToken);
        if (rules.Count == 0)
        {
            return;
        }

        var variables = new Dictionary<string, string>(automationEvent.Variables, StringComparer.OrdinalIgnoreCase)
        {
            ["workspace"] = await workspace.CurrentNameAsync(cancellationToken),
        };

        // Not every event carries the client's name; rules may still say {{party}}.
        if (automationEvent.PartyId is { } partyId && string.IsNullOrEmpty(variables.GetValueOrDefault("party")))
        {
            variables["party"] = (await parties.FindAsync(partyId, cancellationToken))?.Name ?? string.Empty;
        }

        foreach (var rule in rules)
        {
            if (await db.Runs.AnyAsync(run => run.RuleId == rule.Id && run.EventId == automationEvent.EventId, cancellationToken))
            {
                continue;
            }

            var now = timeProvider.GetUtcNow();
            var (outcome, detail) = await ActAsync(rule, automationEvent, variables, now, cancellationToken);
            db.Runs.Add(AutomationRun.Of(rule, automationEvent.EventId, outcome, detail, now));
            if (outcome == RunOutcome.Done)
            {
                rule.Ran(now);
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: AutomationConstraints.RunUnique })
            {
                // A parallel delivery of the same event ran this rule a moment ago.
                db.ChangeTracker.Clear();
            }
        }
    }

    private async Task<(RunOutcome Outcome, string? Detail)> ActAsync(
        AutomationRule rule, AutomationEvent automationEvent, Dictionary<string, string> variables, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var body = MessageTemplate.Render(rule.Body, variables);
        var subject = MessageTemplate.Render(rule.Subject ?? rule.Name, variables);

        switch (rule.Action)
        {
            case AutomationCatalog.EmailClient:
                {
                    if (automationEvent.PartyId is not { } partyId)
                    {
                        return (RunOutcome.Skipped, "no_client");
                    }

                    var email = await parties.BillingEmailAsync(partyId, cancellationToken);
                    if (email is null)
                    {
                        return (RunOutcome.Skipped, "no_email");
                    }

                    await emailSender.SendAsync(new EmailMessage(email, subject, body), cancellationToken);
                    db.Publish(new AutomationMessageSent(rule.TenantId, now, rule.Id.Value, rule.Name, partyId, "email", email));
                    return (RunOutcome.Done, email);
                }

            case AutomationCatalog.SmsClient:
                {
                    if (automationEvent.PartyId is not { } partyId)
                    {
                        return (RunOutcome.Skipped, "no_client");
                    }

                    if (!TurkishPhone.TryNormalize(await parties.BillingPhoneAsync(partyId, cancellationToken), out var phone))
                    {
                        return (RunOutcome.Skipped, "no_mobile");
                    }

                    var text = body.Length <= TurkishPhone.MaxTextLength ? body : body[..TurkishPhone.MaxTextLength];
                    await smsSender.SendAsync(new SmsMessage(phone, text), cancellationToken);
                    db.Publish(new AutomationMessageSent(rule.TenantId, now, rule.Id.Value, rule.Name, partyId, "sms", phone));
                    return (RunOutcome.Done, phone);
                }

            case AutomationCatalog.EmailAddress:
                await emailSender.SendAsync(new EmailMessage(rule.Target!, subject, body), cancellationToken);
                return (RunOutcome.Done, rule.Target);

            case AutomationCatalog.NotifyUser:
            case AutomationCatalog.NotifyTeam:
                {
                    Guid? userId = rule.Action == AutomationCatalog.NotifyUser && Guid.TryParse(rule.Target, out var target) ? target : null;
                    db.Publish(new AutomationNotice(rule.TenantId, now, rule.Id.Value, rule.Name, userId, body, automationEvent.SubjectType, automationEvent.SubjectId));
                    return (RunOutcome.Done, null);
                }

            default:
                return (RunOutcome.Skipped, "unknown_action");
        }
    }
}

/// <summary>Maps the business events rules can start from onto <see cref="AutomationEvent"/>; one place to read what each trigger offers.</summary>
internal sealed class AutomationTriggers(AutomationEngine engine)
    : IIntegrationEventConsumer<QuoteDecided>,
        IIntegrationEventConsumer<WorkOrderMoved>,
        IIntegrationEventConsumer<ReceiptRecorded>,
        IIntegrationEventConsumer<LeadCreated>,
        IIntegrationEventConsumer<TicketOpened>,
        IIntegrationEventConsumer<ContentDecided>
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public Task HandleAsync(QuoteDecided integrationEvent, CancellationToken cancellationToken) =>
        engine.RunAsync(
            new AutomationEvent(
                integrationEvent.Accepted ? AutomationCatalog.QuoteAccepted : AutomationCatalog.QuoteRejected,
                integrationEvent.EventId,
                integrationEvent.OccurredAt,
                integrationEvent.PartyId,
                new Dictionary<string, string>
                {
                    ["party"] = integrationEvent.PartyName,
                    ["number"] = integrationEvent.Number,
                    ["title"] = integrationEvent.Title,
                    ["amount"] = Money(integrationEvent.GrandTotal, integrationEvent.Currency),
                    ["person"] = integrationEvent.DecidedByName,
                },
                Subjects.Quote,
                integrationEvent.QuoteId),
            cancellationToken);

    public Task HandleAsync(WorkOrderMoved integrationEvent, CancellationToken cancellationToken) =>
        !integrationEvent.Completed
            ? Task.CompletedTask
            : engine.RunAsync(
                new AutomationEvent(
                    AutomationCatalog.WorkOrderCompleted,
                    integrationEvent.EventId,
                    integrationEvent.OccurredAt,
                    integrationEvent.PartyId,
                    new Dictionary<string, string> { ["number"] = integrationEvent.Number, ["title"] = integrationEvent.Title },
                    Subjects.WorkOrder,
                    integrationEvent.WorkOrderId),
                cancellationToken);

    public Task HandleAsync(ReceiptRecorded integrationEvent, CancellationToken cancellationToken) =>
        engine.RunAsync(
            new AutomationEvent(
                AutomationCatalog.ReceiptRecorded,
                integrationEvent.EventId,
                integrationEvent.OccurredAt,
                integrationEvent.PartyId,
                new Dictionary<string, string>
                {
                    ["party"] = integrationEvent.PartyName,
                    ["number"] = integrationEvent.Number,
                    ["amount"] = Money(integrationEvent.Amount, integrationEvent.Currency),
                },
                Subjects.Party,
                integrationEvent.PartyId),
            cancellationToken);

    public Task HandleAsync(LeadCreated integrationEvent, CancellationToken cancellationToken) =>
        engine.RunAsync(
            new AutomationEvent(
                AutomationCatalog.LeadCreated,
                integrationEvent.EventId,
                integrationEvent.OccurredAt,
                integrationEvent.PartyId,
                new Dictionary<string, string>
                {
                    ["title"] = integrationEvent.Title,
                    ["company"] = integrationEvent.CompanyName ?? string.Empty,
                    ["person"] = integrationEvent.ContactName ?? string.Empty,
                    ["source"] = integrationEvent.Source,
                },
                Subjects.Lead,
                integrationEvent.LeadId),
            cancellationToken);

    public Task HandleAsync(TicketOpened integrationEvent, CancellationToken cancellationToken) =>
        engine.RunAsync(
            new AutomationEvent(
                AutomationCatalog.TicketOpened,
                integrationEvent.EventId,
                integrationEvent.OccurredAt,
                integrationEvent.PartyId,
                new Dictionary<string, string>
                {
                    ["party"] = integrationEvent.PartyName,
                    ["number"] = integrationEvent.Number,
                    ["title"] = integrationEvent.Subject,
                    ["person"] = integrationEvent.RequesterName,
                },
                Subjects.Ticket,
                integrationEvent.TicketId),
            cancellationToken);

    public Task HandleAsync(ContentDecided integrationEvent, CancellationToken cancellationToken) =>
        !integrationEvent.Approved || !integrationEvent.ByClient
            ? Task.CompletedTask
            : engine.RunAsync(
                new AutomationEvent(
                    AutomationCatalog.ContentApproved,
                    integrationEvent.EventId,
                    integrationEvent.OccurredAt,
                    integrationEvent.PartyId,
                    new Dictionary<string, string>
                    {
                        ["party"] = integrationEvent.PartyName,
                        ["title"] = integrationEvent.Title,
                        ["person"] = integrationEvent.DecidedByName,
                    },
                    Subjects.ContentPost,
                    integrationEvent.PostId),
                cancellationToken);

    private static string Money(decimal amount, string currency) => amount.ToString("N2", Turkish) + " " + currency;
}
