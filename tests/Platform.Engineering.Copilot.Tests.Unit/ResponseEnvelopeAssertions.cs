using System.Text.Json;
using FluentAssertions;

namespace Platform.Engineering.Copilot.Tests.Unit;

/// <summary>
/// Custom FluentAssertions extensions for validating ResponseEnvelope schema.
/// </summary>
public static class ResponseEnvelopeAssertions
{
    /// <summary>
    /// Assert that a JSON string is a valid success ResponseEnvelope.
    /// </summary>
    public static void ShouldBeSuccessEnvelope(this string json, string? expectedToolName = null)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("success");
        root.TryGetProperty("data", out _).Should().BeTrue("envelope should have 'data' property");
        root.TryGetProperty("metadata", out var metadata).Should().BeTrue("envelope should have 'metadata' property");

        if (expectedToolName is not null)
        {
            metadata.GetProperty("toolName").GetString().Should().Be(expectedToolName);
        }

        metadata.TryGetProperty("executionTimeMs", out _).Should().BeTrue("metadata should have 'executionTimeMs'");
        metadata.TryGetProperty("timestamp", out _).Should().BeTrue("metadata should have 'timestamp'");
    }

    /// <summary>
    /// Assert that a JSON string is a valid error ResponseEnvelope.
    /// </summary>
    public static void ShouldBeErrorEnvelope(this string json, string? expectedErrorCode = null)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("error");
        root.TryGetProperty("error", out var error).Should().BeTrue("envelope should have 'error' property");
        error.TryGetProperty("errorCode", out var code).Should().BeTrue("error should have 'errorCode'");
        error.TryGetProperty("message", out _).Should().BeTrue("error should have 'message'");

        if (expectedErrorCode is not null)
        {
            code.GetString().Should().Be(expectedErrorCode);
        }
    }

    /// <summary>
    /// Assert that a JSON string has valid pagination info.
    /// </summary>
    public static void ShouldHavePagination(
        this string json,
        int? expectedPage = null,
        int? expectedTotalItems = null)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("pagination", out var pagination).Should().BeTrue(
            "envelope should have 'pagination' property");
        pagination.TryGetProperty("page", out _).Should().BeTrue("pagination should have 'page'");
        pagination.TryGetProperty("pageSize", out _).Should().BeTrue("pagination should have 'pageSize'");
        pagination.TryGetProperty("totalItems", out _).Should().BeTrue("pagination should have 'totalItems'");

        if (expectedPage is not null)
        {
            pagination.GetProperty("page").GetInt32().Should().Be(expectedPage.Value);
        }

        if (expectedTotalItems is not null)
        {
            pagination.GetProperty("totalItems").GetInt32().Should().Be(expectedTotalItems.Value);
        }
    }

    /// <summary>
    /// Extract the data property from a success envelope JSON string.
    /// </summary>
    public static T ExtractData<T>(this string json)
    {
        var doc = JsonDocument.Parse(json);
        var dataElement = doc.RootElement.GetProperty("data");
        return JsonSerializer.Deserialize<T>(dataElement.GetRawText())
            ?? throw new InvalidOperationException("Failed to deserialize data from envelope.");
    }

    /// <summary>
    /// Assert that the envelope metadata has a reasonable execution time.
    /// </summary>
    public static void ShouldHaveReasonableExecutionTime(this string json, int maxMs = 30000)
    {
        var doc = JsonDocument.Parse(json);
        var metadata = doc.RootElement.GetProperty("metadata");
        var executionMs = metadata.GetProperty("executionTimeMs").GetInt64();
        executionMs.Should().BeGreaterOrEqualTo(0);
        executionMs.Should().BeLessOrEqualTo(maxMs);
    }
}
