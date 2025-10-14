#!/usr/bin/env python3
"""
Fix scanner files: replace Evidence dictionary with JsonSerializer.Serialize
and fix control.Description references
"""
import re
import sys

def fix_scanner_file(filepath):
    with open(filepath, 'r') as f:
        content = f.read()
    
    # Fix Evidence = new Dictionary<string, object> { ... }
    # This regex finds the Evidence assignment pattern
    content = re.sub(
        r'Evidence = new Dictionary<string, object>\s*\{([^}]+)\}',
        r'Evidence = JsonSerializer.Serialize(new Dictionary<string, object> {\1})',
        content,
        flags=re.DOTALL
    )
    
    # Fix control.Description to control.Title ?? "Manual Review"
    content = re.sub(
        r'\$"\{control\.Title\}: \{control\.Description\}"',
        r'control.Title ?? "Manual review required for this control"',
        content
    )
    
    with open(filepath, 'w') as f:
        f.write(content)
    
    print(f"Fixed {filepath}")

if __name__ == "__main__":
    scanner_files = [
        "src/Platform.Engineering.Copilot.Core/Services/Compliance/Scanners/ContingencyPlanningScanner.cs",
        "src/Platform.Engineering.Copilot.Core/Services/Compliance/Scanners/IdentificationAuthenticationScanner.cs",
        "src/Platform.Engineering.Copilot.Core/Services/Compliance/Scanners/ConfigurationManagementScanner.cs",
        "src/Platform.Engineering.Copilot.Core/Services/Compliance/Scanners/IncidentResponseScanner.cs",
        "src/Platform.Engineering.Copilot.Core/Services/Compliance/Scanners/RiskAssessmentScanner.cs",
        "src/Platform.Engineering.Copilot.Core/Services/Compliance/Scanners/SecurityAssessmentScanner.cs",
    ]
    
    for filepath in scanner_files:
        try:
            fix_scanner_file(filepath)
        except Exception as e:
            print(f"Error fixing {filepath}: {e}", file=sys.stderr)
