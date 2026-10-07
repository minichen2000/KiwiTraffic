using KiwiTraffic.Core.Calculation;

namespace KiwiTraffic.Core.Tests;

public class UsageCalculatorTests
{
    // --- the arithmetic itself ------------------------------------------------

    [Fact]
    public void Used250Of1000_Gives25PercentAnd750Remaining()
    {
        var usage = UsageCalculator.Calculate(usedBytes: 250m, quotaBytes: 1000m);

        Assert.True(usage.IsQuotaKnown);
        Assert.Equal(25m, usage.UsedPercent);
        Assert.Equal(750m, usage.RemainingBytes);
        Assert.Equal(25m, usage.ProgressValue);
    }

    [Fact]
    public void ZeroUsed_IsZeroPercent_NotUnknown()
    {
        var usage = UsageCalculator.Calculate(usedBytes: 0m, quotaBytes: 1000m);

        Assert.True(usage.IsQuotaKnown);
        Assert.Equal(0m, usage.UsedPercent);
        Assert.Equal(1000m, usage.RemainingBytes);
        Assert.Equal(0m, usage.ProgressValue);
    }

    [Fact]
    public void UsedEqualsQuota_IsExactly100Percent_AndNotOverQuota()
    {
        var usage = UsageCalculator.Calculate(usedBytes: 1000m, quotaBytes: 1000m);

        Assert.Equal(100m, usage.UsedPercent);
        Assert.Equal(0m, usage.RemainingBytes);
        Assert.Equal(100m, usage.ProgressValue);
        Assert.False(usage.IsOverQuota);
    }

    [Fact]
    public void OverQuota_KeepsTheRealPercentage_ButCapsTheBarAndFloorsRemaining()
    {
        var usage = UsageCalculator.Calculate(usedBytes: 1200m, quotaBytes: 1000m);

        // The text must stay truthful at 120 %; only the bar is clipped.
        Assert.Equal(120m, usage.UsedPercent);
        Assert.Equal(100m, usage.ProgressValue);
        Assert.Equal(0m, usage.RemainingBytes);
        Assert.True(usage.IsOverQuota);
    }

    [Fact]
    public void JustOverQuota_IsDetectedFromTheUnroundedValue()
    {
        var usage = UsageCalculator.Calculate(usedBytes: 1000.001m, quotaBytes: 1000m);

        Assert.True(usage.IsOverQuota);
        Assert.Equal(100m, usage.ProgressValue);
    }

    // --- precision: never round before the caller decides to -------------------

    [Fact]
    public void PercentageIsNotRoundedToDisplayPrecision()
    {
        var usage = UsageCalculator.Calculate(usedBytes: 1m, quotaBytes: 3m);

        var percent = usage.UsedPercent!.Value;
        Assert.True(percent > 33.3m && percent < 33.4m);
        Assert.NotEqual(33.3m, percent);
        Assert.NotEqual(33.33m, percent);

        // Below the cap the bar tracks the unrounded value exactly.
        Assert.Equal(percent, usage.ProgressValue);
    }

    [Fact]
    public void ThresholdComparison_UsesTheUnroundedValue()
    {
        // 0.799999999 of the quota - below the 80 % alert, and it must stay below
        // it even though it would round to 80.0 for display.
        var usage = UsageCalculator.Calculate(usedBytes: 799_999_999m, quotaBytes: 1_000_000_000m);

        Assert.Equal(79.9999999m, usage.UsedPercent);
        Assert.True(usage.UsedPercent < 80m);
        Assert.False(usage.IsOverQuota);
    }

    [Fact]
    public void LargeByteCounters_KeepExactPrecision()
    {
        // 1.5 TB of a 3 TB decimal quota.
        var usage = UsageCalculator.Calculate(usedBytes: 1_500_000_000_000m, quotaBytes: 3_000_000_000_000m);

        Assert.Equal(50m, usage.UsedPercent);
        Assert.Equal(1_500_000_000_000m, usage.RemainingBytes);
        Assert.Equal(50m, usage.ProgressValue);
    }

    [Fact]
    public void LargeCountersWithOddRatio_DoNotCollapseToOneHundredPercent()
    {
        var usage = UsageCalculator.Calculate(usedBytes: 999_999_999_999m, quotaBytes: 1_000_000_000_000m);

        Assert.Equal(99.9999999999m, usage.UsedPercent);
        Assert.False(usage.IsOverQuota);
        Assert.Equal(1m, usage.RemainingBytes);
    }

    // --- quota unknown: never divide, never pretend it is 0 % -------------------

    /// <summary>
    /// Attribute arguments cannot carry decimal literals, so the quotas are
    /// supplied through member data instead.
    /// </summary>
    public static TheoryData<decimal?> UnusableQuotas() => new() { null, 0m, -1m };

    [Theory]
    [MemberData(nameof(UnusableQuotas))]
    public void MissingZeroOrNegativeQuota_IsUnknownRatherThanZeroPercent(decimal? quota)
    {
        var usage = UsageCalculator.Calculate(usedBytes: 123m, quotaBytes: quota);

        Assert.False(usage.IsQuotaKnown);
        Assert.Null(usage.QuotaBytes);
        Assert.Null(usage.UsedPercent);
        Assert.Null(usage.RemainingBytes);

        // The bar must not claim "0 % used" either - it is simply empty.
        Assert.Equal(0m, usage.ProgressValue);

        // The used figure itself is still truthful and displayable.
        Assert.Equal(123m, usage.UsedBytes);
    }

    // --- invalid readings: fail loudly instead of fabricating a value -----------

    [Fact]
    public void NegativeUsage_IsRejectedByTry()
    {
        var ok = UsageCalculator.TryCalculate(usedBytes: -1m, quotaBytes: 1000m, out var usage);

        Assert.False(ok);
        Assert.Null(usage);
    }

    [Fact]
    public void NegativeUsage_ThrowsFromCalculate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UsageCalculator.Calculate(usedBytes: -1m, quotaBytes: 1000m));
    }

    [Fact]
    public void RatioTooLargeToRepresent_IsRejectedInsteadOfOverflowing()
    {
        var ok = UsageCalculator.TryCalculate(usedBytes: decimal.MaxValue, quotaBytes: 0.0000000000000000000000000001m, out var usage);

        Assert.False(ok);
        Assert.Null(usage);
    }

    [Fact]
    public void TryCalculate_SucceedsForAHealthyReading()
    {
        var ok = UsageCalculator.TryCalculate(usedBytes: 250m, quotaBytes: 1000m, out var usage);

        Assert.True(ok);
        Assert.NotNull(usage);
        Assert.Equal(25m, usage.UsedPercent);
    }
}
