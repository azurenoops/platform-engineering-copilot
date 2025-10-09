using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Observability
{
    /// <summary>
    /// Observability component generator
    /// </summary>
    public class ObservabilityComponentGenerator
    {
        public Task<Dictionary<string, string>> GenerateAsync(TemplateGenerationRequest request, CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, string>();

            if (request.Observability.Prometheus)
            {
                files["monitoring/servicemonitor.yaml"] = GenerateServiceMonitor(request);
            }

            if (request.Observability.Grafana)
            {
                files["monitoring/dashboard.json"] = GenerateGrafanaDashboard(request);
            }

            return Task.FromResult(files);
        }

        private string GenerateServiceMonitor(TemplateGenerationRequest request)
        {
            return $@"apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: {request.ServiceName}
spec:
  selector:
    matchLabels:
      app: {request.ServiceName}
  endpoints:
  - port: http
    path: /metrics
    interval: 30s
";
        }

        private string GenerateGrafanaDashboard(TemplateGenerationRequest request)
        {
            // Use regular string concatenation to avoid escaping issues
            var json = "{\n";
            json += "  \"dashboard\": {\n";
            json += $"    \"title\": \"{request.ServiceName} Dashboard\",\n";
            json += "    \"panels\": [\n";
            json += "      {\n";
            json += "        \"title\": \"Request Rate\",\n";
            json += "        \"targets\": [\n";
            json += "          {\n";
            json += $"            \"expr\": \"rate(http_requests_total{{service=\\\"{request.ServiceName}\\\"}}[5m])\"\n";
            json += "          }\n";
            json += "        ]\n";
            json += "      }\n";
            json += "    ]\n";
            json += "  }\n";
            json += "}";
            return json;
        }
    }
}
