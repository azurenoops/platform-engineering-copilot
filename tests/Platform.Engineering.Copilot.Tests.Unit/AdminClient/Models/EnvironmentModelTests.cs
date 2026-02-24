using System.Text.Json;
using FluentAssertions;
using Platform.Engineering.Copilot.Admin.Client.Models;

namespace Platform.Engineering.Copilot.Tests.Unit.AdminClient.Models;

public class EnvironmentModelTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void EnvironmentSummaryDto_DefaultValues_AreCorrect()
    {
        var dto = new EnvironmentSummaryDto();

        dto.TotalCount.Should().Be(0);
        dto.HealthyCount.Should().Be(0);
        dto.DegradedCount.Should().Be(0);
        dto.UnhealthyCount.Should().Be(0);
        dto.ByStatus.Should().BeEmpty();
        dto.DriftCount.Should().Be(0);
        dto.ExpiringWithin7Days.Should().Be(0);
        dto.TotalEstimatedMonthlyCost.Should().Be(0);
        dto.ByTemplate.Should().BeEmpty();
    }

    [Fact]
    public void EnvironmentSummaryDto_Serialization_RoundTrip()
    {
        var dto = new EnvironmentSummaryDto
        {
            TotalCount = 10,
            HealthyCount = 7,
            DegradedCount = 2,
            UnhealthyCount = 1,
            ByStatus = new Dictionary<string, int> { { "Running", 8 }, { "Failed", 2 } },
            DriftCount = 3,
            ExpiringWithin7Days = 2,
            TotalEstimatedMonthlyCost = 5000.50m,
            ByTemplate = new List<TemplateCountDto>
            {
                new() { TemplateName = "vm-template", Count = 5 },
                new() { TemplateName = "aks-template", Count = 3 }
            }
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<EnvironmentSummaryDto>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.TotalCount.Should().Be(10);
        deserialized.ByStatus.Should().ContainKey("Running");
        deserialized.ByTemplate.Should().HaveCount(2);
        deserialized.TotalEstimatedMonthlyCost.Should().Be(5000.50m);
    }

    [Fact]
    public void EnvironmentDetailDto_DefaultValues_AreCorrect()
    {
        var dto = new EnvironmentDetailDto();

        dto.Id.Should().Be(Guid.Empty);
        dto.Name.Should().BeEmpty();
        dto.DisplayName.Should().BeNull();
        dto.Description.Should().BeNull();
        dto.Status.Should().BeEmpty();
        dto.HasDrift.Should().BeFalse();
        dto.DriftCount.Should().Be(0);
        dto.EstimatedMonthlyCost.Should().BeNull();
        dto.ExpiresAt.Should().BeNull();
        dto.AutoDelete.Should().BeFalse();
    }

    [Fact]
    public void EnvironmentDetailDto_Serialization_WithAllFields()
    {
        var id = Guid.NewGuid();
        var dto = new EnvironmentDetailDto
        {
            Id = id,
            Name = "dev-env-01",
            DisplayName = "Development 01",
            TemplateId = Guid.NewGuid(),
            TemplateName = "vm-template",
            SubscriptionId = "sub-123",
            ResourceGroup = "rg-dev",
            Location = "usgovvirginia",
            Status = "Running",
            HasDrift = true,
            DriftCount = 2,
            EstimatedMonthlyCost = 450.75m,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<EnvironmentDetailDto>(json, JsonOptions);

        deserialized!.Id.Should().Be(id);
        deserialized.Name.Should().Be("dev-env-01");
        deserialized.HasDrift.Should().BeTrue();
        deserialized.EstimatedMonthlyCost.Should().Be(450.75m);
    }

    [Fact]
    public void ResourceDto_DefaultValues_AreCorrect()
    {
        var dto = new ResourceDto();

        dto.AzureResourceId.Should().BeEmpty();
        dto.Name.Should().BeEmpty();
        dto.Type.Should().BeEmpty();
        dto.Location.Should().BeNull();
        dto.Sku.Should().BeNull();
        dto.ProvisioningState.Should().BeNull();
        dto.PortalUrl.Should().BeNull();
    }

    [Fact]
    public void ScaleResultDto_Serialization_RoundTrip()
    {
        var dto = new ScaleResultDto
        {
            EnvironmentId = Guid.NewGuid(),
            PreviousScale = "Standard_D2s_v3",
            NewScale = "Standard_D4s_v3",
            Status = "Succeeded",
            Message = "Scale operation completed"
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ScaleResultDto>(json, JsonOptions);

        deserialized!.Status.Should().Be("Succeeded");
        deserialized.PreviousScale.Should().Be("Standard_D2s_v3");
    }

    [Fact]
    public void DeleteResourcesResultDto_DefaultValues()
    {
        var dto = new DeleteResourcesResultDto();

        dto.DeletedCount.Should().Be(0);
        dto.FailedCount.Should().Be(0);
        dto.Failures.Should().BeEmpty();
    }

    [Fact]
    public void DriftDetectionResultDto_Serialization_RoundTrip()
    {
        var dto = new DriftDetectionResultDto
        {
            EnvironmentId = Guid.NewGuid(),
            TotalDriftCount = 3,
            DetectedAt = DateTimeOffset.UtcNow,
            DriftItems = new List<DriftItemDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ResourceId = "/subscriptions/sub/rg/res",
                    PropertyPath = "sku.name",
                    ExpectedValue = "Standard_D2s",
                    ActualValue = "Standard_D4s",
                    DriftType = "ConfigChange",
                    Severity = "High",
                    CanAutoRemediate = true
                }
            }
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<DriftDetectionResultDto>(json, JsonOptions);

        deserialized!.DriftItems.Should().HaveCount(1);
        deserialized.DriftItems[0].CanAutoRemediate.Should().BeTrue();
    }

    [Fact]
    public void RemediateDriftResultDto_DefaultValues()
    {
        var dto = new RemediateDriftResultDto();

        dto.RemediatedCount.Should().Be(0);
        dto.FailedCount.Should().Be(0);
        dto.RemainingCount.Should().Be(0);
        dto.Failures.Should().BeEmpty();
    }

    [Fact]
    public void EnvironmentHealthDto_DefaultValues()
    {
        var dto = new EnvironmentHealthDto();

        dto.OverallStatus.Should().BeEmpty();
        dto.HasDrift.Should().BeFalse();
        dto.DriftCount.Should().Be(0);
        dto.Issues.Should().BeEmpty();
        dto.ResourceHealth.Should().BeEmpty();
    }

    [Fact]
    public void ActivityListDto_DefaultValues()
    {
        var dto = new ActivityListDto();

        dto.Activities.Should().BeEmpty();
        dto.HasMore.Should().BeFalse();
    }

    [Fact]
    public void ComplianceSummaryDto_DefaultValues()
    {
        var dto = new ComplianceSummaryDto();

        dto.OverallScore.Should().Be(0);
        dto.FrameworkScores.Should().BeEmpty();
        dto.EnvironmentStatuses.Should().BeEmpty();
        dto.TopViolations.Should().BeEmpty();
    }

    [Fact]
    public void EnvironmentComplianceDto_Serialization_RoundTrip()
    {
        var dto = new EnvironmentComplianceDto
        {
            EnvironmentId = Guid.NewGuid(),
            EnvironmentName = "dev-env",
            OverallScore = 85.5,
            FrameworkResults = new List<FrameworkResultDto>
            {
                new()
                {
                    Framework = "NIST 800-53",
                    Score = 90.0,
                    Controls = new List<ControlResultDto>
                    {
                        new()
                        {
                            ControlId = "AC-1",
                            ControlName = "Access Control Policy",
                            Status = "Compliant",
                            Severity = "High"
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<EnvironmentComplianceDto>(json, JsonOptions);

        deserialized!.OverallScore.Should().Be(85.5);
        deserialized.FrameworkResults.Should().HaveCount(1);
        deserialized.FrameworkResults[0].Controls.Should().HaveCount(1);
    }
}
