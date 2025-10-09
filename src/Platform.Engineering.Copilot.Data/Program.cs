using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Platform.Engineering.Copilot.Data.Extensions;
using Platform.Engineering.Copilot.Data.Services;

namespace Platform.Engineering.Copilot.Data.Tests;

/// <summary>
/// Simple console application to test database setup
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        // Use the validator instead of the hosted service approach
        await DatabaseValidator.ValidateAsync(args);
    }
}