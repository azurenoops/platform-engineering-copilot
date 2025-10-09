using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Interfaces;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Gateway service for GitHub operations
/// </summary>
public class GitHubGatewayService : IGitHubServices
{
    private readonly ILogger<GitHubGatewayService> _logger;
    private readonly GitHubGatewayOptions _options;
    private readonly GitHubClient? _gitHubClient;

    public GitHubGatewayService(
        ILogger<GitHubGatewayService> logger,
        IOptions<GatewayOptions> options)
    {
        _logger = logger;
        _options = options.Value.GitHub;

        if (_options.Enabled && !string.IsNullOrEmpty(_options.AccessToken))
        {
            try
            {
                _gitHubClient = new GitHubClient(new ProductHeaderValue("supervisor-mcp-server"))
                {
                    Credentials = new Credentials(_options.AccessToken)
                };
                _logger.LogInformation("GitHub client initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize GitHub client");
            }
        }
    }

    #region Repository Operations

    public async Task<Repository?> GetRepositoryAsync(string owner, string name, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Getting repository {Owner}/{Name}", owner, name);
            var repository = await _gitHubClient.Repository.Get(owner, name);
            return repository;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get repository {Owner}/{Name}", owner, name);
            return null;
        }
    }

    public async Task<IEnumerable<Repository>?> ListRepositoriesAsync(string? owner = null, string type = "all", CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            var targetOwner = owner ?? _options.DefaultOwner;
            if (string.IsNullOrEmpty(targetOwner))
            {
                _logger.LogError("No owner provided and no default owner configured");
                return null;
            }

            _logger.LogDebug("Listing repositories for {Owner} of type {Type}", targetOwner, type);
            var repositories = await _gitHubClient.Repository.GetAllForUser(targetOwner);

            return type.ToLowerInvariant() switch
            {
                "public" => repositories.Where(r => !r.Private),
                "private" => repositories.Where(r => r.Private),
                _ => repositories
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list repositories for {Owner}", owner);
            return null;
        }
    }

    public async Task<Repository?> CreateRepositoryAsync(string name, string? description = null, bool isPrivate = false, string? owner = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Creating repository {Name} for {Owner}", name, owner ?? "authenticated user");

            var newRepository = new NewRepository(name)
            {
                Description = description,
                Private = isPrivate
            };

            Repository repository;
            if (!string.IsNullOrEmpty(owner))
            {
                repository = await _gitHubClient.Repository.Create(owner, newRepository);
            }
            else
            {
                repository = await _gitHubClient.Repository.Create(newRepository);
            }

            return repository;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create repository {Name}", name);
            return null;
        }
    }

    public async Task<Repository?> ForkRepositoryAsync(string owner, string name, string? organization = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Forking repository {Owner}/{Name} to {Organization}", owner, name, organization ?? "user");

            var fork = new NewRepositoryFork();
            if (!string.IsNullOrEmpty(organization))
            {
                fork.Organization = organization;
            }

            var repository = await _gitHubClient.Repository.Forks.Create(owner, name, fork);
            return repository;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fork repository {Owner}/{Name}", owner, name);
            return null;
        }
    }

    #endregion

    #region Issue Management

    public async Task<Issue?> CreateIssueAsync(string owner, string name, string title, string? body = null, string[]? labels = null, string[]? assignees = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Creating issue '{Title}' in {Owner}/{Name}", title, owner, name);

            var newIssue = new NewIssue(title)
            {
                Body = body ?? string.Empty
            };

            if (labels != null)
            {
                foreach (var label in labels)
                {
                    newIssue.Labels.Add(label);
                }
            }

            if (assignees != null)
            {
                foreach (var assignee in assignees)
                {
                    newIssue.Assignees.Add(assignee);
                }
            }

            var issue = await _gitHubClient.Issue.Create(owner, name, newIssue);
            return issue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create issue '{Title}' in {Owner}/{Name}", title, owner, name);
            return null;
        }
    }

    public async Task<IEnumerable<Issue>?> ListIssuesAsync(string owner, string name, string state = "open", string[]? labels = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Listing issues for {Owner}/{Name} with state {State}", owner, name, state);

            var issueState = state.ToLowerInvariant() switch
            {
                "closed" => ItemStateFilter.Closed,
                "all" => ItemStateFilter.All,
                _ => ItemStateFilter.Open
            };

            var request = new RepositoryIssueRequest
            {
                State = issueState
            };

            if (labels != null && labels.Length > 0)
            {
                foreach (var label in labels)
                {
                    request.Labels.Add(label);
                }
            }

            var issues = await _gitHubClient.Issue.GetAllForRepository(owner, name, request);
            return issues;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list issues for {Owner}/{Name}", owner, name);
            return null;
        }
    }

    public async Task<Issue?> UpdateIssueAsync(string owner, string name, int issueNumber, string? title = null, string? body = null, string? state = null, string[]? labels = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Updating issue #{IssueNumber} in {Owner}/{Name}", issueNumber, owner, name);

            var issueUpdate = new IssueUpdate();

            if (!string.IsNullOrEmpty(title))
                issueUpdate.Title = title;

            if (!string.IsNullOrEmpty(body))
                issueUpdate.Body = body;

            if (!string.IsNullOrEmpty(state))
            {
                issueUpdate.State = state.ToLowerInvariant() switch
                {
                    "closed" => ItemState.Closed,
                    _ => ItemState.Open
                };
            }

            if (labels != null)
            {
                issueUpdate.Labels.Clear();
                foreach (var label in labels)
                {
                    issueUpdate.Labels.Add(label);
                }
            }

            var issue = await _gitHubClient.Issue.Update(owner, name, issueNumber, issueUpdate);
            return issue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update issue #{IssueNumber} in {Owner}/{Name}", issueNumber, owner, name);
            return null;
        }
    }

    #endregion

    #region Pull Request Management

    public async Task<PullRequest?> CreatePullRequestAsync(string owner, string name, string title, string? body, string head, string baseRef, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Creating pull request '{Title}' in {Owner}/{Name} from {Head} to {Base}", title, owner, name, head, baseRef);

            var newPullRequest = new NewPullRequest(title, head, baseRef)
            {
                Body = body ?? string.Empty
            };

            var pullRequest = await _gitHubClient.PullRequest.Create(owner, name, newPullRequest);
            return pullRequest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create pull request '{Title}' in {Owner}/{Name}", title, owner, name);
            return null;
        }
    }

    public async Task<IEnumerable<PullRequest>?> ListPullRequestsAsync(string owner, string name, string state = "open", CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Listing pull requests for {Owner}/{Name} with state {State}", owner, name, state);

            var prState = state.ToLowerInvariant() switch
            {
                "closed" => ItemStateFilter.Closed,
                "all" => ItemStateFilter.All,
                _ => ItemStateFilter.Open
            };

            var request = new PullRequestRequest
            {
                State = prState
            };

            var pullRequests = await _gitHubClient.PullRequest.GetAllForRepository(owner, name, request);
            return pullRequests;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list pull requests for {Owner}/{Name}", owner, name);
            return null;
        }
    }

    public async Task<PullRequestMerge?> MergePullRequestAsync(string owner, string name, int pullNumber, string? commitMessage = null, PullRequestMergeMethod mergeMethod = PullRequestMergeMethod.Merge, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Merging pull request #{PullNumber} in {Owner}/{Name}", pullNumber, owner, name);

            var mergePullRequest = new MergePullRequest
            {
                CommitMessage = commitMessage,
                MergeMethod = mergeMethod
            };

            var mergeResult = await _gitHubClient.PullRequest.Merge(owner, name, pullNumber, mergePullRequest);
            return mergeResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to merge pull request #{PullNumber} in {Owner}/{Name}", pullNumber, owner, name);
            return null;
        }
    }

    #endregion

    #region Workflow Management

    public async Task<IEnumerable<WorkflowRun>?> ListWorkflowRunsAsync(string owner, string name, long? workflowId = null, string? status = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Listing workflow runs for {Owner}/{Name}", owner, name);

            // Use repository actions API to get workflow runs
            var workflowRuns = await _gitHubClient.Actions.Workflows.Runs.List(owner, name);
            
            // Filter by workflow ID if specified
            if (workflowId.HasValue)
            {
                return workflowRuns.WorkflowRuns.Where(r => r.WorkflowId == workflowId.Value);
            }

            return workflowRuns.WorkflowRuns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list workflow runs for {Owner}/{Name}", owner, name);
            return null;
        }
    }

    public async Task<bool> TriggerWorkflowDispatchAsync(string owner, string name, string workflowId, string branch = "main", Dictionary<string, object>? inputs = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return false;
            }

            _logger.LogDebug("Triggering workflow dispatch for {WorkflowId} in {Owner}/{Name} on branch {Branch}", workflowId, owner, name, branch);

            var dispatch = new CreateWorkflowDispatch(branch)
            {
                Inputs = inputs ?? new Dictionary<string, object>()
            };

            await _gitHubClient.Actions.Workflows.CreateDispatch(owner, name, workflowId, dispatch);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger workflow dispatch for {WorkflowId} in {Owner}/{Name}", workflowId, owner, name);
            return false;
        }
    }

    public async Task<WorkflowRun?> GetWorkflowRunAsync(string owner, string name, long runId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Getting workflow run {RunId} for {Owner}/{Name}", runId, owner, name);

            var workflowRun = await _gitHubClient.Actions.Workflows.Runs.Get(owner, name, runId);
            return workflowRun;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get workflow run {RunId} for {Owner}/{Name}", runId, owner, name);
            return null;
        }
    }

    public async Task<bool> CancelWorkflowRunAsync(string owner, string name, long runId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return false;
            }

            _logger.LogDebug("Cancelling workflow run {RunId} for {Owner}/{Name}", runId, owner, name);

            await _gitHubClient.Actions.Workflows.Runs.Cancel(owner, name, runId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel workflow run {RunId} for {Owner}/{Name}", runId, owner, name);
            return false;
        }
    }

    #endregion

    #region Branch and Tag Management

    public async Task<IEnumerable<Branch>?> ListBranchesAsync(string owner, string name, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Listing branches for {Owner}/{Name}", owner, name);

            var branches = await _gitHubClient.Repository.Branch.GetAll(owner, name);
            return branches;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list branches for {Owner}/{Name}", owner, name);
            return null;
        }
    }

    public async Task<Reference?> CreateBranchAsync(string owner, string name, string branchName, string fromBranch = "main", CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Creating branch {BranchName} from {FromBranch} in {Owner}/{Name}", branchName, fromBranch, owner, name);

            // Get the SHA of the source branch
            var sourceBranch = await _gitHubClient.Repository.Branch.Get(owner, name, fromBranch);
            var sha = sourceBranch.Commit.Sha;

            // Create the new branch reference
            var newReference = new NewReference($"refs/heads/{branchName}", sha);
            var reference = await _gitHubClient.Git.Reference.Create(owner, name, newReference);

            return reference;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create branch {BranchName} in {Owner}/{Name}", branchName, owner, name);
            return null;
        }
    }

    public Task<IEnumerable<GitTag>?> ListTagsAsync(string owner, string name, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return Task.FromResult<IEnumerable<GitTag>?>(null);
            }

            _logger.LogDebug("Listing tags for {Owner}/{Name}", owner, name);

            // TODO: Implement proper tag listing - requires repository ID
            // For now, return empty list as the Git API requires repository ID
            return Task.FromResult<IEnumerable<GitTag>?>(new List<GitTag>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list tags for {Owner}/{Name}", owner, name);
            return Task.FromResult<IEnumerable<GitTag>?>(null);
        }
    }

    public async Task<Release?> CreateReleaseAsync(string owner, string name, string tagName, string? releaseName = null, string? body = null, bool isPrerelease = false, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Creating release {TagName} for {Owner}/{Name}", tagName, owner, name);

            var newRelease = new NewRelease(tagName)
            {
                Name = releaseName ?? tagName,
                Body = body ?? string.Empty,
                Prerelease = isPrerelease
            };

            var release = await _gitHubClient.Repository.Release.Create(owner, name, newRelease);
            return release;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create release {TagName} for {Owner}/{Name}", tagName, owner, name);
            return null;
        }
    }

    #endregion

    #region File and Content Management

    public async Task<RepositoryContent?> GetFileAsync(string owner, string name, string path, string? reference = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Getting file {Path} from {Owner}/{Name} at {Reference}", path, owner, name, reference ?? "default");

            IReadOnlyList<RepositoryContent> contents;
            if (!string.IsNullOrEmpty(reference))
            {
                contents = await _gitHubClient.Repository.Content.GetAllContentsByRef(owner, name, path, reference);
            }
            else
            {
                contents = await _gitHubClient.Repository.Content.GetAllContents(owner, name, path);
            }

            return contents.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file {Path} from {Owner}/{Name}", path, owner, name);
            return null;
        }
    }

    public async Task<RepositoryContentChangeSet?> CreateOrUpdateFileAsync(string owner, string name, string path, string content, string message, string? branch = null, string? sha = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Creating/updating file {Path} in {Owner}/{Name}", path, owner, name);

            var request = new CreateFileRequest(message, content, branch);
            // Note: Sha is not needed for CreateFileRequest, it's for UpdateFileRequest

            var result = await _gitHubClient.Repository.Content.CreateFile(owner, name, path, request);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create/update file {Path} in {Owner}/{Name}", path, owner, name);
            return null;
        }
    }

    public async Task<RepositoryContentChangeSet?> DeleteFileAsync(string owner, string name, string path, string message, string sha, string? branch = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Deleting file {Path} from {Owner}/{Name}", path, owner, name);

            var request = new DeleteFileRequest(message, sha, branch);
            await _gitHubClient.Repository.Content.DeleteFile(owner, name, path, request);
            // Return null as the API doesn't return a changeset
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file {Path} from {Owner}/{Name}", path, owner, name);
            return null;
        }
    }

    #endregion

    #region Organization Management

    public async Task<IEnumerable<Organization>?> ListOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Listing organizations for authenticated user");

            var organizations = await _gitHubClient.Organization.GetAllForCurrent();
            return organizations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list organizations");
            return null;
        }
    }

    public async Task<Organization?> GetOrganizationAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Getting organization {Name}", name);

            var organization = await _gitHubClient.Organization.Get(name);
            return organization;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get organization {Name}", name);
            return null;
        }
    }

    public async Task<IEnumerable<User>?> ListOrganizationMembersAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Listing members for organization {Name}", name);

            var members = await _gitHubClient.Organization.Member.GetAll(name);
            return members;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list members for organization {Name}", name);
            return null;
        }
    }

    #endregion

    #region Authentication and User Info

    public async Task<User?> GetUserAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                _logger.LogWarning("GitHub client not initialized");
                return null;
            }

            _logger.LogDebug("Getting authenticated user information");

            var user = await _gitHubClient.User.Current();
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get authenticated user information");
            return null;
        }
    }

    public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gitHubClient == null)
            {
                return false;
            }

            var user = await GetUserAsync(cancellationToken);
            return user != null;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}