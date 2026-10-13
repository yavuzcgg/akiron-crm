using Akiron.Modules.Jobs.Domain;

namespace Akiron.Tests.Unit;

public sealed class TicketTests
{
    private static readonly DateTimeOffset Opened = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    private static Ticket FromClient(WorkOrderPriority priority) =>
        Ticket.Open("DST-2026-0001", Guid.NewGuid(), "Mavi Kahve", "Site açılmıyor", null, priority, fromClient: true, "Deniz Aksoy", Opened);

    [Theory]
    [InlineData(WorkOrderPriority.Urgent, 4)]
    [InlineData(WorkOrderPriority.High, 8)]
    [InlineData(WorkOrderPriority.Normal, 24)]
    [InlineData(WorkOrderPriority.Low, 72)]
    public void Open_PromisesAFirstAnswerByPriority(WorkOrderPriority priority, int hours) =>
        Assert.Equal(Opened.AddHours(hours), FromClient(priority).ResponseDueAt);

    [Fact]
    public void Overdue_IsTakenOnce_AndOnlyWhileNobodyAnswered()
    {
        var ticket = FromClient(WorkOrderPriority.Urgent);

        Assert.False(ticket.TakeOverdueIfDue(Opened.AddHours(3)));
        Assert.True(ticket.TakeOverdueIfDue(Opened.AddHours(5)));
        Assert.False(ticket.TakeOverdueIfDue(Opened.AddHours(6)));

        var answered = FromClient(WorkOrderPriority.Urgent);
        answered.StaffAnswered(Opened.AddHours(1));
        Assert.False(answered.TakeOverdueIfDue(Opened.AddHours(5)));
    }

    [Fact]
    public void Conversation_MovesTheTicket()
    {
        var ticket = FromClient(WorkOrderPriority.Normal);

        Assert.Equal(TicketStatus.Open, ticket.StaffAnswered(Opened.AddHours(1)));
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Null(ticket.ClientWrote(Opened.AddHours(2)));

        ticket.MoveTo(TicketStatus.Resolved, Opened.AddHours(3));
        Assert.NotNull(ticket.ResolvedAt);
        Assert.Equal(TicketStatus.Resolved, ticket.ClientWrote(Opened.AddHours(4)));
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Null(ticket.ResolvedAt);
    }

    [Fact]
    public void OpenedByStaff_CountsAsAnswered()
    {
        var ticket = Ticket.Open("DST-2026-0002", Guid.NewGuid(), "Mavi Kahve", "Telefonla aradı", null, WorkOrderPriority.High, fromClient: false, "Deniz Aksoy", Opened);

        Assert.Equal(Opened, ticket.FirstResponseAt);
        Assert.False(ticket.TakeOverdueIfDue(Opened.AddDays(3)));
    }
}
