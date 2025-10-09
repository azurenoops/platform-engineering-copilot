#!/usr/bin/env python3
"""
Script to help convert raw string interpolations to StringBuilder in C# files.
This is a helper script to demonstrate the pattern, not for automated replacement.
"""

def convert_raw_string_to_stringbuilder(raw_string_content):
    """
    Convert a raw string interpolation to StringBuilder pattern.
    
    Example:
    Input: 
        return $"""
            # Title
            **Property:** {value}
            """;
            
    Output:
        var sb = new StringBuilder();
        sb.AppendLine("# Title");
        sb.AppendLine($"**Property:** {value}");
        return sb.ToString();
    """
    lines = raw_string_content.strip().split('\n')
    output = []
    
    # Start with StringBuilder initialization
    output.append("var sb = new StringBuilder();")
    
    # Convert each line
    for line in lines:
        # Skip the opening and closing triple quotes
        if line.strip() in ['return $"""', '""";', '$"""', '"""']:
            continue
            
        # Remove leading whitespace to determine actual content
        content = line.strip()
        
        if not content:
            # Empty line
            output.append("sb.AppendLine();")
        elif '{' in content and '}' in content:
            # Line contains interpolation
            output.append(f'sb.AppendLine($"{content}");')
        else:
            # Regular string line
            output.append(f'sb.AppendLine("{content}");')
    
    # Add return statement
    output.append("return sb.ToString();")
    
    return '\n'.join(output)

# Example usage
if __name__ == "__main__":
    example = '''
            return $"""
                # 📊 Environment Metrics: {name}

                **Retrieved At:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

                ## Performance Overview
                | Metric | Current Value | Status |
                |--------|---------------|---------|
                | CPU Utilization | {GetPropertyValue(result, "cpu")?.ToString() ?? "N/A"}% | {GetMetricStatus("cpu", result)} |
                """;
    '''
    
    print("Original:")
    print(example)
    print("\nConverted:")
    print(convert_raw_string_to_stringbuilder(example))