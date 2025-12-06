using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Core.Plugins;
using Platform.Engineering.Copilot.Core.Models.PullRequest;
using Platform.Engineering.Copilot.Compliance.Agent.Services.PullRequest;

namespace Platform.Engineering.Copilot.Compliance.Agent.Plugins.Code;

/// <summary>
/// Semantic Kernel plugin for Pull Request compliance reviews
/// Enables automated IaC review for GitHub/Azure DevOps PRs
/// Phase 1: Advisory only - no auto-merge or auto-approval
/// </summary>
public class PullRequestReviewPlugin : BaseSupervisorPlugin
{
    private readonly GitHubPullRequestService _githubService;
    private readonly PullRequestReviewService _reviewService;

    public PullRequestReviewPlugin(
        ILogger<PullRequestReviewPlugin> logger,
        Kernel kernel,
        GitHubPullRequestService githubService,
        PullRequestReviewService reviewService) : base(logger, kernel)
    {
        _githubService = githubService ?? throw new ArgumentNullException(nameof(githubService));
        _reviewService = reviewService ?? throw new ArgumentNullException(nameof(reviewService));
    }

    [KernelFunction("review_pull_request")]
    [Description("Review a GitHub pull request for compliance violations in IaC files (Bicep, Terraform, ARM, K8s). " +
                 "Scans for NIST 800-53, STIG, and DoD instruction violations. " +
                 "ADVISORY ONLY - Does not auto-merge or approve. Phase 1 compliant.")]
    public async Task<string> ReviewPullRequestAsync(
        [Description("GitHub repository owner (e.g., 'azurenoops')")] string owner,
        [Description("GitHub repository name (e.g., 'platform-engineering-copilot')")] string repo,
        [Description("Pull request number")] int prNumber,
        [Description("Post review comments to GitHub (true/false, default: true)")] bool postComments = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Reviewing PR {Owner}/{Repo}#{PrNumber}", owner, repo, prNumber);

            // Step 1: Fetch PR details
            var pr = await _githubService.GetPullRequestAsync(owner, repo, prNumber, cancellationToken);

            _logger.LogInformation("PR #{Number}: {Title} by {Author} ({FileCount} files changed)",
                pr.Number, pr.Title, pr.Author, pr.Files.Count);

            // Step 2: Filter IaC files only
            var iacFiles = pr.Files.Where(f => f.IsIaCFile()).ToList();

            if (!iacFiles.Any())
            {
                _logger.LogInformation("No IaC files found in PR #{Number}", prNumber);
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    prNumber = pr.Number,
                    title = pr.Title,
                    message = "No Infrastructure as Code files detected in this PR. No compliance review needed.",
                    filesScanned = 0
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            _logger.LogInformation("Found {Count} IaC files: {Files}",
                iacFiles.Count, string.Join(", ", iacFiles.Select(f => f.Filename)));

            // Step 3: Download file contents
            var fileContents = new Dictionary<string, string>();
            foreach (var file in iacFiles)
            {
                try
                {
                    var content = await _githubService.GetFileContentAsync(file.RawUrl, cancellationToken);
                    fileContents[file.Filename] = content;
                    _logger.LogDebug("Downloaded {Filename} ({Size} bytes)", file.Filename, content.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to download {Filename}", file.Filename);
                }
            }

            // Step 4: Run compliance review
            var review = await _reviewService.ReviewPullRequestAsync(pr, fileContents, cancellationToken);

            _logger.LogInformation("Review complete: {Total} findings", review.TotalFindings);

            // Step 5: Post review comments if requested
            if (postComments && review.TotalFindings > 0)
            {
                await PostReviewCommentsAsync(owner, repo, prNumber, pr.HeadSha, review, cancellationToken);
            }

            // Step 6: Set commit status
            var statusState = review.CriticalFindings == 0 ? "success" : "failure";
            var statusDescription = review.CriticalFindings == 0
                ? $"Compliance check passed ({review.TotalFindings} warnings)"
                : $"Compliance check failed ({review.CriticalFindings} critical issues)";

            await _githubService.SetCommitStatusAsync(owner, repo, pr.HeadSha, new CommitStatus
            {
                State = statusState,
                Description = statusDescription,
                Context = "Platform Engineering Copilot / Compliance Review"
            }, cancellationToken);

            // Return results
            return JsonSerializer.Serialize(new
            {
                success = true,
                prNumber = pr.Number,
                title = pr.Title,
                author = pr.Author,
                filesScanned = iacFiles.Count,
                review = new
                {
                    totalFindings = review.TotalFindings,
                    critical = review.CriticalFindings,
                    high = review.HighFindings,
                    medium = review.MediumFindings,
                    low = review.LowFindings,
                    approved = review.Approved,
                    summary = review.Summary
                },
                phase1Note = "ADVISORY ONLY - Manual review and approval required before merge",
                nextSteps = review.CriticalFindings > 0
                    ? new[] {
                        "Fix critical compliance violations",
                        "Request re-review after fixes",
                        "Manual approval required before merge"
                      }
                    : new[] {
                        "Review warnings and recommendations",
                        "Manual approval required before merge"
                      }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reviewing PR {Owner}/{Repo}#{PrNumber}", owner, repo, prNumber);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to review pull request: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    /// <summary>
    /// Post individual review comments for each finding
    /// </summary>
    private async Task PostReviewCommentsAsync(
        string owner,
        string repo,
        int prNumber,
        string commitSha,
        PullRequestComplianceReview review,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Posting {Count} review comments to PR #{PrNumber}",
            review.Findings.Count, prNumber);

        // Post summary review first
        await _githubService.SubmitReviewAsync(owner, repo, prNumber, new PullRequestReview
        {
            Body = review.Summary,
            Event = review.Approved ? ReviewEvent.Comment : ReviewEvent.RequestChanges,
            Comments = null // Will post individual comments separately
        }, cancellationToken);

        // Post individual inline comments for each finding
        foreach (var finding in review.Findings.Take(20)) // Limit to 20 comments to avoid spam
        {
            try
            {
                var comment = _reviewService.GenerateFindingComment(finding);
                await _githubService.PostReviewCommentAsync(owner, repo, prNumber, commitSha, new PullRequestReviewComment
                {
                    Path = finding.FilePath,
                    Line = finding.Line,
                    Body = comment
                }, cancellationToken);

                _logger.LogDebug("Posted comment on {Path}:{Line}", finding.FilePath, finding.Line);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to post comment on {Path}:{Line}", finding.FilePath, finding.Line);
            }
        }

        if (review.Findings.Count > 20)
        {
            _logger.LogWarning("Limited to 20 inline comments (found {Total})", review.Findings.Count);
        }
    }

    [KernelFunction("get_pr_review_status")]
    [Description("Get the status of a previous compliance review for a pull request")]
    public async Task<string> GetPullRequestReviewStatusAsync(
        [Description("GitHub repository owner")] string owner,
        [Description("GitHub repository name")] string repo,
        [Description("Pull request number")] int prNumber,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pr = await _githubService.GetPullRequestAsync(owner, repo, prNumber, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = true,
                prNumber = pr.Number,
                title = pr.Title,
                state = pr.State,
                author = pr.Author,
                filesChanged = pr.Files.Count,
                iacFiles = pr.Files.Count(f => f.IsIaCFile()),
                message = "Use review_pull_request to perform a new compliance review"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting PR status");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to get PR status: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
