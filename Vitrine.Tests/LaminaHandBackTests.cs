using Vitrine.Lamina;
using Xunit;

namespace Vitrine.Tests;

/// <summary> In-memory Lamina link for tests. </summary>
public sealed class FakeLaminaLink : ILaminaLink
{
    public bool Available { get; set; } = true;
    public (int Major, int Minor)? ApiVersion { get; set; } = (1, 0);
    public bool ReapplyResult { get; set; } = true;
    public int ReapplyCalls { get; private set; }

    public bool Reapply()
    {
        ReapplyCalls++;
        return ReapplyResult;
    }
}

public class LaminaHandBackTests
{
    [Fact]
    public void HandsBack_WhenSettingOnAndLaminaAvailable()
    {
        var link = new FakeLaminaLink();
        var hb   = new LaminaHandBack(link, () => true);

        Assert.Equal(LaminaHandBack.Outcome.Reapplied, hb.TryHandBack());
        Assert.Equal(1, link.ReapplyCalls);
        Assert.Equal("Preview ended; Lamina re-applied.", hb.Append("Preview ended."));
    }

    [Fact]
    public void DoesNotContactLamina_WhenSettingOff()
    {
        var link = new FakeLaminaLink();
        var hb   = new LaminaHandBack(link, () => false);

        Assert.Equal(LaminaHandBack.Outcome.Disabled, hb.TryHandBack());
        Assert.Equal(0, link.ReapplyCalls);
        Assert.Equal("Preview ended.", hb.Append("Preview ended"));
    }

    [Fact]
    public void ReportsNotInstalled_WhenLaminaUnavailable()
    {
        var link = new FakeLaminaLink { Available = false };
        var hb   = new LaminaHandBack(link, () => true);

        Assert.Equal(LaminaHandBack.Outcome.NotInstalled, hb.TryHandBack());
        Assert.Equal(0, link.ReapplyCalls);
        Assert.Equal("Preview ended; Lamina not installed.", hb.Append("Preview ended."));
    }

    [Fact]
    public void ReportsFailure_WhenReapplyThrowsInsideLamina()
    {
        var link = new FakeLaminaLink { ReapplyResult = false };
        var hb   = new LaminaHandBack(link, () => true);

        Assert.Equal(LaminaHandBack.Outcome.Failed, hb.TryHandBack());
        Assert.Contains("/lam reapply", hb.Append("All previews removed."));
    }
}
