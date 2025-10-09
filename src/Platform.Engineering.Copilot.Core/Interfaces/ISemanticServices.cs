using Platform.Engineering.Copilot.Core.Models.SemanticParsing;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Interfaces;

/// <summary>
/// Service for advanced natural language processing and query parsing
/// NOTE: This interface is obsolete. Semantic Kernel now handles query parsing,
/// intent classification, tool selection, and parameter extraction automatically
/// via ToolCallBehavior.AutoInvokeKernelFunctions. New code should use SK plugins.
/// </summary>
[Obsolete("Use Semantic Kernel plugins with automatic function calling instead. See IntelligentChatService_v2 and docs/SEMANTIC-KERNEL-V2-MIGRATION.md")]
public interface ISemanticQueryProcessor
{
    /// <summary>
    /// Parse a natural language query and extract intent, entities, and parameters
    /// </summary>
    Task<ParsedQuery> ParseQueryAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the best matching tool for a parsed query
    /// </summary>
    Task<ToolSuggestion?> GetBestToolAsync(ParsedQuery parsedQuery, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract parameters for a specific tool from a natural language query
    /// </summary>
    Task<Dictionary<string, object>> ExtractParametersAsync(string query, ToolSchema toolSchema, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate that all required parameters are present for a tool
    /// </summary>
    Task<(bool IsValid, List<string> MissingParameters)> ValidateParametersAsync(ToolSchema toolSchema, Dictionary<string, object> parameters);
}

/// <summary>
/// Registry for managing tool schemas and their metadata
/// </summary>
public interface IToolSchemaRegistry
{
    /// <summary>
    /// Register a tool schema in the registry
    /// </summary>
    Task RegisterToolAsync(ToolSchema toolSchema);

    /// <summary>
    /// Get all registered tool schemas
    /// </summary>
    Task<IEnumerable<ToolSchema>> GetAllToolsAsync();

    /// <summary>
    /// Get a specific tool schema by name
    /// </summary>
    Task<ToolSchema?> GetToolAsync(string toolName);

    /// <summary>
    /// Search for tools by category
    /// </summary>
    Task<IEnumerable<ToolSchema>> GetToolsByCategoryAsync(IntentCategory category);

    /// <summary>
    /// Search for tools by keywords
    /// </summary>
    Task<IEnumerable<ToolSchema>> SearchToolsAsync(params string[] keywords);

    /// <summary>
    /// Update tool schema metadata
    /// </summary>
    Task UpdateToolAsync(ToolSchema toolSchema);

    /// <summary>
    /// Remove a tool from the registry
    /// </summary>
    Task RemoveToolAsync(string toolName);
}

/// <summary>
/// Service for intent classification and entity extraction
/// NOTE: This interface is obsolete. Semantic Kernel now handles intent
/// classification automatically via ToolCallBehavior.AutoInvokeKernelFunctions.
/// New code should use SK plugins instead. See IntelligentChatService_v2.
/// </summary>
[Obsolete("Use Semantic Kernel plugins with automatic function calling instead. See IntelligentChatService_v2 and docs/SEMANTIC-KERNEL-V2-MIGRATION.md")]
public interface IIntentClassifier
{
    /// <summary>
    /// Classify the intent of a natural language query
    /// </summary>
    Task<QueryIntent> ClassifyIntentAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract named entities from text
    /// </summary>
    Task<Dictionary<string, object>> ExtractEntitiesAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get intent confidence score
    /// </summary>
    Task<double> GetIntentConfidenceAsync(string query, IntentCategory category, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for parameter extraction and validation
/// NOTE: This interface is obsolete. Semantic Kernel now handles parameter
/// extraction automatically when invoking functions using KernelFunction
/// parameter descriptions. New code should use SK plugins instead.
/// </summary>
[Obsolete("Use Semantic Kernel plugins with [Description] attributes on parameters instead. See IntelligentChatService_v2 and docs/SEMANTIC-KERNEL-V2-MIGRATION.md")]
public interface IParameterExtractor
{
    /// <summary>
    /// Extract parameters from natural language based on schema
    /// </summary>
    Task<Dictionary<string, object>> ExtractAsync(string text, Dictionary<string, ParameterSchema> parameterSchemas, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract a specific parameter value
    /// </summary>
    Task<object?> ExtractParameterAsync(string text, ParameterSchema parameterSchema, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate extracted parameters against schema
    /// </summary>
    Task<(bool IsValid, List<string> Errors)> ValidateAsync(Dictionary<string, object> parameters, Dictionary<string, ParameterSchema> parameterSchemas);
}

/// <summary>
/// Service for semantic kernel integration and AI-powered processing
/// </summary>
public interface ISemanticKernelService
{
    /// <summary>
    /// Process query using semantic kernel
    /// </summary>
    Task<string> ProcessQueryAsync(string query, string context = "", CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate tool selection reasoning
    /// </summary>
    Task<string> GenerateToolSelectionReasoningAsync(string query, IEnumerable<ToolSchema> availableTools, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate parameter extraction suggestions
    /// </summary>
    Task<Dictionary<string, object>> GenerateParameterSuggestionsAsync(string query, ToolSchema toolSchema, CancellationToken cancellationToken = default);

    /// <summary>
    /// Format response with natural language explanation
    /// </summary>
    Task<string> FormatResponseAsync(object result, string originalQuery, string toolName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze natural language request for template generation requirements
    /// </summary>
    Task<TemplateIntent> AnalyzeTemplateIntentAsync(string userRequest, CancellationToken cancellationToken = default);
}