using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data;
using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Records and retrieves environment activity (audit trail).
/// </summary>
public class EnvironmentActivityService
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly ILogger<EnvironmentActivityService> _logger;

    public EnvironmentActivityService(PlatformEngineeringCopilotContext context, ILogger<EnvironmentActivityService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RecordAsync(Guid environmentId, string action, string? details = null,
        string? performedBy = null, CancellationToken cancellationToken = default)
    {
        var activity = new EnvironmentActivity
        {
            Id = Guid.NewGuid(),
            EnvironmentId = environmentId,
            ActivityType = action,
            Description = details ?? action,
            UserId = performedBy ?? "system",
            Timestamp = DateTimeOffset.UtcNow
        };

        _context.EnvironmentActivities.Add(activity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Recorded activity {Action} for environment {EnvironmentId}", action, environmentId);
    }

    public async Task<object> GetActivitiesAsync(Guid environmentId, int skip = 0, int take = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _context.EnvironmentActivities
            .Where(a => a.EnvironmentId == environmentId)
            .OrderByDescending(a => a.Timestamp);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip(skip).Take(Math.Min(take, 100)).ToListAsync(cancellationToken);

        return new
        {
            items = items.Select(a => new
            {
                a.Id,
                action = a.ActivityType,
                details = a.Description,
                performedBy = a.UserId,
                timestamp = a.Timestamp
            }),
            totalCount,
            skip,
            take = Math.Min(take, 100)
        };
    }
}
