using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;

namespace Akiron.Contracts.Automation;

/// <summary>
/// A rule asked for someone on the team to be told. <paramref name="UserId"/> null means everyone.
/// </summary>
[IntegrationEventName("automation.notice")]
public sealed record AutomationNotice(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid RuleId,
    string RuleName,
    Guid? UserId,
    string Text,
    string? SubjectType,
    Guid? SubjectId) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>
/// A rule wrote to a client (<paramref name="Channel"/> is <c>email</c> or <c>sms</c>); kept on the
/// party's history so nobody wonders who sent it.
/// </summary>
[IntegrationEventName("automation.message.sent")]
public sealed record AutomationMessageSent(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid RuleId,
    string RuleName,
    Guid PartyId,
    string Channel,
    string Recipient) : IntegrationEvent(TenantId, OccurredAt);
