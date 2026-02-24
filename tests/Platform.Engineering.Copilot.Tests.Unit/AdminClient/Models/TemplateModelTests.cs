using System.Text.Json;
using FluentAssertions;
using Platform.Engineering.Copilot.Admin.Client.Models;

namespace Platform.Engineering.Copilot.Tests.Unit.AdminClient.Models;

public class TemplateModelTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void TemplateSummaryDto_DefaultValues_AreCorrect()
    {
        var dto = new TemplateSummaryDto();

        dto.TemplateId.Should().Be(Guid.Empty);
        dto.Name.Should().BeEmpty();
        dto.Description.Should().BeEmpty();
        dto.Version.Should().BeEmpty();
        dto.Category.Should().BeEmpty();
        dto.Format.Should().BeEmpty();
        dto.Status.Should().BeEmpty();
        dto.DisplayName.Should().BeNull();
        dto.DeploymentScope.Should().BeNull();
        dto.HasGitSource.Should().BeFalse();
        dto.GitRepositoryUrl.Should().BeNull();
        dto.LastSyncedFromGit.Should().BeNull();
        dto.GitAutoSync.Should().BeFalse();
    }

    [Fact]
    public void TemplateSummaryDto_Serialization_RoundTrip()
    {
        var dto = new TemplateSummaryDto
        {
            TemplateId = Guid.NewGuid(),
            Name = "test-template",
            DisplayName = "Test Template",
            Description = "A test template",
            Version = "1.0.0",
            Category = "Compute",
            Format = "Bicep",
            Status = "Published",
            DeploymentScope = "ResourceGroup",
            HasGitSource = true,
            GitRepositoryUrl = "https://github.com/test/repo",
            GitAutoSync = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<TemplateSummaryDto>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.TemplateId.Should().Be(dto.TemplateId);
        deserialized.Name.Should().Be("test-template");
        deserialized.Status.Should().Be("Published");
        deserialized.HasGitSource.Should().BeTrue();
    }

    [Fact]
    public void TemplateDetailDto_InheritsFromSummary()
    {
        var dto = new TemplateDetailDto
        {
            Name = "detail-test",
            Content = "param location string",
            ParametersJson = "[{\"name\":\"location\"}]"
        };

        dto.Should().BeAssignableTo<TemplateSummaryDto>();
        dto.Name.Should().Be("detail-test");
        dto.Content.Should().Contain("param");
    }

    [Fact]
    public void TemplateDetailDto_NullableFields_DefaultToNull()
    {
        var dto = new TemplateDetailDto();

        dto.ParametersJson.Should().BeNull();
        dto.GuardrailsJson.Should().BeNull();
        dto.ComplianceFrameworks.Should().BeNull();
        dto.Keywords.Should().BeNull();
        dto.UseCases.Should().BeNull();
        dto.AiSelectionHints.Should().BeNull();
        dto.AdditionalFilesJson.Should().BeNull();
        dto.ApprovalSource.Should().BeNull();
        dto.ApprovedBy.Should().BeNull();
        dto.ApprovedAt.Should().BeNull();
        dto.ApprovalComments.Should().BeNull();
        dto.DeprecatedBy.Should().BeNull();
        dto.DeprecatedAt.Should().BeNull();
        dto.DeprecationReason.Should().BeNull();
        dto.GitBranch.Should().BeNull();
        dto.GitPath.Should().BeNull();
        dto.GitSyncIntervalMinutes.Should().BeNull();
        dto.CreatedBy.Should().BeNull();
    }

    [Fact]
    public void TemplateParameterDto_DefaultValues_AreCorrect()
    {
        var dto = new TemplateParameterDto();

        dto.Name.Should().BeEmpty();
        dto.Type.Should().BeEmpty();
        dto.Required.Should().BeFalse();
        dto.DefaultValue.Should().BeNull();
        dto.AllowedValues.Should().BeEmpty();
        dto.MinValue.Should().BeNull();
        dto.MaxValue.Should().BeNull();
        dto.DisplayOrder.Should().Be(0);
    }

    [Fact]
    public void TemplateParameterDto_Serialization_WithAllowedValues()
    {
        var dto = new TemplateParameterDto
        {
            Name = "size",
            Type = "string",
            AllowedValues = new List<string> { "Small", "Medium", "Large" }
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<TemplateParameterDto>(json, JsonOptions);

        deserialized!.AllowedValues.Should().HaveCount(3);
        deserialized.AllowedValues.Should().Contain("Medium");
    }

    [Fact]
    public void TemplateGuardrailDto_DefaultValues_AreEmpty()
    {
        var dto = new TemplateGuardrailDto();

        dto.Type.Should().BeEmpty();
        dto.Property.Should().BeEmpty();
        dto.Operator.Should().BeEmpty();
        dto.Value.Should().BeEmpty();
        dto.Action.Should().BeEmpty();
        dto.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void TemplateValidationResultDto_DefaultValues()
    {
        var dto = new TemplateValidationResultDto();

        dto.IsValid.Should().BeFalse();
        dto.Errors.Should().BeEmpty();
        dto.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void GitStatusDto_DefaultValues()
    {
        var dto = new GitStatusDto();

        dto.HasChanges.Should().BeFalse();
        dto.CurrentCommitSha.Should().BeNull();
        dto.LatestCommitSha.Should().BeNull();
        dto.LastSyncedAt.Should().BeNull();
    }

    [Fact]
    public void TemplateMatchResultDto_DefaultValues()
    {
        var dto = new TemplateMatchResultDto();

        dto.Matches.Should().BeEmpty();
        dto.Query.Should().BeEmpty();
        dto.TotalMatches.Should().Be(0);
    }

    [Fact]
    public void TemplateMatchDto_Serialization_RoundTrip()
    {
        var dto = new TemplateMatchDto
        {
            TemplateId = Guid.NewGuid(),
            TemplateName = "vm-template",
            DisplayName = "VM Template",
            Score = 0.95,
            Reason = "High confidence match"
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<TemplateMatchDto>(json, JsonOptions);

        deserialized!.Score.Should().Be(0.95);
        deserialized.Reason.Should().Be("High confidence match");
    }

    [Fact]
    public void TemplateSummaryDto_Serialization_HandlesNulls()
    {
        var json = """{"templateId":"00000000-0000-0000-0000-000000000000","name":"test","description":"desc","version":"1.0","category":"cat","format":"Bicep","status":"Draft","hasGitSource":false,"gitAutoSync":false,"createdAt":"2024-01-01T00:00:00+00:00","updatedAt":"2024-01-01T00:00:00+00:00"}""";

        var dto = JsonSerializer.Deserialize<TemplateSummaryDto>(json, JsonOptions);

        dto.Should().NotBeNull();
        dto!.DisplayName.Should().BeNull();
        dto.DeploymentScope.Should().BeNull();
        dto.GitRepositoryUrl.Should().BeNull();
    }
}
