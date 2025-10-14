using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Services.Parsing;
using System;
using System.Threading.Tasks;

namespace Platform.Engineering.Copilot.Tests.Manual;

/// <summary>
/// Manual test runner for RequirementsParser to verify parsing strategies work
/// </summary>
public class RequirementsParserManualTest
{
    public static async Task Main(string[] args)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        var logger = loggerFactory.CreateLogger<RequirementsParser>();
        var parser = new RequirementsParser(logger);

        Console.WriteLine("=== RequirementsParser Manual Test ===\n");

        // Test 1: JSON Format
        Console.WriteLine("Test 1: JSON Format");
        var json = @"{""classificationLevel"": ""Secret"", ""environmentType"": ""Production"", ""region"": ""US Gov Virginia""}";
        var result1 = await parser.ParseAsync(json);
        PrintResult(result1);

        // Test 2: Bullet List Format
        Console.WriteLine("\nTest 2: Bullet List Format");
        var bulletList = @"
- Classification: Secret
- Environment: Production
- Region: US Gov Virginia
- Services: AKS cluster, Azure SQL";
        var result2 = await parser.ParseAsync(bulletList);
        PrintResult(result2);

        // Test 3: Comma-Separated Format
        Console.WriteLine("\nTest 3: Comma-Separated Format");
        var commaSeparated = "Classification is Secret, environment is Production, region is US Gov Virginia";
        var result3 = await parser.ParseAsync(commaSeparated);
        PrintResult(result3);

        // Test 4: Natural Language
        Console.WriteLine("\nTest 4: Natural Language");
        var naturalLanguage = "We need a Secret classification production environment in US Gov Virginia";
        var result4 = await parser.ParseAsync(naturalLanguage);
        PrintResult(result4);

        // Test 5: Mixed Format (Real User Example)
        Console.WriteLine("\nTest 5: Real User Example");
        var realExample = @"Onboard Mission Alpha for NAVSEA:
- Classification: Secret
- Environment type: Production
- Region: US Gov Virginia
- Required services: AKS cluster, Azure SQL Database
- Network requirements: VNet isolation, private endpoints
- Compliance frameworks: FedRAMP High, NIST 800-53";
        var result5 = await parser.ParseAsync(realExample);
        PrintResult(result5);

        Console.WriteLine("\n=== All Tests Complete ===");
    }

    private static void PrintResult(Dictionary<string, object?> result)
    {
        Console.WriteLine($"Extracted {result.Count} fields:");
        foreach (var kvp in result)
        {
            Console.WriteLine($"  - {kvp.Key}: {kvp.Value}");
        }
    }
}
