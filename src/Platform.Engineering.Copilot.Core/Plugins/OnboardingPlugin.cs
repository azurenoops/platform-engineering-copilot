using Microsoft.SemanticKernel;
using System.ComponentModel;
using Platform.Engineering.Copilot.Core.Contracts;
using Platform.Engineering.Copilot.Core.Models;
using Microsoft.Extensions.Logging;

namespace Platform.Engineering.Copilot.Core.Plugins;

/// <summary>
/// Semantic Kernel plugin for Navy Flankspeed mission owner onboarding
/// </summary>
public class OnboardingPlugin : BaseSupervisorPlugin
{
    private readonly IMcpToolHandler _onboardingToolHandler;

    public OnboardingPlugin(
        IMcpToolHandler onboardingToolHandler,
        ILogger<OnboardingPlugin> logger,
        Kernel kernel) : base(logger, kernel)
    {
        _onboardingToolHandler = onboardingToolHandler ?? throw new ArgumentNullException(nameof(onboardingToolHandler));
    }

    [KernelFunction("start_flankspeed_onboarding")]
    [Description("Start a new Navy Flankspeed mission owner onboarding session. Initiates a guided conversation to collect mission details, technical requirements, and compliance needs. Use when user wants to: start onboarding, onboard mission, request Flankspeed access, begin provisioning, or set up new mission environment.")]
    public async Task<string> StartFlankspeedOnboardingAsync(
        [Description("Mission name or identifier. Optional - will be asked during conversation if not provided.")] string? missionName = null,
        [Description("Mission owner email or contact. Optional - will be asked during conversation if not provided.")] string? ownerContact = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var toolCall = new McpToolCall
            {
                Name = "flankspeed_start_onboarding",
                Arguments = new Dictionary<string, object?>
                {
                    ["mission_name"] = missionName,
                    ["owner_contact"] = ownerContact
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _onboardingToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("start Flankspeed onboarding", ex);
        }
    }

    [KernelFunction("provide_onboarding_info")]
    [Description("Provide information during an active Flankspeed onboarding session. Answers questions about mission details, technical requirements, data classification, compliance needs, and resource requests. Use when user is responding to onboarding questions or providing mission information.")]
    public async Task<string> ProvideOnboardingInfoAsync(
        [Description("The information or response provided by the mission owner. Can be mission details, classification level, required resources, compliance requirements, etc.")] string response,
        [Description("Conversation or session ID for the active onboarding session. Optional - uses current active session if available.")] string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var toolCall = new McpToolCall
            {
                Name = "flankspeed_provide_info",
                Arguments = new Dictionary<string, object?>
                {
                    ["response"] = response,
                    ["session_id"] = sessionId
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _onboardingToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("provide onboarding information", ex);
        }
    }

    [KernelFunction("review_onboarding_submission")]
    [Description("Review and submit the completed Flankspeed onboarding request. Shows a summary of all collected information and allows mission owner to review before final submission. Use when user wants to: review submission, check details, submit request, or finalize onboarding.")]
    public async Task<string> ReviewOnboardingSubmissionAsync(
        [Description("Conversation or session ID for the onboarding session to review. Optional - uses current active session if available.")] string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var toolCall = new McpToolCall
            {
                Name = "flankspeed_provide_info",
                Arguments = new Dictionary<string, object?>
                {
                    ["command"] = "review",
                    ["session_id"] = sessionId
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _onboardingToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("review onboarding submission", ex);
        }
    }

    [KernelFunction("submit_onboarding_request")]
    [Description("Submit the completed Flankspeed onboarding request for approval and provisioning. Finalizes the request and triggers the approval workflow. Use when user confirms submission after reviewing their information.")]
    public async Task<string> SubmitOnboardingRequestAsync(
        [Description("Conversation or session ID for the onboarding session to submit. Optional - uses current active session if available.")] string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var toolCall = new McpToolCall
            {
                Name = "flankspeed_provide_info",
                Arguments = new Dictionary<string, object?>
                {
                    ["command"] = "submit",
                    ["session_id"] = sessionId
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _onboardingToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("submit onboarding request", ex);
        }
    }

    [KernelFunction("cancel_onboarding")]
    [Description("Cancel an active Flankspeed onboarding session. Discards all collected information and exits the onboarding workflow. Use when user wants to: cancel onboarding, exit conversation, abort request, or start over.")]
    public async Task<string> CancelOnboardingAsync(
        [Description("Conversation or session ID for the onboarding session to cancel. Optional - uses current active session if available.")] string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var toolCall = new McpToolCall
            {
                Name = "flankspeed_cancel_onboarding",
                Arguments = new Dictionary<string, object?>
                {
                    ["session_id"] = sessionId
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _onboardingToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("cancel onboarding", ex);
        }
    }

    [KernelFunction("check_onboarding_status")]
    [Description("Check the status of a Flankspeed onboarding request. Shows current status, approval progress, provisioning updates, and next steps. Use when user wants to: check status, see progress, get updates, or track onboarding request.")]
    public async Task<string> CheckOnboardingStatusAsync(
        [Description("Request ID or session ID to check status for. Optional - shows status of all requests for current user if not specified.")] string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var toolCall = new McpToolCall
            {
                Name = "flankspeed_provide_info",
                Arguments = new Dictionary<string, object?>
                {
                    ["command"] = "status",
                    ["request_id"] = requestId
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _onboardingToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("check onboarding status", ex);
        }
    }
}
