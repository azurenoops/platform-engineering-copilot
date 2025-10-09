using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.CloudFormation
{
    /// <summary>
    /// AWS CloudFormation generator (stub)
    /// </summary>
    public class CloudFormationGenerator : IInfrastructureGenerator
    {
        public Task<Dictionary<string, string>> GenerateAsync(TemplateGenerationRequest request, CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, string>();
            files["infra/template.yaml"] = "# AWS CloudFormation template - to be implemented";
            return Task.FromResult(files);
        }
    }
}
