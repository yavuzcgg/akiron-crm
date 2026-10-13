using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Automation.Domain;

public readonly record struct AutomationRuleId(Guid Value) : ITypedId<AutomationRuleId>
{
    public static AutomationRuleId New() => new(Guid.CreateVersion7());

    public static AutomationRuleId From(Guid value) => new(value);
}

/// <summary>What a rule's "when" and "then" may be. Codes are stable; the web app names them.</summary>
public static class AutomationCatalog
{
    public const string QuoteAccepted = "quote.accepted";
    public const string QuoteRejected = "quote.rejected";
    public const string WorkOrderCompleted = "work_order.completed";
    public const string ReceiptRecorded = "receipt.recorded";
    public const string LeadCreated = "lead.created";
    public const string TicketOpened = "ticket.opened";
    public const string ContentApproved = "content.approved";

    public const string EmailClient = "email_client";
    public const string EmailAddress = "email_address";
    public const string SmsClient = "sms_client";
    public const string NotifyUser = "notify_user";
    public const string NotifyTeam = "notify_team";

    /// <summary>Each trigger with the variables its messages can use (<c>{{party}}</c>, <c>{{number}}</c> …).</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Triggers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        [QuoteAccepted] = ["workspace", "party", "number", "title", "amount", "person"],
        [QuoteRejected] = ["workspace", "party", "number", "title", "amount", "person"],
        [WorkOrderCompleted] = ["workspace", "party", "number", "title"],
        [ReceiptRecorded] = ["workspace", "party", "number", "amount"],
        [LeadCreated] = ["workspace", "title", "company", "person", "source"],
        [TicketOpened] = ["workspace", "party", "number", "title", "person"],
        [ContentApproved] = ["workspace", "party", "title", "person"],
    };

    public static readonly IReadOnlyList<string> Actions = [EmailClient, EmailAddress, SmsClient, NotifyUser, NotifyTeam];

    /// <summary>Actions that write to the client; they need a party on the event (a new lead has none).</summary>
    public static bool WritesToClient(string action) => action is EmailClient or SmsClient;

    public static bool NeedsSubject(string action) => action is EmailClient or EmailAddress;
}

/// <summary>What a person sets on a rule.</summary>
public sealed record RuleDefinition(string Name, string Trigger, string Action, string? Target, string? Subject, string Body, bool Enabled);

/// <summary>
/// "When this happens, do that" (otomasyon kuralı): one trigger, one action, a message with
/// variables. Deliberately flat: no conditions tree, no chains. A rule that needs more is a sign the
/// product should do that thing itself.
/// </summary>
public sealed class AutomationRule : Entity<AutomationRuleId>, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int NameMaxLength = 120;
    public const int SubjectMaxLength = 200;
    public const int BodyMaxLength = 4000;
    public const int TargetMaxLength = 254;

    private AutomationRule(AutomationRuleId id)
        : base(id)
    {
        Name = string.Empty;
        Trigger = string.Empty;
        Action = string.Empty;
        Body = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public string Name { get; private set; }

    public string Trigger { get; private set; }

    public string Action { get; private set; }

    /// <summary>An e-mail address (<c>email_address</c>) or a member's id (<c>notify_user</c>); unused otherwise.</summary>
    public string? Target { get; private set; }

    public string? Subject { get; private set; }

    public string Body { get; private set; }

    public bool Enabled { get; private set; }

    public int RunCount { get; private set; }

    public DateTimeOffset? LastRunAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static AutomationRule Create(RuleDefinition definition)
    {
        var rule = new AutomationRule(AutomationRuleId.New());
        rule.Update(definition);
        return rule;
    }

    public void Update(RuleDefinition definition)
    {
        Name = definition.Name.Trim();
        Trigger = definition.Trigger;
        Action = definition.Action;
        Target = string.IsNullOrWhiteSpace(definition.Target) ? null : definition.Target.Trim();
        Subject = string.IsNullOrWhiteSpace(definition.Subject) ? null : definition.Subject.Trim();
        Body = definition.Body.Trim();
        Enabled = definition.Enabled;
    }

    public void Ran(DateTimeOffset now)
    {
        RunCount++;
        LastRunAt = now;
    }
}

public enum RunOutcome
{
    /// <summary>The action was carried out.</summary>
    Done,

    /// <summary>Nothing to do it with: the event has no client, the client no address or mobile number.</summary>
    Skipped,
}

/// <summary>One firing of a rule for one event; unique per rule and event, so a redelivered event does nothing twice.</summary>
public sealed class AutomationRun : ITenantScoped
{
    private AutomationRun()
    {
        Trigger = string.Empty;
    }

    public long Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public AutomationRuleId RuleId { get; private set; }

    public Guid EventId { get; private set; }

    public string Trigger { get; private set; }

    public RunOutcome Outcome { get; private set; }

    /// <summary>Who it went to, or why it was skipped (a stable code such as <c>no_email</c>).</summary>
    public string? Detail { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static AutomationRun Of(AutomationRule rule, Guid eventId, RunOutcome outcome, string? detail, DateTimeOffset now) =>
        new() { RuleId = rule.Id, EventId = eventId, Trigger = rule.Trigger, Outcome = outcome, Detail = detail, OccurredAt = now };
}

/// <summary>Fills <c>{{name}}</c> placeholders; an unknown name stays as typed, so a typo is visible in the result.</summary>
public static class MessageTemplate
{
    public static string Render(string template, IReadOnlyDictionary<string, string> variables)
    {
        var result = template;
        foreach (var (name, value) in variables)
        {
            result = result.Replace("{{" + name + "}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
