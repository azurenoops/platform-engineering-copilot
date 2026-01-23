using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.Jobs;
using Platform.Engineering.Copilot.Core.Models.Jobs;

namespace Platform.Engineering.Copilot.Core.Services.Jobs;

/// <summary>
/// In-memory implementation of background job service
/// Manages long-running operations asynchronously with progress tracking
/// </summary>
public class BackgroundJobService : IBackgroundJobService
{
    private readonly ILogger<BackgroundJobService> _logger;
    private readonly ConcurrentDictionary<string, BackgroundJob> _jobs = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _jobCancellations = new();
    
    public BackgroundJobService(ILogger<BackgroundJobService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Start a new background job
    /// </summary>
    public async Task<BackgroundJob> StartJobAsync(
        string jobType,
        string conversationId,
        string userId,
        string inputMessage,
        Dictionary<string, object>? inputContext,
        Func<IProgress<JobProgressUpdate>, CancellationToken, Task<object>> workload,
        CancellationToken cancellationToken = default)
    {
        var job = new BackgroundJob
        {
            JobType = jobType,
            ConversationId = conversationId,
            UserId = userId,
            InputMessage = inputMessage,
            InputContext = inputContext ?? new Dictionary<string, object>(),
            Status = JobStatus.Queued,
            ExpiresAt = DateTime.UtcNow.AddHours(24) // Results expire after 24 hours
        };
        
        _jobs[job.JobId] = job;
        
        // Create cancellation token source linked to the request cancellation token
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _jobCancellations[job.JobId] = cts;
        
        // Start job execution in background with proper exception handling
        _ = ExecuteJobWithExceptionHandlingAsync(job, workload, cts.Token);
        
        _logger.LogInformation(
            "Started background job {JobId} of type {JobType} for conversation {ConversationId}", 
            job.JobId, 
            jobType, 
            conversationId);
        
        return await Task.FromResult(job);
    }
    
    /// <summary>
    /// Wrapper to ensure fire-and-forget tasks have proper exception handling
    /// </summary>
    private async Task ExecuteJobWithExceptionHandlingAsync(
        BackgroundJob job,
        Func<IProgress<JobProgressUpdate>, CancellationToken, Task<object>> workload,
        CancellationToken cancellationToken)
    {
        try
        {
            await ExecuteJobAsync(job, workload, cancellationToken);
        }
        catch (Exception ex)
        {
            // Ensure unhandled exceptions from fire-and-forget are logged
            _logger.LogError(ex, "Unhandled exception in background job {JobId}", job.JobId);
            job.Status = JobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            job.Error = ex.Message;
        }
    }
    
    /// <summary>
    /// Execute a job in the background with progress tracking
    /// </summary>
    private async Task ExecuteJobAsync(
        BackgroundJob job,
        Func<IProgress<JobProgressUpdate>, CancellationToken, Task<object>> workload,
        CancellationToken cancellationToken)
    {
        try
        {
            job.Status = JobStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            
            _logger.LogInformation("Executing job {JobId}", job.JobId);
            
            // Create progress reporter
            var progress = new Progress<JobProgressUpdate>(update =>
            {
                job.ProgressPercentage = update.ProgressPercentage;
                job.CurrentStep = update.CurrentStep;
                
                if (!string.IsNullOrEmpty(update.CurrentStep))
                {
                    job.CompletedSteps.Add(update.CurrentStep);
                }
                
                _logger.LogInformation(
                    "Job {JobId} progress: {Percentage}% - {Step}", 
                    job.JobId, 
                    update.ProgressPercentage, 
                    update.CurrentStep);
            });
            
            // Execute the workload
            var result = await workload(progress, cancellationToken);
            
            // Mark as completed
            job.Result = result;
            job.Status = JobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            job.ProgressPercentage = 100;
            
            _logger.LogInformation(
                "Job {JobId} completed successfully in {Duration:0.00}s", 
                job.JobId, 
                (job.CompletedAt.Value - job.StartedAt!.Value).TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            job.Status = JobStatus.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
            _logger.LogWarning("Job {JobId} was cancelled", job.JobId);
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            job.Error = ex.Message;
            
            _logger.LogError(ex, "Job {JobId} failed with error: {Error}", job.JobId, ex.Message);
        }
        finally
        {
            // Clean up cancellation token
            _jobCancellations.TryRemove(job.JobId, out _);
        }
    }
    
    /// <summary>
    /// Get a job by its ID
    /// </summary>
    public Task<BackgroundJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        _jobs.TryGetValue(jobId, out var job);
        
        // Check if expired
        if (job != null && job.ExpiresAt.HasValue && job.ExpiresAt.Value < DateTime.UtcNow)
        {
            job.Status = JobStatus.Expired;
        }
        
        return Task.FromResult(job);
    }
    
    /// <summary>
    /// Get all jobs for a conversation
    /// </summary>
    public Task<List<BackgroundJob>> GetJobsByConversationAsync(
        string conversationId, 
        CancellationToken cancellationToken = default)
    {
        var jobs = _jobs.Values
            .Where(j => j.ConversationId == conversationId)
            .OrderByDescending(j => j.CreatedAt)
            .ToList();
        
        return Task.FromResult(jobs);
    }
    
    /// <summary>
    /// Cancel a running job
    /// </summary>
    public Task<bool> CancelJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_jobCancellations.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
            _logger.LogInformation("Cancelled job {JobId}", jobId);
            return Task.FromResult(true);
        }
        
        _logger.LogWarning("Attempted to cancel job {JobId} but it was not found or already completed", jobId);
        return Task.FromResult(false);
    }
    
    /// <summary>
    /// Clean up expired jobs
    /// </summary>
    public Task<int> CleanupExpiredJobsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiredJobIds = _jobs.Values
            .Where(j => j.ExpiresAt.HasValue && j.ExpiresAt.Value < now)
            .Select(j => j.JobId)
            .ToList();
        
        foreach (var jobId in expiredJobIds)
        {
            _jobs.TryRemove(jobId, out _);
            _jobCancellations.TryRemove(jobId, out _);
        }
        
        if (expiredJobIds.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired jobs", expiredJobIds.Count);
        }
        
        return Task.FromResult(expiredJobIds.Count);
    }
}
