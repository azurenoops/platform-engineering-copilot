using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Engineering.Copilot.Data.Entities;

/// <summary>
/// Semantic intent entity for natural language processing and intent recognition
/// </summary>
[Table("SemanticIntents")]
public class SemanticIntent
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    [StringLength(500)]
    public string UserInput { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string IntentCategory { get; set; } = string.Empty; // environment_management, deployment, scaling, monitoring
    
    [Required]
    [StringLength(100)]
    public string IntentAction { get; set; } = string.Empty; // create, delete, scale, list, deploy
    
    [Column(TypeName = "decimal(5,4)")]
    public decimal Confidence { get; set; } // 0.0000 to 1.0000
    
    public string? ExtractedParameters { get; set; } // JSON extracted parameters
    public string? ResolvedToolCall { get; set; } // JSON resolved MCP tool call
    
    [Required]
    [StringLength(100)]
    public string UserId { get; set; } = string.Empty;
    
    [StringLength(100)]
    public string? SessionId { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public bool WasSuccessful { get; set; } = false;
    public string? ErrorMessage { get; set; }
    
    // Navigation properties
    public virtual ICollection<IntentFeedback> Feedback { get; set; } = new List<IntentFeedback>();
}

/// <summary>
/// Intent feedback entity for machine learning improvement
/// </summary>
[Table("IntentFeedback")]
public class IntentFeedback
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    public Guid IntentId { get; set; }
    
    [Required]
    [StringLength(20)]
    public string FeedbackType { get; set; } = string.Empty; // correct, incorrect, partial
    
    [StringLength(100)]
    public string? CorrectIntentCategory { get; set; }
    
    [StringLength(100)]
    public string? CorrectIntentAction { get; set; }
    
    public string? CorrectParameters { get; set; } // JSON correct parameters
    
    [Required]
    [StringLength(100)]
    public string ProvidedBy { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    [ForeignKey("IntentId")]
    public virtual SemanticIntent Intent { get; set; } = null!;
}

/// <summary>
/// Intent pattern entity for storing learned patterns
/// </summary>
[Table("IntentPatterns")]
public class IntentPattern
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    [StringLength(500)]
    public string Pattern { get; set; } = string.Empty; // Regex or semantic pattern
    
    [Required]
    [StringLength(100)]
    public string IntentCategory { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string IntentAction { get; set; } = string.Empty;
    
    [Column(TypeName = "decimal(5,4)")]
    public decimal Weight { get; set; } = 1.0000m;
    
    public string? ParameterExtractionRules { get; set; } // JSON parameter extraction rules
    
    public int UsageCount { get; set; } = 0;
    public int SuccessCount { get; set; } = 0;
    
    [Column(TypeName = "decimal(5,4)")]
    public decimal SuccessRate { get; set; } = 0.0000m;
    
    [Required]
    [StringLength(100)]
    public string CreatedBy { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public bool IsActive { get; set; } = true;
}