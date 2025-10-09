using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Security
{
    /// <summary>
    /// Security component generator
    /// </summary>
    public class SecurityComponentGenerator
    {
        public Task<Dictionary<string, string>> GenerateAsync(TemplateGenerationRequest request, CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, string>();

            if (request.Security.ServiceAccount)
            {
                files["k8s/serviceaccount.yaml"] = GenerateServiceAccount(request);
            }

            if (request.Security.RBAC)
            {
                files["k8s/rbac.yaml"] = GenerateRBAC(request);
            }

            if (request.Security.NetworkPolicies)
            {
                files["k8s/networkpolicy.yaml"] = GenerateNetworkPolicy(request);
            }

            return Task.FromResult(files);
        }

        private string GenerateServiceAccount(TemplateGenerationRequest request)
        {
            return $@"apiVersion: v1
kind: ServiceAccount
metadata:
  name: {request.ServiceName}
  labels:
    app: {request.ServiceName}
";
        }

        private string GenerateRBAC(TemplateGenerationRequest request)
        {
            return $@"apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: {request.ServiceName}
rules:
- apiGroups: [""""]
  resources: [""pods"", ""services""]
  verbs: [""get"", ""list""]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: {request.ServiceName}
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: Role
  name: {request.ServiceName}
subjects:
- kind: ServiceAccount
  name: {request.ServiceName}
";
        }

        private string GenerateNetworkPolicy(TemplateGenerationRequest request)
        {
            return $@"apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: {request.ServiceName}
spec:
  podSelector:
    matchLabels:
      app: {request.ServiceName}
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - podSelector: {{}}
  egress:
  - to:
    - podSelector: {{}}
";
        }
    }
}