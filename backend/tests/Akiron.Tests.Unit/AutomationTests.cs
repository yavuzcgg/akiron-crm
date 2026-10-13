using Akiron.BuildingBlocks.Sms;
using Akiron.Modules.Automation.Domain;

namespace Akiron.Tests.Unit;

public sealed class AutomationTests
{
    [Fact]
    public void Render_FillsKnownVariables_AndLeavesTyposVisible()
    {
        var variables = new Dictionary<string, string> { ["party"] = "Mavi Kahve", ["number"] = "TKL-2026-0001" };

        var text = MessageTemplate.Render("Merhaba {{party}}, {{NUMBER}} onaylandı. {{tutar}}", variables);

        Assert.Equal("Merhaba Mavi Kahve, TKL-2026-0001 onaylandı. {{tutar}}", text);
    }

    [Theory]
    [InlineData("0532 123 45 67", "+905321234567")]
    [InlineData("+90 (532) 123-45-67", "+905321234567")]
    [InlineData("5321234567", "+905321234567")]
    [InlineData("905321234567", "+905321234567")]
    [InlineData("00905321234567", "+905321234567")]
    public void Phone_IsNormalizedFromTheWaysPeopleTypeIt(string typed, string expected)
    {
        Assert.True(TurkishPhone.TryNormalize(typed, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("0212 123 45 67")] // a land line cannot receive SMS
    [InlineData("532 123 45")]
    [InlineData("")]
    [InlineData(null)]
    public void Phone_ThatIsNotATurkishMobile_IsRefused(string? typed) =>
        Assert.False(TurkishPhone.TryNormalize(typed, out _));
}
