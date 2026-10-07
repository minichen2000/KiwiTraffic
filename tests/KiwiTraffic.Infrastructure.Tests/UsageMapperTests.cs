using KiwiTraffic.Infrastructure.KiwiVm;

namespace KiwiTraffic.Infrastructure.Tests;

/// <summary>
/// Contract tests built from synthetic responses that follow the confirmed
/// KiwiVM contract (docs/api-contract.md). No real account is involved.
/// </summary>
public class UsageMapperTests
{
    private const string ProfileId = "veid:12345";
    private const long Veid = 12345L;

    private static readonly DateTimeOffset FetchedAt = new(2026, 10, 7, 8, 0, 0, TimeSpan.Zero);

    private static ServiceInfoResponse Response(
        decimal? dataCounter = 100m,
        decimal? planMonthlyData = 1000m,
        decimal? multiplier = 1m,
        long? nextReset = null,
        long? error = null,
        string? message = null) => new()
        {
            DataCounter = dataCounter,
            PlanMonthlyData = planMonthlyData,
            MonthlyDataMultiplier = multiplier,
            DataNextReset = nextReset,
            Error = error,
            Message = message,
        };

    private static KiwiVmQueryResult Map(
        ServiceInfoResponse response,
        MultiplierPolicy policy = MultiplierPolicy.ScaleBoth) =>
        new UsageMapper(policy).Map(response, ProfileId, Veid, FetchedAt);

    // --- success path ---------------------------------------------------------

    [Fact]
    public void HealthyResponse_ProducesASnapshot()
    {
        var result = Map(Response(dataCounter: 250m, planMonthlyData: 1000m));

        Assert.True(result.IsSuccess);
        var snapshot = result.Snapshot!;
        Assert.Equal(ProfileId, snapshot.ProfileId);
        Assert.Equal(Veid, snapshot.Veid);
        Assert.Equal(250m, snapshot.UsedBytes);
        Assert.Equal(1000m, snapshot.QuotaBytes);
        Assert.Equal(FetchedAt, snapshot.FetchedAtUtc);
        Assert.Equal(25m, snapshot.ToUsage().UsedPercent);
    }

    [Fact]
    public void MissingErrorField_IsTreatedAsSuccess()
    {
        // Older responses may omit error entirely; absent is not the same as
        // a non-zero code.
        var result = Map(Response(error: null));

        Assert.True(result.IsSuccess);
    }

    // --- business errors: HTTP 200 is not success -----------------------------

