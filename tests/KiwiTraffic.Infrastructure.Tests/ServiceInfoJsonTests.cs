using System.Text.Json;
using KiwiTraffic.Infrastructure.KiwiVm;

namespace KiwiTraffic.Infrastructure.Tests;

/// <summary>
/// KiwiVM does not keep its field types stable - the same field can arrive as
/// a JSON number or as a numeric string (docs/api-contract.md section 9).
/// </summary>
public class ServiceInfoJsonTests
{
    private static ServiceInfoResponse Parse(string json) =>
        JsonSerializer.Deserialize<ServiceInfoResponse>(json, KiwiVmJson.Options)!;

    [Fact]
    public void Numbers_AreReadAsNumbers()
    {
        var payload = Parse("""
            {"plan_monthly_data": 2147483648000, "data_counter": 123, "monthly_data_multiplier": 1, "error": 0}
            """);

        Assert.Equal(2147483648000m, payload.PlanMonthlyData);
        Assert.Equal(123m, payload.DataCounter);
        Assert.Equal(0L, payload.Error);
    }

    [Fact]
    public void NumericStrings_AreReadAsNumbers()
    {
        // Observed in the wild for plan_disk and friends; harmless to accept here.
        var payload = Parse("""
            {"plan_monthly_data": "2147483648000", "data_counter": "123", "monthly_data_multiplier": "1", "error": "0"}
            """);

        Assert.Equal(2147483648000m, payload.PlanMonthlyData);
        Assert.Equal(123m, payload.DataCounter);
        Assert.Equal(0L, payload.Error);
    }

    [Fact]
    public void MixedTypesInOnePayload_AreAllRead()
    {
        var payload = Parse("""
            {"plan_monthly_data": "2147483648000", "data_counter": 123, "monthly_data_multiplier": 0.33}
            """);

        Assert.Equal(2147483648000m, payload.PlanMonthlyData);
        Assert.Equal(123m, payload.DataCounter);
        Assert.Equal(0.33m, payload.MonthlyDataMultiplier);
    }

    [Fact]
    public void FractionalMultiplier_IsKeptExactly()
    {
        var payload = Parse("""{"monthly_data_multiplier": 0.33}""");

        Assert.Equal(0.33m, payload.MonthlyDataMultiplier);
    }

    [Fact]
    public void MissingFields_AreNull_NotZero()
    {
        var payload = Parse("""{"hostname": "example.invalid"}""");

        Assert.Null(payload.DataCounter);
        Assert.Null(payload.PlanMonthlyData);
        Assert.Null(payload.MonthlyDataMultiplier);
        Assert.Null(payload.DataNextReset);
        Assert.Null(payload.Error);
    }

    [Fact]
    public void ExplicitNulls_AreNull()
    {
        var payload = Parse("""{"data_counter": null, "monthly_data_multiplier": null}""");

        Assert.Null(payload.DataCounter);
        Assert.Null(payload.MonthlyDataMultiplier);
    }

    [Fact]
    public void UnmodelledFields_AreIgnored()
    {
        // The real response carries about forty fields.
        var payload = Parse("""
            {"plan_disk": "4294967296", "location_ipv6_ready": 1, "ve_status": "Running", "data_counter": 5}
            """);

        Assert.Equal(5m, payload.DataCounter);
    }

    [Fact]
    public void NonNumericString_IsRejected_NotSilentlyZero()
    {
        var json = """{"data_counter": "not-a-number"}""";

        Assert.Throws<JsonException>(() => Parse(json));
    }

    [Fact]
    public void EmptyString_IsRejected_NotSilentlyZero()
    {
        var json = """{"data_counter": ""}""";

        Assert.Throws<JsonException>(() => Parse(json));
    }

    [Fact]
    public void UnreadablePayloadAsAWhole_Throws()
    {
        Assert.Throws<JsonException>(() => Parse("<html>session timeout</html>"));
    }
}
