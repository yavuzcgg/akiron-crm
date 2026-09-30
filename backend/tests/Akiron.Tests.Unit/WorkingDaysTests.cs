using System.Globalization;
using Akiron.Modules.People.Domain;

namespace Akiron.Tests.Unit;

public sealed class WorkingDaysTests
{
    [Theory]
    [InlineData("2027-03-01", "2027-03-05", 5)] // a plain week
    [InlineData("2026-10-26", "2026-10-30", 4)] // Republic Day on Thursday
    [InlineData("2026-11-07", "2026-11-08", 0)] // a weekend
    [InlineData("2026-12-28", "2027-01-04", 5)] // across New Year
    public void Between_SkipsWeekendsAndFixedHolidays(string start, string end, int expected) =>
        Assert.Equal(expected, WorkingDays.Between(DateOnly.Parse(start, CultureInfo.InvariantCulture), DateOnly.Parse(end, CultureInfo.InvariantCulture)));

    [Fact]
    public void Submit_HalfDay_IsHalfADayOnOneDate()
    {
        var request = LeaveRequest.Submit(Guid.NewGuid(), LeaveType.Annual, new DateOnly(2027, 3, 3), new DateOnly(2027, 3, 5), halfDay: true, note: null);

        Assert.Equal(0.5m, request.Days);
        Assert.Equal(request.StartDate, request.EndDate);
    }
}