    [Fact]
    public void AuthenticationErrorCode_IsClassifiedAsAuthentication()
    {
        var result = Map(Response(error: 700005, message: "Authentication failure"));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Snapshot);
        Assert.Equal(KiwiVmErrorKind.Authentication, result.ErrorKind);
        Assert.Equal(700005L, result.ApiErrorCode);
        Assert.Equal("Authentication failure", result.ErrorMessage);
    }

    [Fact]
    public void UnknownErrorCode_IsABusinessErrorCarryingTheProviderMessage()
    {
        var result = Map(Response(error: 788888, message: "VE is currently locked, try again in a few minutes"));

        Assert.False(result.IsSuccess);
        Assert.Equal(KiwiVmErrorKind.Business, result.ErrorKind);
        Assert.Contains("VE is currently locked", result.ErrorMessage!);
    }

    [Fact]
    public void ErrorWithoutMessage_StillProducesAUsableMessage()
    {
        var result = Map(Response(error: 999999, message: null));

        Assert.Equal(KiwiVmErrorKind.Business, result.ErrorKind);
        Assert.Contains("999999", result.ErrorMessage!);
    }

    // --- invalid readings never become snapshots ------------------------------

    [Fact]
    public void MissingCounter_IsInvalid()
    {
        var result = Map(Response(dataCounter: null));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Snapshot);
        Assert.Equal(KiwiVmErrorKind.InvalidResponse, result.ErrorKind);
    }

    [Fact]
    public void NegativeCounter_IsInvalid()
    {
        var result = Map(Response(dataCounter: -1m));

        Assert.Equal(KiwiVmErrorKind.InvalidResponse, result.ErrorKind);
        Assert.Null(result.Snapshot);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    public void UnusableMultiplier_IsInvalidRatherThanAssumedToBeOne(double? raw)
    {
        // Absent or nonsensical multiplier means we cannot know the real
        // accounting. Guessing 1 would silently produce a wrong percentage.
        var result = Map(Response(multiplier: raw is null ? null : (decimal)raw.Value));

        Assert.Equal(KiwiVmErrorKind.InvalidResponse, result.ErrorKind);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public void NegativeMultiplier_IsInvalid()
    {
        var result = Map(Response(multiplier: -0.5m));

        Assert.Equal(KiwiVmErrorKind.InvalidResponse, result.ErrorKind);
    }

    [Fact]
    public void OverflowDuringScaling_IsInvalidInsteadOfThrowing()
    {
        var result = Map(Response(dataCounter: decimal.MaxValue, multiplier: 1000m));

        Assert.Equal(KiwiVmErrorKind.InvalidResponse, result.ErrorKind);
        Assert.Null(result.Snapshot);
    }

    // --- unknown quota is a state, not a failure ------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void MissingZeroOrNegativeQuota_YieldsUnknownQuota_ButStillSucceeds(double? raw)
    {
        var result = Map(Response(planMonthlyData: raw is null ? null : (decimal)raw.Value));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Snapshot!.QuotaBytes);

        // Used traffic is still truthful and displayable.
        Assert.Equal(100m, result.Snapshot.UsedBytes);
        Assert.False(result.Snapshot.ToUsage().IsQuotaKnown);
    }

    // --- reset time -----------------------------------------------------------

    [Fact]
    public void UnixResetTimestamp_IsMappedToUtc()
    {
        var epoch = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var result = Map(Response(nextReset: epoch.ToUnixTimeSeconds()));

        Assert.Equal(epoch, result.Snapshot!.NextResetAtUtc);
        Assert.Equal(TimeSpan.Zero, result.Snapshot.NextResetAtUtc!.Value.Offset);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(253402300800L)] // one second past what DateTimeOffset can hold
    public void MissingOrNonsensicalResetTimestamp_IsUnknown_NotAFailure(long? raw)
    {
        var result = Map(Response(nextReset: raw));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Snapshot!.NextResetAtUtc);
    }

    // --- the multiplier policy ------------------------------------------------

    [Fact]
    public void ScaleBoth_CancelsTheMultiplierInThePercentage()
    {
        var result = Map(Response(dataCounter: 300m, planMonthlyData: 1000m, multiplier: 0.5m), MultiplierPolicy.ScaleBoth);

        Assert.Equal(150m, result.Snapshot!.UsedBytes);
        Assert.Equal(500m, result.Snapshot.QuotaBytes);
        Assert.Equal(30m, result.Snapshot.ToUsage().UsedPercent);
    }

    [Fact]
    public void ScaleQuotaOnly_ShrinksTheEffectiveQuotaForTheSameCounter()
    {
        var result = Map(Response(dataCounter: 300m, planMonthlyData: 1000m, multiplier: 0.5m), MultiplierPolicy.ScaleQuotaOnly);

        Assert.Equal(300m, result.Snapshot!.UsedBytes);
        Assert.Equal(500m, result.Snapshot.QuotaBytes);
        Assert.Equal(60m, result.Snapshot.ToUsage().UsedPercent);
    }

    [Fact]
    public void BothPolicies_AgreeOnTheQuota_SoTheQuotaCannotDisambiguateThem()
    {
        // This is exactly why the open question in docs/api-contract.md can only
        // be settled by looking at the used figure the panel shows.
        var response = Response(dataCounter: 300m, planMonthlyData: 1000m, multiplier: 0.5m);

        var both = Map(response, MultiplierPolicy.ScaleBoth).Snapshot!;
        var quotaOnly = Map(response, MultiplierPolicy.ScaleQuotaOnly).Snapshot!;

        Assert.Equal(both.QuotaBytes, quotaOnly.QuotaBytes);
        Assert.NotEqual(both.UsedBytes, quotaOnly.UsedBytes);
        Assert.NotEqual(both.ToUsage().UsedPercent, quotaOnly.ToUsage().UsedPercent);
    }

    [Fact]
    public void UnitMultiplier_MakesBothPoliciesIdentical()
    {
        var response = Response(dataCounter: 300m, planMonthlyData: 1000m, multiplier: 1m);

        var both = Map(response, MultiplierPolicy.ScaleBoth).Snapshot!;
        var quotaOnly = Map(response, MultiplierPolicy.ScaleQuotaOnly).Snapshot!;

        Assert.Equal(both.UsedBytes, quotaOnly.UsedBytes);
        Assert.Equal(both.ToUsage().UsedPercent, quotaOnly.ToUsage().UsedPercent);
    }

    // --- no failure path leaks a snapshot -------------------------------------

    [Fact]
    public void Failures_NeverCarryASnapshot()
    {
        var failures = new[]
        {
            Map(Response(error: 700005)),
            Map(Response(error: 42)),
            Map(Response(dataCounter: null)),
            Map(Response(dataCounter: -5m)),
            Map(Response(multiplier: null)),
        };

        Assert.All(failures, failure =>
        {
            Assert.False(failure.IsSuccess);
            Assert.Null(failure.Snapshot);
            Assert.False(string.IsNullOrWhiteSpace(failure.ErrorMessage));
        });
    }
}
