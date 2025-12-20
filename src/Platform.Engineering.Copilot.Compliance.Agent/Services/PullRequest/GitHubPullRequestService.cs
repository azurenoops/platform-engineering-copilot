using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Models.PullRequest;

namespace Platform.Engineering.Copilot.Compliance.Agent.Services.PullRequest;

/// <summary>
/// Service for interacting with GitHub Pull Request API
/// Handles PR review comments, status checks, and file retrieval
/// </summary>
public class GitHubPullRequestService
{
    private readonly ILogger<GitHubPullRequestService> _logger;
    private readonly HttpClient _httpClient;
    private readonly GitHubGatewayOptions _config;

    public GitHubPullRequestService(
        ILogger<GitHubPullRequestService> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<GatewayOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClientFactory?.CreateClient("GitHub") ?? throw new ArgumentNullException(nameof(httpClientFactory));
        var gatewayOptions = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _config = gatewayOptions.GitHub;

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        
        if (!string.IsNullOrEmpty(_config.AccessToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _config.AccessToken);
        }

        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Platform-Engineering-Copilot/1.0");
    }

    /// <summary>
    /// Get pull request details including files changed
    /// </summary>
    public async Task<PullRequestDetails> GetPullRequestAsync(string owner, string repo, int prNumber, CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            _logger.LogWarning("GitHub integration is disabled");
            throw new InvalidOperationException("GitHub integration is disabled");
        }

        if (!_config.EnablePrReviews)
        {
            _logger.LogWarning("PR review operations are disabled");
            throw new InvalidOperationException("PR review operations are disabled");
        }

        _logger.LogInformation("Fetching PR details for {Owner}/{Repo}#{PrNumber}", owner, repo, prNumber);

        var baseUrl = _config.ApiBaseUrl?.TrimEnd('/') ?? "https://api.github.com";
        var prUrl = $"{baseUrl}/repos/{owner}/{repo}/pulls/{prNumber}";
        var response = await _httpClient.GetAsync(prUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var prJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var prData = JsonSerializer.Deserialize<JsonElement>(prJson);

        // Get files changed
        var filesUrl = $"{baseUrl}/repos/{owner}/{repo}/pulls/{prNumber}/files";
        var filesResponse = await _httpClient.GetAsync(filesUrl, cancellationToken);
        filesResponse.EnsureSuccessStatusCode();

        var filesJson = await filesResponse.Content.ReadAsStringAsync(cancellationToken);
        var filesData = JsonSerializer.Deserialize<List<JsonElement>>(filesJson);

        var maxFileSizeBytes = _config.MaxFileSizeKb * 1024;
        var files = filesData?.Select(f => 
        {
            var fileSize = f.GetProperty("size").GetInt64();
            if (fileSize > maxFileSizeBytes)
            {
                _logger.LogWarning("File {Filename} ({SizeKb}KB) exceeds max size of {MaxSizeKb}KB",
                    f.GetProperty("filename").GetString(), fileSize / 1024, _config.MaxFileSizeKb);
            }
            
            return new PullRequestFile
            {
                Filename = f.GetProperty("filename").GetString() ?? "",
                Status = f.GetProperty("status").GetString() ?? "",
                Additions = f.GetProperty("additions").GetInt32(),
                Deletions = f.GetProperty("deletions").GetInt32(),
                Changes = f.GetProperty("changes").GetInt32(),
                Patch = f.TryGetProperty("patch", out var patch) ? patch.GetString() : null,
                RawUrl = f.GetProperty("raw_url").GetString() ?? "",
                ContentsUrl = f.GetProperty("contents_url").GetString() ?? ""
            };
        }).ToList() ?? new List<PullRequestFile>();

        return new PullRequestDetails
        {
            Number = prNumber,
            Title = prData.GetProperty("title").GetString() ?? "",
            State = prData.GetProperty("state").GetString() ?? "",
            BaseBranch = prData.GetProperty("base").GetProperty("ref").GetString() ?? "",
            HeadBranch = prData.GetProperty("head").GetProperty("ref").GetString() ?? "",
            HeadSha = prData.GetProperty("head").GetProperty("sha").GetString() ?? "",
            Author = prData.GetProperty("user").GetProperty("login").GetString() ?? "",
            Files = files
        };
    }

    /// <summary>
    /// Download file content from the PR
    /// </summary>
    public async Task<string> GetFileContentAsync(string rawUrl, CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            throw new InvalidOperationException("GitHub integration is disabled");
        }

