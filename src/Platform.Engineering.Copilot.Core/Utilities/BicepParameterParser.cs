using System.Text.RegularExpressions;
using Platform.Engineering.Copilot.Core.Models.ServiceTemplates;

namespace Platform.Engineering.Copilot.Core.Utilities;

/// <summary>
/// Parses Bicep templates to extract parameter definitions
/// </summary>
public static class BicepParameterParser
{
    /// <summary>
    /// Parse Bicep template content and extract parameter definitions
    /// </summary>
    public static List<TemplateParameter> ParseParameters(string bicepContent)
    {
        var parameters = new List<TemplateParameter>();
        
        if (string.IsNullOrWhiteSpace(bicepContent))
            return parameters;

        // Remove block comments /* ... */
        bicepContent = Regex.Replace(bicepContent, @"/\*[\s\S]*?\*/", "", RegexOptions.Multiline);
        
        // Pattern to match parameter declarations with decorators
        // Captures: decorators (optional), param name, type, default value (optional)
        var paramPattern = @"(?:(?<decorator>@[^\r\n]+)\s*)*\s*param\s+(?<name>\w+)\s+(?<type>\w+(?:<[^>]+>)?)\s*(?:=\s*(?<default>[^\r\n]+))?";
        
        var matches = Regex.Matches(bicepContent, paramPattern, RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            var name = match.Groups["name"].Value;
            var bicepType = match.Groups["type"].Value;
            var defaultValue = match.Groups["default"].Value.Trim();
            
            // Extract decorators for this parameter
            var decorators = match.Groups["decorator"].Captures
                .Cast<Capture>()
                .Select(c => c.Value.Trim())
                .ToList();

            var parameter = new TemplateParameter
            {
                Name = name,
                DisplayName = SplitCamelCase(name),
                Type = MapBicepTypeToParameterType(bicepType),
                Required = string.IsNullOrEmpty(defaultValue),
                DefaultValue = string.IsNullOrEmpty(defaultValue) ? null : CleanDefaultValue(defaultValue),
                Description = ExtractDescription(decorators) ?? string.Empty
            };

            // Extract validation rules and apply to parameter
            ExtractAndApplyValidationRules(parameter, decorators, bicepType);

            // Extract allowed values
            var allowedValues = ExtractAllowedValues(decorators);
            if (allowedValues.Any())
            {
                parameter.AllowedValues = allowedValues;
                parameter.Type = ParameterType.Choice; // Use dropdown for restricted values
            }

            parameters.Add(parameter);
        }

        return parameters;
    }

    private static ParameterType MapBicepTypeToParameterType(string bicepType)
    {
        // Remove generic type parameters (e.g., array<string> -> array)
        var baseType = Regex.Replace(bicepType, @"<.*>", "").ToLower();
        
        return baseType switch
        {
            "string" => ParameterType.String,
            "int" => ParameterType.Number,
            "bool" => ParameterType.Boolean,
            "object" => ParameterType.String, // No Object type, use String
            "array" => ParameterType.String, // No Array type, use String
            "securestring" => ParameterType.Secret,
            "secureobject" => ParameterType.Secret,
            _ => ParameterType.String
        };
    }

    private static string? ExtractDescription(List<string> decorators)
    {
        var descDecorator = decorators.FirstOrDefault(d => d.StartsWith("@description("));
        if (descDecorator == null)
            return null;

        // Extract text between quotes
        var match = Regex.Match(descDecorator, @"@description\(\s*'([^']+)'|@description\(\s*""([^""]+)""");
        return match.Success ? (match.Groups[1].Value + match.Groups[2].Value) : null;
    }

    private static List<string> ExtractAllowedValues(List<string> decorators)
    {
        var allowedDecorator = decorators.FirstOrDefault(d => d.StartsWith("@allowed("));
        if (allowedDecorator == null)
            return new List<string>();

        // Extract array values: @allowed(['value1', 'value2'])
        var match = Regex.Match(allowedDecorator, @"\[([^\]]+)\]");
        if (!match.Success)
            return new List<string>();

        var valuesText = match.Groups[1].Value;
        
        // Extract individual quoted values
        var values = Regex.Matches(valuesText, @"'([^']+)'|""([^""]+)""")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value + m.Groups[2].Value)
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();

        return values;
    }

    private static void ExtractAndApplyValidationRules(TemplateParameter parameter, List<string> decorators, string bicepType)
    {
        // Extract minValue/maxValue for numbers
        if (bicepType.ToLower() == "int")
        {
            var minValue = decorators.FirstOrDefault(d => d.StartsWith("@minValue("));
            if (minValue != null)
            {
                var match = Regex.Match(minValue, @"@minValue\((\d+)\)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var min))
                    parameter.MinValue = min;
            }

            var maxValue = decorators.FirstOrDefault(d => d.StartsWith("@maxValue("));
            if (maxValue != null)
            {
                var match = Regex.Match(maxValue, @"@maxValue\((\d+)\)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var max))
                    parameter.MaxValue = max;
            }
        }

        // Extract minLength/maxLength for strings
        if (bicepType.ToLower() == "string")
        {
            var minLength = decorators.FirstOrDefault(d => d.StartsWith("@minLength("));
            if (minLength != null)
            {
                var match = Regex.Match(minLength, @"@minLength\((\d+)\)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var min))
                    parameter.MinLength = min;
            }

            var maxLength = decorators.FirstOrDefault(d => d.StartsWith("@maxLength("));
            if (maxLength != null)
            {
                var match = Regex.Match(maxLength, @"@maxLength\((\d+)\)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var max))
                    parameter.MaxLength = max;
            }
        }
    }

    private static Dictionary<string, object> ExtractValidationRules(List<string> decorators, string bicepType)
    {
        var rules = new Dictionary<string, object>();

        // Extract minValue/maxValue for numbers
        if (bicepType.ToLower() == "int")
        {
            var minValue = decorators.FirstOrDefault(d => d.StartsWith("@minValue("));
            if (minValue != null)
            {
                var match = Regex.Match(minValue, @"@minValue\((\d+)\)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var min))
                    rules["min"] = min;
            }

            var maxValue = decorators.FirstOrDefault(d => d.StartsWith("@maxValue("));
            if (maxValue != null)
            {
                var match = Regex.Match(maxValue, @"@maxValue\((\d+)\)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var max))
                    rules["max"] = max;
            }
        }

        // Extract minLength/maxLength for strings
        if (bicepType.ToLower() == "string")
        {
            var minLength = decorators.FirstOrDefault(d => d.StartsWith("@minLength("));
            if (minLength != null)
            {
                var match = Regex.Match(minLength, @"@minLength\((\d+)\)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var min))
                    rules["minLength"] = min;
            }

            var maxLength = decorators.FirstOrDefault(d => d.StartsWith("@maxLength("));
            if (maxLength != null)
            {
                var match = Regex.Match(maxLength, @"@maxLength\((\d+)\)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var max))
                    rules["maxLength"] = max;
            }
        }

        return rules;
    }

    private static string CleanDefaultValue(string defaultValue)
    {
        // Remove quotes from string defaults
        defaultValue = defaultValue.Trim();
        if ((defaultValue.StartsWith("'") && defaultValue.EndsWith("'")) ||
            (defaultValue.StartsWith("\"") && defaultValue.EndsWith("\"")))
        {
            return defaultValue[1..^1];
        }
        return defaultValue;
    }

    private static string SplitCamelCase(string input)
    {
        // Convert camelCase to "Camel Case" for display
        return Regex.Replace(input, "([a-z])([A-Z])", "$1 $2");
    }
}
