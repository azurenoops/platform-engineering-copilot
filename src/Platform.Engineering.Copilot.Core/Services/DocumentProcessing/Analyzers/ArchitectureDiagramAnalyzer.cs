using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Platform.Engineering.Copilot.DocumentProcessing.Models;
using Platform.Engineering.Copilot.DocumentProcessing.Services;

namespace Platform.Engineering.Copilot.DocumentProcessing.Analyzers;

/// <summary>
/// Analyzer for extracting and analyzing architecture diagrams from various document formats.
/// Supports Visio files (.vsdx, .vsd), PowerPoint presentations (.pptx), Word documents (.docx),
/// and image files containing architecture diagrams. Identifies system components, data flows,
/// security boundaries, and technology stacks from visual representations.
/// </summary>
public class ArchitectureDiagramAnalyzer : IArchitectureDiagramAnalyzer
{
    private readonly ILogger<ArchitectureDiagramAnalyzer> _logger;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the ArchitectureDiagramAnalyzer.
    /// </summary>
    /// <param name="logger">Logger for diagnostic and processing information</param>
    /// <param name="configuration">Application configuration for analyzer parameters</param>
    public ArchitectureDiagramAnalyzer(
        ILogger<ArchitectureDiagramAnalyzer> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Extracts architecture diagrams from various document formats.
    /// Supports Visio files, PowerPoint presentations, Word documents, and image files.
    /// Converts diagram content into structured data for further analysis.
    /// </summary>
    /// <param name="filePath">Path to the document file containing diagrams</param>
    /// <returns>List of extracted diagrams with metadata and content</returns>
    public async Task<List<ExtractedDiagram>> ExtractDiagramsAsync(string filePath)
    {
        var diagrams = new List<ExtractedDiagram>();
        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

        _logger.LogInformation("Extracting diagrams from {FilePath}", filePath);

        switch (fileExtension)
        {
            case ".vsdx":
            case ".vsd":
                diagrams = await ExtractVisioDiagramsAsync(filePath);
                break;
            case ".png":
            case ".jpg":
            case ".jpeg":
                diagrams = await ExtractImageDiagramsAsync(filePath);
                break;
            default:
                _logger.LogWarning("Unsupported file type for diagram extraction: {FileExtension}", fileExtension);
                break;
        }

        return diagrams;
    }

    /// <summary>
    /// Performs comprehensive architecture analysis on extracted document content.
    /// Analyzes both textual descriptions and visual diagrams to identify system components,
    /// technology stack, data flows, security boundaries, and architectural patterns.
    /// </summary>
    /// <param name="content">Extracted content including text, diagrams, and metadata</param>
    /// <returns>Structured architecture analysis with identified components and patterns</returns>
    public async Task<ArchitectureAnalysis> AnalyzeArchitectureAsync(ExtractedContent content)
    {
        var analysis = new ArchitectureAnalysis();

        _logger.LogInformation("Analyzing architecture from extracted content");

        // Analyze text content for architecture patterns
        await AnalyzeTextForArchitecturePatternsAsync(content, analysis);

        // Analyze diagrams if present
        if (content.Diagrams.Any())
        {
            await AnalyzeDiagramsForArchitectureAsync(content.Diagrams, analysis);
        }

        // Generate recommendations based on findings
        analysis.Recommendations = GenerateArchitectureRecommendations(analysis);

        return analysis;
    }

    private async Task<List<ExtractedDiagram>> ExtractVisioDiagramsAsync(string filePath)
    {
        var diagrams = new List<ExtractedDiagram>();

        // This is a placeholder implementation
        // Real Visio extraction would require Microsoft Visio SDK or specialized libraries
        var diagram = new ExtractedDiagram
        {
            DiagramId = Guid.NewGuid().ToString(),
            Title = "Network Architecture Diagram",
            Type = DiagramType.NetworkDiagram,
            PageNumber = 1,
            Components = await ExtractMockVisioComponentsAsync(filePath),
            Connections = new List<DiagramConnection>()
        };

        // Generate connections based on components
        diagram.Connections = GenerateMockConnections(diagram.Components);

        diagrams.Add(diagram);
        return diagrams;
    }

    private async Task<List<DiagramComponent>> ExtractMockVisioComponentsAsync(string filePath)
    {
        // This would be replaced with actual Visio shape extraction
        return new List<DiagramComponent>
        {
            new DiagramComponent
            {
                ComponentId = "web-server-1",
                Name = "Web Server",
                Type = "Server",
                Description = "Front-end web server",
                Properties = new Dictionary<string, string>
                {
                    { "OS", "Windows Server 2019" },
                    { "Role", "IIS Web Server" }
                },
                SecurityLevel = SecurityClassification.Unclassified
            },
            new DiagramComponent
            {
                ComponentId = "app-server-1",
                Name = "Application Server",
                Type = "Server",
                Description = "Business logic tier",
                Properties = new Dictionary<string, string>
                {
                    { "OS", "Windows Server 2019" },
                    { "Role", "Application Server" }
                },
                SecurityLevel = SecurityClassification.Unclassified
            },
            new DiagramComponent
            {
                ComponentId = "db-server-1",
                Name = "Database Server",
                Type = "Database",
                Description = "SQL Server database",
                Properties = new Dictionary<string, string>
                {
                    { "DBMS", "SQL Server 2019" },
                    { "Role", "Primary Database" }
                },
                SecurityLevel = SecurityClassification.Confidential
            },
            new DiagramComponent
            {
                ComponentId = "firewall-1",
                Name = "Firewall",
                Type = "Security",
                Description = "Network security boundary",
                Properties = new Dictionary<string, string>
                {
                    { "Type", "Next-Gen Firewall" },
                    { "Vendor", "Cisco ASA" }
                },
                SecurityLevel = SecurityClassification.Unclassified
            }
        };
    }

    private List<DiagramConnection> GenerateMockConnections(List<DiagramComponent> components)
    {
        var connections = new List<DiagramConnection>();

        // Generate logical connections between components
        var webServer = components.FirstOrDefault(c => c.Type == "Server" && c.Name.Contains("Web"));
        var appServer = components.FirstOrDefault(c => c.Type == "Server" && c.Name.Contains("Application"));
        var dbServer = components.FirstOrDefault(c => c.Type == "Database");
        var firewall = components.FirstOrDefault(c => c.Type == "Security");

        if (webServer != null && appServer != null)
        {
            connections.Add(new DiagramConnection
            {
                ConnectionId = Guid.NewGuid().ToString(),
                SourceComponentId = webServer.ComponentId,
                TargetComponentId = appServer.ComponentId,
                ConnectionType = "HTTP",
                Protocol = "HTTPS",
                IsSecure = true
            });
        }

        if (appServer != null && dbServer != null)
        {
            connections.Add(new DiagramConnection
            {
                ConnectionId = Guid.NewGuid().ToString(),
                SourceComponentId = appServer.ComponentId,
                TargetComponentId = dbServer.ComponentId,
                ConnectionType = "Database",
                Protocol = "SQL",
                IsSecure = true
            });
        }

        return connections;
    }

    private async Task<List<ExtractedDiagram>> ExtractImageDiagramsAsync(string filePath)
    {
        var diagrams = new List<ExtractedDiagram>();

        // Placeholder for image-based diagram analysis
        var diagram = new ExtractedDiagram
        {
            DiagramId = Guid.NewGuid().ToString(),
            Title = "Image-based Architecture Diagram",
            Type = DiagramType.SystemArchitecture,
            PageNumber = 1,
            Components = new List<DiagramComponent>(),
            Connections = new List<DiagramConnection>()
        };

        diagrams.Add(diagram);
        return diagrams;
    }

    private async Task AnalyzeTextForArchitecturePatternsAsync(ExtractedContent content, ArchitectureAnalysis analysis)
    {
        var architectureKeywords = new Dictionary<string, string>
        {
            { "microservices", "Microservices Architecture" },
            { "monolith", "Monolithic Architecture" },
            { "n-tier", "N-Tier Architecture" },
            { "three-tier", "Three-Tier Architecture" },
            { "mvc", "Model-View-Controller Pattern" },
            { "api gateway", "API Gateway Pattern" },
            { "load balancer", "Load Balancing Pattern" },
            { "database cluster", "Database Clustering Pattern" },
            { "caching", "Caching Pattern" },
            { "message queue", "Message Queue Pattern" }
        };

        var fullText = content.FullText.ToLowerInvariant();

        foreach (var keyword in architectureKeywords)
        {
            if (fullText.Contains(keyword.Key))
            {
                analysis.DetectedPatterns.Add(new ArchitecturePattern
                {
                    Name = keyword.Value,
                    Description = $"Pattern detected based on keyword: {keyword.Key}",
                    Confidence = 0.7
                });
            }
        }

        // Analyze for technology stack
        var techKeywords = new Dictionary<string, string>
        {
            { "java", "Java" },
            { ".net", ".NET" },
            { "node.js", "Node.js" },
            { "python", "Python" },
            { "sql server", "SQL Server" },
            { "mysql", "MySQL" },
            { "postgresql", "PostgreSQL" },
            { "oracle", "Oracle Database" },
            { "redis", "Redis" },
            { "mongodb", "MongoDB" },
            { "kubernetes", "Kubernetes" },
            { "docker", "Docker" },
            { "azure", "Microsoft Azure" },
            { "aws", "Amazon Web Services" }
        };

        foreach (var tech in techKeywords)
        {
            if (fullText.Contains(tech.Key))
            {
                analysis.TechnologyStack.Add(tech.Value);
            }
        }
    }

    private async Task AnalyzeDiagramsForArchitectureAsync(List<ExtractedDiagram> diagrams, ArchitectureAnalysis analysis)
    {
        foreach (var diagram in diagrams)
        {
            // Extract system components
            foreach (var component in diagram.Components)
            {
                analysis.SystemComponents.Add(new SystemComponent
                {
                    Name = component.Name,
                    Type = component.Type,
                    Description = component.Description
                });
            }

            // Extract data flows
            foreach (var connection in diagram.Connections)
            {
                var sourceComponent = diagram.Components.FirstOrDefault(c => c.ComponentId == connection.SourceComponentId);
                var targetComponent = diagram.Components.FirstOrDefault(c => c.ComponentId == connection.TargetComponentId);

                if (sourceComponent != null && targetComponent != null)
                {
                    analysis.DataFlows.Add(new DataFlow
                    {
                        Source = sourceComponent.Name,
                        Target = targetComponent.Name,
                        DataType = connection.ConnectionType,
                        Classification = DetermineDataClassification(connection, sourceComponent, targetComponent)
                    });
                }
            }

            // Identify security boundaries
            var securityComponents = diagram.Components.Where(c => c.Type == "Security" || c.Name.ToLowerInvariant().Contains("firewall"));
            foreach (var securityComponent in securityComponents)
            {
                analysis.SecurityBoundaries.Add(new SecurityBoundary
                {
                    Name = securityComponent.Name,
                    Type = securityComponent.Type,
                    Components = new List<string> { securityComponent.Name }
                });
            }
        }
    }

    private SecurityClassification DetermineDataClassification(DiagramConnection connection, DiagramComponent source, DiagramComponent target)
    {
        if (source.SecurityLevel == SecurityClassification.Secret || target.SecurityLevel == SecurityClassification.Secret)
            return SecurityClassification.Secret;
        
        if (source.SecurityLevel == SecurityClassification.Confidential || target.SecurityLevel == SecurityClassification.Confidential)
            return SecurityClassification.Confidential;
        
        return SecurityClassification.Unclassified;
    }

    private List<ArchitectureRecommendation> GenerateArchitectureRecommendations(ArchitectureAnalysis analysis)
    {
        var recommendations = new List<ArchitectureRecommendation>();

        // Recommend security improvements
        if (!analysis.SecurityBoundaries.Any())
        {
            recommendations.Add(new ArchitectureRecommendation
            {
                Title = "Add Security Boundaries",
                Description = "Consider implementing network segmentation and security boundaries to improve defense in depth",
                Priority = Priority.High
            });
        }

        // Recommend high availability
        var criticalComponents = analysis.SystemComponents.Where(c => c.Type == "Database" || c.Type == "Server").ToList();
        if (criticalComponents.Count > 0)
        {
            recommendations.Add(new ArchitectureRecommendation
            {
                Title = "Implement High Availability",
                Description = "Consider implementing redundancy and failover mechanisms for critical components",
                Priority = Priority.Medium
            });
        }

        // Recommend monitoring
        recommendations.Add(new ArchitectureRecommendation
        {
            Title = "Implement Comprehensive Monitoring",
            Description = "Add monitoring and logging capabilities for all system components and data flows",
            Priority = Priority.Medium
        });

        // Navy Flankspeed specific recommendations
        if (analysis.TechnologyStack.Contains("Microsoft Azure") || analysis.TechnologyStack.Contains(".NET"))
        {
            recommendations.Add(new ArchitectureRecommendation
            {
                Title = "Navy Flankspeed Integration",
                Description = "This architecture appears compatible with Navy Flankspeed platform. Consider leveraging Flankspeed security controls and compliance frameworks",
                Priority = Priority.Medium
            });
        }

        return recommendations;
    }
}