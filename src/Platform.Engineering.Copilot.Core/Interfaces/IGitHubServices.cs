using Octokit;

namespace Platform.Engineering.Copilot.Core.Interfaces
{
    /// <summary>
    /// Gateway service interface for GitHub operations
    /// </summary>
    public interface IGitHubServices
    {
        #region Repository Operations

        /// <summary>
        /// Get repository information
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="name">Repository name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Repository information</returns>
        Task<Repository?> GetRepositoryAsync(string owner, string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// List repositories for a user or organization
        /// </summary>
        /// <param name="owner">User or organization name</param>
        /// <param name="type">Type of repositories (all, public, private)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of repositories</returns>
        Task<IEnumerable<Repository>?> ListRepositoriesAsync(string? owner = null, string type = "all", CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a new repository
        /// </summary>
        /// <param name="name">Repository name</param>
        /// <param name="description">Repository description</param>
        /// <param name="isPrivate">Whether the repository should be private</param>
        /// <param name="owner">Organization owner (null for user repositories)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Created repository</returns>
        Task<Repository?> CreateRepositoryAsync(string name, string? description = null, bool isPrivate = false, string? owner = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fork a repository
        /// </summary>
        /// <param name="owner">Original repository owner</param>
        /// <param name="name">Original repository name</param>
        /// <param name="organization">Target organization (null for user fork)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Forked repository</returns>
        Task<Repository?> ForkRepositoryAsync(string owner, string name, string? organization = null, CancellationToken cancellationToken = default);

        #endregion

        #region Issue Management

        /// <summary>
        /// Create a new issue
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="name">Repository name</param>
        /// <param name="title">Issue title</param>
        /// <param name="body">Issue body</param>
        /// <param name="labels">Issue labels</param>
        /// <param name="assignees">Issue assignees</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Created issue</returns>
        Task<Issue?> CreateIssueAsync(string owner, string name, string title, string? body = null, string[]? labels = null, string[]? assignees = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// List issues for a repository
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="name">Repository name</param>
        /// <param name="state">Issue state (open, closed, all)</param>
        /// <param name="labels">Filter by labels</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of issues</returns>
        Task<IEnumerable<Issue>?> ListIssuesAsync(string owner, string name, string state = "open", string[]? labels = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update an issue
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="name">Repository name</param>
        /// <param name="issueNumber">Issue number</param>
        /// <param name="title">New title</param>
        /// <param name="body">New body</param>
        /// <param name="state">New state (open, closed)</param>
        /// <param name="labels">New labels</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Updated issue</returns>
        Task<Issue?> UpdateIssueAsync(string owner, string name, int issueNumber, string? title = null, string? body = null, string? state = null, string[]? labels = null, CancellationToken cancellationToken = default);

        #endregion

        #region Pull Request Management

        /// <summary>
        /// Create a pull request
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="name">Repository name</param>
        /// <param name="title">Pull request title</param>
        /// <param name="body">Pull request body</param>
        /// <param name="head">Head branch</param>
        /// <param name="baseRef">Base branch</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Created pull request</returns>
        Task<PullRequest?> CreatePullRequestAsync(string owner, string name, string title, string? body, string head, string baseRef, CancellationToken cancellationToken = default);

        /// <summary>
        /// List pull requests for a repository
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="name">Repository name</param>
        /// <param name="state">Pull request state (open, closed, all)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of pull requests</returns>
        Task<IEnumerable<PullRequest>?> ListPullRequestsAsync(string owner, string name, string state = "open", CancellationToken cancellationToken = default);

        /// <summary>
        /// Merge a pull request
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="name">Repository name</param>
        /// <param name="pullNumber">Pull request number</param>
        /// <param name="commitMessage">Merge commit message</param>
        /// <param name="mergeMethod">Merge method (merge, squash, rebase)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Merge result</returns>
        Task<PullRequestMerge?> MergePullRequestAsync(string owner, string name, int pullNumber, string? commitMessage = null, PullRequestMergeMethod mergeMethod = PullRequestMergeMethod.Merge, CancellationToken cancellationToken = default);

        #endregion

        #region Workflow Management

        /// <summary>
        /// List workflow runs for a repository
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="name">Repository name</param>
        /// <param name="workflowId">Workflow ID (optional)</param>
        /// <param name="status">Run status filter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of workflow runs</returns>
        Task<IEnumerable<WorkflowRun>?> ListWorkflowRunsAsync(string owner, string name, long? workflowId = null, string? status = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Trigger a workflow dispatch
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="name">Repository name</param>
        /// <param name="workflowId">Workflow ID</param>
        /// <param name="branch">Branch to run on</param>
        /// <param name="inputs">Workflow inputs</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Success indicator</returns>
        Task<bool> TriggerWorkflowDispatchAsync(string owner, string name, string workflowId, string branch = "main", Dictionary<string, object>? inputs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get workflow run details
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="name">Repository name</param>
        /// <param name="runId">Workflow run ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Workflow run details</returns>
        Task<WorkflowRun?> GetWorkflowRunAsync(string owner, string name, long runId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancel a workflow run
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="name">Repository name</param>
        /// <param name="runId">Workflow run ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Success indicator</returns>
        Task<bool> CancelWorkflowRunAsync(string owner, string name, long runId, CancellationToken cancellationToken = default);

        #endregion

        #region Branch and Tag Management

        /// <summary>
        /// List branches for a repository
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="name">Repository name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of branches</returns>
        Task<IEnumerable<Branch>?> ListBranchesAsync(string owner, string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a branch
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="name">Repository name</param>
        /// <param name="branchName">New branch name</param>
        /// <param name="fromBranch">Source branch (default: main)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Created branch reference</returns>
        Task<Reference?> CreateBranchAsync(string owner, string name, string branchName, string fromBranch = "main", CancellationToken cancellationToken = default);

        /// <summary>
        /// List tags for a repository
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="name">Repository name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of tags</returns>
        Task<IEnumerable<GitTag>?> ListTagsAsync(string owner, string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a release
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="name">Repository name</param>
        /// <param name="tagName">Tag name</param>
        /// <param name="releaseName">Release name</param>
        /// <param name="body">Release notes</param>
        /// <param name="isPrerelease">Whether this is a prerelease</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Created release</returns>
        Task<Release?> CreateReleaseAsync(string owner, string name, string tagName, string? releaseName = null, string? body = null, bool isPrerelease = false, CancellationToken cancellationToken = default);

        #endregion

        #region File and Content Management

        /// <summary>
        /// Get file contents from a repository
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="name">Repository name</param>
        /// <param name="path">File path</param>
        /// <param name="reference">Branch or commit reference</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>File contents</returns>
        Task<RepositoryContent?> GetFileAsync(string owner, string name, string path, string? reference = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create or update a file in a repository
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="name">Repository name</param>
        /// <param name="path">File path</param>
        /// <param name="content">File content</param>
        /// <param name="message">Commit message</param>
        /// <param name="branch">Target branch</param>
        /// <param name="sha">Current file SHA (for updates)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>File change result</returns>
        Task<RepositoryContentChangeSet?> CreateOrUpdateFileAsync(string owner, string name, string path, string content, string message, string? branch = null, string? sha = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a file from a repository
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="name">Repository name</param>
        /// <param name="path">File path</param>
        /// <param name="message">Commit message</param>
        /// <param name="sha">Current file SHA</param>
        /// <param name="branch">Target branch</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Delete result</returns>
        Task<RepositoryContentChangeSet?> DeleteFileAsync(string owner, string name, string path, string message, string sha, string? branch = null, CancellationToken cancellationToken = default);

        #endregion

        #region Organization Management

        /// <summary>
        /// List organizations for the authenticated user
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of organizations</returns>
        Task<IEnumerable<Organization>?> ListOrganizationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get organization information
        /// </summary>
        /// <param name="name">Organization name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Organization information</returns>
        Task<Organization?> GetOrganizationAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// List organization members
        /// </summary>
        /// <param name="name">Organization name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of organization members</returns>
        Task<IEnumerable<User>?> ListOrganizationMembersAsync(string name, CancellationToken cancellationToken = default);

        #endregion

        #region Authentication and User Info

        /// <summary>
        /// Get authenticated user information
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>User information</returns>
        Task<User?> GetUserAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if the service is properly authenticated
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Authentication status</returns>
        Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default);

        #endregion
    }
}