        _logger.LogDebug("Downloading file from {RawUrl}", rawUrl);

        var response = await _httpClient.GetAsync(rawUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        
        // Validate content size
        var contentSizeBytes = Encoding.UTF8.GetByteCount(content);
        if (contentSizeBytes > _config.MaxFileSizeKb * 1024)
        {
            _logger.LogWarning("Downloaded file content ({SizeKb}KB) exceeds max size of {MaxSizeKb}KB",
                contentSizeBytes / 1024, _config.MaxFileSizeKb);
        }

        return content;
    }

    /// <summary>
    /// Post a review comment on the PR
    /// </summary>
    public async Task<bool> PostReviewCommentAsync(
        string owner, 
        string repo, 
        int prNumber, 
        string commitSha,
        PullRequestReviewComment comment,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled || !_config.EnablePrReviews)
        {
            _logger.LogWarning("PR review operations are disabled");
            return false;
        }

        _logger.LogInformation("Posting review comment on {Owner}/{Repo}#{PrNumber} at {Path}:{Line}", 
            owner, repo, prNumber, comment.Path, comment.Line);

        var baseUrl = _config.ApiBaseUrl?.TrimEnd('/') ?? "https://api.github.com";
        var url = $"{baseUrl}/repos/{owner}/{repo}/pulls/{prNumber}/comments";

        var payload = new
        {
            body = comment.Body,
            commit_id = commitSha,
            path = comment.Path,
            line = comment.Line,
            side = "RIGHT" // Comment on the new version of the file
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Failed to post review comment: {StatusCode} - {Error}", response.StatusCode, error);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Submit a PR review (approve, request changes, or comment)
    /// </summary>
    public async Task<bool> SubmitReviewAsync(
        string owner,
        string repo,
        int prNumber,
        PullRequestReview review,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled || !_config.EnablePrReviews)
        {
            _logger.LogWarning("PR review operations are disabled");
            return false;
        }

        _logger.LogInformation("Submitting {Event} review on {Owner}/{Repo}#{PrNumber}", 
            review.Event, owner, repo, prNumber);

        var baseUrl = _config.ApiBaseUrl?.TrimEnd('/') ?? "https://api.github.com";
        var url = $"{baseUrl}/repos/{owner}/{repo}/pulls/{prNumber}/reviews";

        var payload = new
        {
            body = review.Body,
            @event = review.Event.ToString().ToUpperInvariant(),
            comments = review.Comments?.Select(c => new
            {
                path = c.Path,
                line = c.Line,
                body = c.Body
            }).ToArray()
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Failed to submit review: {StatusCode} - {Error}", response.StatusCode, error);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Create or update a commit status check
    /// </summary>
    public async Task<bool> SetCommitStatusAsync(
        string owner,
        string repo,
        string sha,
        CommitStatus status,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            _logger.LogWarning("GitHub integration is disabled");
            return false;
        }

        _logger.LogInformation("Setting commit status to {State} for {Owner}/{Repo}@{Sha}", 
            status.State, owner, repo, sha.Substring(0, 7));

        var baseUrl = _config.ApiBaseUrl?.TrimEnd('/') ?? "https://api.github.com";
        var url = $"{baseUrl}/repos/{owner}/{repo}/statuses/{sha}";

        var payload = new
        {
            state = status.State.ToLowerInvariant(),
            target_url = status.TargetUrl,
            description = status.Description,
            context = status.Context ?? "Platform Engineering Copilot / Compliance Review"
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Failed to set commit status: {StatusCode} - {Error}", response.StatusCode, error);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Approve a pull request if auto-approval is enabled
    /// </summary>
    public async Task<bool> AutoApproveIfEnabledAsync(
        string owner,
        string repo,
        int prNumber,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled || !_config.EnablePrReviews || !_config.AutoApproveOnSuccess)
        {
            _logger.LogDebug("Auto-approval is disabled");
            return false;
        }

        _logger.LogInformation("Auto-approving PR {Owner}/{Repo}#{PrNumber}", owner, repo, prNumber);

        var review = new PullRequestReview
        {
            Body = "✅ All compliance checks passed. Approving automatically.",
            Event = ReviewEvent.Approve
        };

        return await SubmitReviewAsync(owner, repo, prNumber, review, cancellationToken);
    }
}
