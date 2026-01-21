using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Repositories;
using Platform.Engineering.Copilot.Core.Interfaces.Environments;
using Platform.Engineering.Copilot.Core.Models.EnvironmentManagement;

namespace Platform.Engineering.Copilot.Agents.Environments.Services;

/// <summary>
/// Service for recording and retrieving environment activity history.
/// </summary>
public class EnvironmentActivityService : IEnvironmentActivityService
{
    private readonly IEnvironmentActivityRepository _repository;
    private readonly ILogger<EnvironmentActivityService> _logger;

    public EnvironmentActivityService(
        IEnvironmentActivityRepository repository,
        ILogger<EnvironmentActivityService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<EnvironmentActivity> RecordActivityAsync(
        AddEnvironmentActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = new EnvironmentActivityEntity
        {
            Id = Guid.NewGuid(),
            EnvironmentId = request.EnvironmentId,
            ActivityType = request.ActivityType,
            Description = request.Description,
            UserId = request.UserId,
            UserName = request.UserName,
            Metadata = request.Metadata != null 
                ? JsonSerializer.Serialize(request.Metadata) 
                : null,
            Timestamp = DateTime.UtcNow,
            Status = request.Status,
            ErrorMessage = request.ErrorMessage
        };

        await _repository.AddAsync(entity, cancellationToken);

        _logger.LogInformation(
            "Recorded activity {ActivityType} for environment {EnvironmentId}: {Description}",
            request.ActivityType, request.EnvironmentId, request.Description);

        return MapToModel(entity);
    }

    public async Task<EnvironmentActivityPagedResult> GetActivitiesAsync(
        Guid environmentId,
        string? activityType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var (entities, totalCount) = await _repository.GetByEnvironmentIdPagedAsync(
            environmentId,
            activityType,
            fromDate,
            toDate,
            skip,
            take,
            cancellationToken);

        return new EnvironmentActivityPagedResult
        {
            Items = entities.Select(MapToModel).ToList(),
            TotalCount = totalCount,
            Skip = skip,
            Take = take
        };
    }

    public async Task<IReadOnlyList<EnvironmentActivity>> GetRecentActivitiesAsync(
        int count = 50,
        CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetRecentActivitiesAsync(count, cancellationToken);
        return entities.Select(MapToModel).ToList();
    }

    private static EnvironmentActivity MapToModel(EnvironmentActivityEntity entity)
    {
        return new EnvironmentActivity
        {
            Id = entity.Id,
            EnvironmentId = entity.EnvironmentId,
            ActivityType = entity.ActivityType,
            Description = entity.Description,
            UserId = entity.UserId,
            UserName = entity.UserName,
            Metadata = string.IsNullOrEmpty(entity.Metadata)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, object>>(entity.Metadata),
            Timestamp = entity.Timestamp,
            Status = entity.Status,
            ErrorMessage = entity.ErrorMessage
        };
    }
}
