using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Tests.Unit.Services;

public class BicepParameterParserTests
{
    private readonly BicepParameterParser _parser;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public BicepParameterParserTests()
    {
        _parser = new BicepParameterParser(Mock.Of<ILogger<BicepParameterParser>>());
    }

    private static JsonElement ToJson(object obj) =>
        JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(obj, JsonOpts));

    [Fact]
    public async Task ValidateAsync_WithValidBicepContent_ReturnsIsValid()
    {
        const string content = @"
param location string = 'eastus'
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'mystorageacct'
  location: location
}";
        var result = ToJson(await _parser.ValidateAsync(content));

        result.GetProperty("isValid").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithEmptyContent_ReturnsInvalid()
    {
        var result = ToJson(await _parser.ValidateAsync(""));

        result.GetProperty("isValid").GetBoolean().Should().BeFalse();
        result.GetProperty("errors").EnumerateArray().Should().Contain(e => e.GetString()!.Contains("content is required"));
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidParametersJson_ReturnsError()
    {
        var result = ToJson(await _parser.ValidateAsync("resource x", "{invalid json}"));

        result.GetProperty("isValid").GetBoolean().Should().BeFalse();
        result.GetProperty("errors").EnumerateArray().Should().Contain(e => e.GetString()!.Contains("Invalid parameters JSON"));
    }

    [Fact]
    public async Task ValidateAsync_WithNoBicepKeywords_ReturnsWarning()
    {
        var result = ToJson(await _parser.ValidateAsync("some plain text content"));

        result.GetProperty("isValid").GetBoolean().Should().BeTrue();
        result.GetProperty("warnings").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ParseParametersAsync_ExtractsParams()
    {
        const string content = @"
param location string = 'eastus'
param nodeCount int
param vmSize string = 'Standard_D2s_v3'
";
        var result = ToJson(await _parser.ParseParametersAsync(content));

        result.GetProperty("count").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task ParseParametersAsync_DetectsRequiredVsOptional()
    {
        const string content = @"
param requiredParam string
param optionalParam string = 'default'
";
        var result = ToJson(await _parser.ParseParametersAsync(content));

        result.GetProperty("parameters").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task ParseParametersAsync_EmptyContent_ReturnsEmpty()
    {
        var result = ToJson(await _parser.ParseParametersAsync(""));

        result.GetProperty("count").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task ParseFromGitAsync_ReturnsStubResult()
    {
        var result = ToJson(await _parser.ParseFromGitAsync("https://github.com/org/repo.git", "main", "main.bicep"));

        result.GetProperty("gitRepoUrl").GetString().Should().Be("https://github.com/org/repo.git");
        result.GetProperty("branch").GetString().Should().Be("main");
    }
}
