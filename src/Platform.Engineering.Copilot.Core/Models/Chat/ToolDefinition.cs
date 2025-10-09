using System;
using System.Collections.Generic;

namespace Platform.Engineering.Copilot.Core.Models
{
    public class ToolDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string Category { get; set; } = "General";
        public List<ToolParameter> Parameters { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class ToolParameter
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = "string";
        public bool IsRequired { get; set; }
        public object? DefaultValue { get; set; }
        public List<string>? AllowedValues { get; set; }
        public Dictionary<string, object>? Validation { get; set; }
    }
}