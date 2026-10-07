using KiwiTraffic.Core.Model;

namespace KiwiTraffic.Infrastructure.KiwiVm;

/// <summary>
/// Turns a raw <see cref="ServiceInfoResponse"/> into a validated
/// <see cref="TrafficSnapshot"/>.
/// </summary>
/// <remarks>
/// This is the only place where units and the multiplier are handled. Anything
/// that cannot be established from the confirmed contract fails here rather
/// than being guessed - a rejected response never becomes a snapshot, so the
/// previous reading survives.
/// </remarks>
public sealed class UsageMapper
{
    /// <summary>The highest Unix timestamp <see cref="DateTimeOffset"/> can represent.</summary>
    private const long MaxUnixSeconds = 253402300799L;

    /// <summary>The documented API error code for a rejected veid/api_key pair.</summary>
    private const long AuthenticationFailureCode = 700005L;

    private readonly MultiplierPolicy _policy;

    public UsageMapper(MultiplierPolicy policy = MultiplierPolicy.ScaleBoth)
    {
        _policy = policy;
    }

    public MultiplierPolicy Policy => _policy;

    /// <summary>
    /// Validates and maps one response. On failure the caller must keep the
    /// previous snapshot and surface <see cref="KiwiVmQueryResult.ErrorMessage"/>.
    /// </summary>
    public KiwiVmQueryResult Map(
        ServiceInfoResponse response,
        string profileId,
        long veid,
        DateTimeOffset fetchedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(response);

        // HTTP 200 is not business success.
        if (response.Error is { } errorCode && errorCode != 0)
        {
            var kind = errorCode == AuthenticationFailureCode
                ? KiwiVmErrorKind.Authentication
                : KiwiVmErrorKind.Business;

            var message = string.IsNullOrWhiteSpace(response.Message)
                ? $"KiwiVM 返回错误码 {errorCode}。"
                : response.Message;

            return KiwiVmQueryResult.Failure(kind, message, errorCode);
        }

        if (response.DataCounter is not { } counter)
        {
            return Invalid("响应缺少 data_counter。");
        }

        if (counter < 0m)
        {
            return Invalid($"data_counter 为负值（{counter}）。");
        }

        // The contract lists monthly_data_multiplier as a regular field and it
        // is the only thing standing between us and a wrong percentage. A
        // missing or nonsensical value is an invalid reading, not "assume 1".
        if (response.MonthlyDataMultiplier is not { } multiplier || multiplier <= 0m)
        {
            return Invalid("响应缺少可用的 monthly_data_multiplier。");
        }

        if (!TryScale(counter, multiplier, out var scaledCounter))
        {
            return Invalid("已用量乘以倍率后超出可表示范围。");
        }

        var used = _policy == MultiplierPolicy.ScaleBoth ? scaledCounter : counter;

        decimal? quota = null;
        if (response.PlanMonthlyData is { } planMonthlyData)
        {
            if (planMonthlyData > 0m)
            {
                if (!TryScale(planMonthlyData, multiplier, out var scaledQuota))
                {
                    return Invalid("额度乘以倍率后超出可表示范围。");
                }

                quota = scaledQuota;
            }
            // Zero or negative means "no usable quota": leave it null so the UI
            // says "quota unknown" instead of dividing by it or calling it
            // unlimited.
        }

        var nextReset = TryMapResetTime(response.DataNextReset);

        return KiwiVmQueryResult.Success(new TrafficSnapshot
        {
            ProfileId = profileId,
            Veid = veid,
            UsedBytes = used,
            QuotaBytes = quota,
            NextResetAtUtc = nextReset,
            FetchedAtUtc = fetchedAtUtc,
        });
    }

    /// <summary>
    /// A missing or nonsensical reset timestamp yields <c>null</c> ("reset time
    /// unknown"), not an error: the rest of the reading is still useful. The
    /// counter is never zeroed just because the reset time has passed.
    /// </summary>
    private static DateTimeOffset? TryMapResetTime(long? unixSeconds)
    {
        if (unixSeconds is not { } value || value <= 0 || value > MaxUnixSeconds)
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(value).ToUniversalTime();
    }

    private static bool TryScale(decimal value, decimal factor, out decimal result)
    {
        try
        {
            result = value * factor;
            return true;
        }
        catch (OverflowException)
        {
            result = 0m;
            return false;
        }
    }

    private static KiwiVmQueryResult Invalid(string message) =>
        KiwiVmQueryResult.Failure(KiwiVmErrorKind.InvalidResponse, message);
}
