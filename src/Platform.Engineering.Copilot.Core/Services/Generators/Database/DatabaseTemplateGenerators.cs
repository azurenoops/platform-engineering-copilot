using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using YamlDotNet.Serialization;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Database
{
    /// <summary>
    /// PostgreSQL template generator
    /// </summary>
    public class PostgreSQLTemplateGenerator : IDatabaseTemplateGenerator
    {
        private readonly ISerializer _yamlSerializer;

        public PostgreSQLTemplateGenerator()
        {
            _yamlSerializer = new SerializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                .Build();
        }

        public Task<Dictionary<string, string>> GenerateAsync(
            TemplateGenerationRequest request, 
            DatabaseSpec dbSpec, 
            CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, string>();

            if (dbSpec.Location == DatabaseLocation.Kubernetes)
            {
                // Generate K8s StatefulSet for in-cluster PostgreSQL
                files[$"k8s/database-{dbSpec.Name}-statefulset.yaml"] = GenerateStatefulSet(request, dbSpec);
                files[$"k8s/database-{dbSpec.Name}-service.yaml"] = GenerateService(request, dbSpec);
                files[$"k8s/database-{dbSpec.Name}-pvc.yaml"] = GeneratePVC(request, dbSpec);
                files[$"k8s/database-{dbSpec.Name}-configmap.yaml"] = GenerateConfigMap(request, dbSpec);
            }
            else if (dbSpec.Location == DatabaseLocation.Cloud)
            {
                // Cloud provisioning handled by infrastructure generator (Bicep/Terraform)
                files[$"config/database-{dbSpec.Name}-connection.yaml"] = GenerateConnectionConfig(request, dbSpec);
            }
            else if (dbSpec.Location == DatabaseLocation.External)
            {
                // External connection only
                files[$"config/database-{dbSpec.Name}-external.yaml"] = GenerateExternalConfig(request, dbSpec);
            }

            return Task.FromResult(files);
        }

        private string GenerateStatefulSet(TemplateGenerationRequest request, DatabaseSpec dbSpec)
        {
            var statefulSet = new
            {
                apiVersion = "apps/v1",
                kind = "StatefulSet",
                metadata = new
                {
                    name = $"postgres-{dbSpec.Name}",
                    labels = new Dictionary<string, string>
                    {
                        ["app"] = $"postgres-{dbSpec.Name}",
                        ["database"] = "postgresql"
                    }
                },
                spec = new
                {
                    serviceName = $"postgres-{dbSpec.Name}",
                    replicas = dbSpec.HighAvailability ? 3 : 1,
                    selector = new
                    {
                        matchLabels = new Dictionary<string, string>
                        {
                            ["app"] = $"postgres-{dbSpec.Name}"
                        }
                    },
                    template = new
                    {
                        metadata = new
                        {
                            labels = new Dictionary<string, string>
                            {
                                ["app"] = $"postgres-{dbSpec.Name}"
                            }
                        },
                        spec = new
                        {
                            containers = new[]
                            {
                                new
                                {
                                    name = "postgresql",
                                    image = $"postgres:{dbSpec.Version}",
                                    ports = new[] { new { containerPort = 5432, name = "postgresql" } },
                                    env = new object[]
                                    {
                                        new
                                        {
                                            name = "POSTGRES_DB",
                                            value = dbSpec.Name
                                        },
                                        new
                                        {
                                            name = "POSTGRES_USER",
                                            valueFrom = new
                                            {
                                                secretKeyRef = new
                                                {
                                                    name = $"postgres-{dbSpec.Name}-secret",
                                                    key = "username"
                                                }
                                            }
                                        },
                                        new
                                        {
                                            name = "POSTGRES_PASSWORD",
                                            valueFrom = new
                                            {
                                                secretKeyRef = new
                                                {
                                                    name = $"postgres-{dbSpec.Name}-secret",
                                                    key = "password"
                                                }
                                            }
                                        }
                                    },
                                    volumeMounts = new[]
                                    {
                                        new
                                        {
                                            name = "data",
                                            mountPath = "/var/lib/postgresql/data"
                                        }
                                    },
                                    resources = new
                                    {
                                        requests = new
                                        {
                                            cpu = "250m",
                                            memory = "512Mi"
                                        },
                                        limits = new
                                        {
                                            cpu = "1000m",
                                            memory = "2Gi"
                                        }
                                    }
                                }
                            }
                        }
                    },
                    volumeClaimTemplates = new[]
                    {
                        new
                        {
                            metadata = new
                            {
                                name = "data"
                            },
                            spec = new
                            {
                                accessModes = new[] { "ReadWriteOnce" },
                                resources = new
                                {
                                    requests = new
                                    {
                                        storage = $"{dbSpec.StorageGB}Gi"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            return _yamlSerializer.Serialize(statefulSet);
        }

        private string GenerateService(TemplateGenerationRequest request, DatabaseSpec dbSpec)
        {
            var service = new
            {
                apiVersion = "v1",
                kind = "Service",
                metadata = new
                {
                    name = $"postgres-{dbSpec.Name}",
                    labels = new Dictionary<string, string>
                    {
                        ["app"] = $"postgres-{dbSpec.Name}"
                    }
                },
                spec = new
                {
                    type = "ClusterIP",
                    clusterIP = "None",
                    ports = new[]
                    {
                        new
                        {
                            port = 5432,
                            targetPort = 5432,
                            name = "postgresql"
                        }
                    },
                    selector = new Dictionary<string, string>
                    {
                        ["app"] = $"postgres-{dbSpec.Name}"
                    }
                }
            };

            return _yamlSerializer.Serialize(service);
        }

        private string GeneratePVC(TemplateGenerationRequest request, DatabaseSpec dbSpec)
        {
            // PVC is part of StatefulSet volumeClaimTemplates
            return "# PVC is managed by StatefulSet volumeClaimTemplates";
        }

        private string GenerateConfigMap(TemplateGenerationRequest request, DatabaseSpec dbSpec)
        {
            var configMap = new
            {
                apiVersion = "v1",
                kind = "ConfigMap",
                metadata = new
                {
                    name = $"postgres-{dbSpec.Name}-config"
                },
                data = new Dictionary<string, string>
                {
                    ["POSTGRES_DB"] = dbSpec.Name,
                    ["POSTGRES_HOST"] = $"postgres-{dbSpec.Name}",
                    ["POSTGRES_PORT"] = "5432"
                }
            };

            return _yamlSerializer.Serialize(configMap);
        }

        private string GenerateConnectionConfig(TemplateGenerationRequest request, DatabaseSpec dbSpec)
        {
            var config = new
            {
                database = new
                {
                    name = dbSpec.Name,
                    type = "PostgreSQL",
                    version = dbSpec.Version,
                    connectionString = $"Host=${{POSTGRES_HOST}};Database={dbSpec.Name};Username=${{POSTGRES_USER}};Password=${{POSTGRES_PASSWORD}}",
                    configMapRef = new
                    {
                        name = $"postgres-{dbSpec.Name}-config"
                    },
                    secretRef = new
                    {
                        name = $"postgres-{dbSpec.Name}-secret"
                    }
                }
            };

            return _yamlSerializer.Serialize(config);
        }

        private string GenerateExternalConfig(TemplateGenerationRequest request, DatabaseSpec dbSpec)
        {
            var config = new
            {
                database = new
                {
                    name = dbSpec.Name,
                    type = "PostgreSQL (External)",
                    connectionString = "${EXTERNAL_POSTGRES_CONNECTION_STRING}",
                    note = "Configure connection string in Key Vault or Kubernetes secret"
                }
            };

            return _yamlSerializer.Serialize(config);
        }
    }

    /// <summary>
    /// MySQL template generator
    /// </summary>
    public class MySQLTemplateGenerator : IDatabaseTemplateGenerator
    {
        private readonly ISerializer _yamlSerializer;

        public MySQLTemplateGenerator()
        {
            _yamlSerializer = new SerializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                .Build();
        }

        public Task<Dictionary<string, string>> GenerateAsync(
            TemplateGenerationRequest request, 
            DatabaseSpec dbSpec, 
            CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, string>();

            if (dbSpec.Location == DatabaseLocation.Kubernetes)
            {
                files[$"k8s/database-{dbSpec.Name}-statefulset.yaml"] = GenerateStatefulSet(request, dbSpec);
                files[$"k8s/database-{dbSpec.Name}-service.yaml"] = GenerateService(request, dbSpec);
            }

            return Task.FromResult(files);
        }

        private string GenerateStatefulSet(TemplateGenerationRequest request, DatabaseSpec dbSpec)
        {
            var statefulSet = new
            {
                apiVersion = "apps/v1",
                kind = "StatefulSet",
                metadata = new { name = $"mysql-{dbSpec.Name}" },
                spec = new
                {
                    serviceName = $"mysql-{dbSpec.Name}",
                    replicas = 1,
                    selector = new { matchLabels = new Dictionary<string, string> { ["app"] = $"mysql-{dbSpec.Name}" } },
                    template = new
                    {
                        metadata = new { labels = new Dictionary<string, string> { ["app"] = $"mysql-{dbSpec.Name}" } },
                        spec = new
                        {
                            containers = new[]
                            {
                                new
                                {
                                    name = "mysql",
                                    image = $"mysql:{dbSpec.Version}",
                                    ports = new[] { new { containerPort = 3306 } },
                                    env = new object[]
                                    {
                                        new { name = "MYSQL_DATABASE", value = dbSpec.Name },
                                        new { name = "MYSQL_ROOT_PASSWORD", valueFrom = new { secretKeyRef = new { name = $"mysql-{dbSpec.Name}-secret", key = "password" } } }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            return _yamlSerializer.Serialize(statefulSet);
        }

        private string GenerateService(TemplateGenerationRequest request, DatabaseSpec dbSpec)
        {
            var service = new
            {
                apiVersion = "v1",
                kind = "Service",
                metadata = new { name = $"mysql-{dbSpec.Name}" },
                spec = new
                {
                    type = "ClusterIP",
                    ports = new[] { new { port = 3306 } },
                    selector = new Dictionary<string, string> { ["app"] = $"mysql-{dbSpec.Name}" }
                }
            };

            return _yamlSerializer.Serialize(service);
        }
    }

    /// <summary>
    /// SQL Server template generator
    /// </summary>
    public class SQLServerTemplateGenerator : IDatabaseTemplateGenerator
    {
        public Task<Dictionary<string, string>> GenerateAsync(
            TemplateGenerationRequest request, 
            DatabaseSpec dbSpec, 
            CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, string>();
            files[$"k8s/database-{dbSpec.Name}-deployment.yaml"] = "# SQL Server K8s deployment - to be implemented";
            return Task.FromResult(files);
        }
    }

    /// <summary>
    /// Azure SQL template generator
    /// </summary>
    public class AzureSQLTemplateGenerator : IDatabaseTemplateGenerator
    {
        public Task<Dictionary<string, string>> GenerateAsync(
            TemplateGenerationRequest request, 
            DatabaseSpec dbSpec, 
            CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, string>();
            // Azure SQL provisioning handled by Bicep generator
            files[$"config/database-{dbSpec.Name}-azuresql.yaml"] = $"# Azure SQL: {dbSpec.Name} - provisioned via Bicep";
            return Task.FromResult(files);
        }
    }

    /// <summary>
    /// MongoDB template generator
    /// </summary>
    public class MongoDBTemplateGenerator : IDatabaseTemplateGenerator
    {
        private readonly ISerializer _yamlSerializer;

        public MongoDBTemplateGenerator()
        {
            _yamlSerializer = new SerializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                .Build();
        }

        public Task<Dictionary<string, string>> GenerateAsync(
            TemplateGenerationRequest request, 
            DatabaseSpec dbSpec, 
            CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, string>();

            if (dbSpec.Location == DatabaseLocation.Kubernetes)
            {
                files[$"k8s/database-{dbSpec.Name}-statefulset.yaml"] = GenerateStatefulSet(request, dbSpec);
                files[$"k8s/database-{dbSpec.Name}-service.yaml"] = GenerateService(request, dbSpec);
            }

            return Task.FromResult(files);
        }

        private string GenerateStatefulSet(TemplateGenerationRequest request, DatabaseSpec dbSpec)
        {
            var statefulSet = new
            {
                apiVersion = "apps/v1",
                kind = "StatefulSet",
                metadata = new { name = $"mongodb-{dbSpec.Name}" },
                spec = new
                {
                    serviceName = $"mongodb-{dbSpec.Name}",
                    replicas = dbSpec.HighAvailability ? 3 : 1,
                    selector = new { matchLabels = new Dictionary<string, string> { ["app"] = $"mongodb-{dbSpec.Name}" } },
                    template = new
                    {
                        metadata = new { labels = new Dictionary<string, string> { ["app"] = $"mongodb-{dbSpec.Name}" } },
                        spec = new
                        {
                            containers = new[]
                            {
                                new
                                {
                                    name = "mongodb",
                                    image = $"mongo:{dbSpec.Version}",
                                    ports = new[] { new { containerPort = 27017 } },
                                    volumeMounts = new[]
                                    {
                                        new { name = "data", mountPath = "/data/db" }
                                    }
                                }
                            }
                        }
                    },
                    volumeClaimTemplates = new[]
                    {
                        new
                        {
                            metadata = new { name = "data" },
                            spec = new
                            {
                                accessModes = new[] { "ReadWriteOnce" },
                                resources = new { requests = new { storage = $"{dbSpec.StorageGB}Gi" } }
                            }
                        }
                    }
                }
            };

            return _yamlSerializer.Serialize(statefulSet);
        }

        private string GenerateService(TemplateGenerationRequest request, DatabaseSpec dbSpec)
        {
            var service = new
            {
                apiVersion = "v1",
                kind = "Service",
                metadata = new { name = $"mongodb-{dbSpec.Name}" },
                spec = new
                {
                    type = "ClusterIP",
                    ports = new[] { new { port = 27017 } },
                    selector = new Dictionary<string, string> { ["app"] = $"mongodb-{dbSpec.Name}" }
                }
            };

            return _yamlSerializer.Serialize(service);
        }
    }

    /// <summary>
    /// CosmosDB template generator
    /// </summary>
    public class CosmosDBTemplateGenerator : IDatabaseTemplateGenerator
    {
        public Task<Dictionary<string, string>> GenerateAsync(
            TemplateGenerationRequest request, 
            DatabaseSpec dbSpec, 
            CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, string>();
            // CosmosDB provisioning handled by Bicep generator
            files[$"config/database-{dbSpec.Name}-cosmosdb.yaml"] = $"# CosmosDB: {dbSpec.Name} - provisioned via Bicep";
            return Task.FromResult(files);
        }
    }

    /// <summary>
    /// Redis template generator
    /// </summary>
    public class RedisTemplateGenerator : IDatabaseTemplateGenerator
    {
        private readonly ISerializer _yamlSerializer;

        public RedisTemplateGenerator()
        {
            _yamlSerializer = new SerializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                .Build();
        }

        public Task<Dictionary<string, string>> GenerateAsync(
            TemplateGenerationRequest request, 
            DatabaseSpec dbSpec, 
            CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, string>();

            if (dbSpec.Location == DatabaseLocation.Kubernetes)
            {
                files[$"k8s/database-{dbSpec.Name}-deployment.yaml"] = GenerateDeployment(request, dbSpec);
                files[$"k8s/database-{dbSpec.Name}-service.yaml"] = GenerateService(request, dbSpec);
            }

            return Task.FromResult(files);
        }

        private string GenerateDeployment(TemplateGenerationRequest request, DatabaseSpec dbSpec)
        {
            var deployment = new
            {
                apiVersion = "apps/v1",
                kind = "Deployment",
                metadata = new { name = $"redis-{dbSpec.Name}" },
                spec = new
                {
                    replicas = 1,
                    selector = new { matchLabels = new Dictionary<string, string> { ["app"] = $"redis-{dbSpec.Name}" } },
                    template = new
                    {
                        metadata = new { labels = new Dictionary<string, string> { ["app"] = $"redis-{dbSpec.Name}" } },
                        spec = new
                        {
                            containers = new[]
                            {
                                new
                                {
                                    name = "redis",
                                    image = "redis:7-alpine",
                                    ports = new[] { new { containerPort = 6379 } },
                                    resources = new
                                    {
                                        requests = new { cpu = "100m", memory = "128Mi" },
                                        limits = new { cpu = "500m", memory = "512Mi" }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            return _yamlSerializer.Serialize(deployment);
        }

        private string GenerateService(TemplateGenerationRequest request, DatabaseSpec dbSpec)
        {
            var service = new
            {
                apiVersion = "v1",
                kind = "Service",
                metadata = new { name = $"redis-{dbSpec.Name}" },
                spec = new
                {
                    type = "ClusterIP",
                    ports = new[] { new { port = 6379 } },
                    selector = new Dictionary<string, string> { ["app"] = $"redis-{dbSpec.Name}" }
                }
            };

            return _yamlSerializer.Serialize(service);
        }
    }

    /// <summary>
    /// DynamoDB template generator
    /// </summary>
    public class DynamoDBTemplateGenerator : IDatabaseTemplateGenerator
    {
        public Task<Dictionary<string, string>> GenerateAsync(
            TemplateGenerationRequest request, 
            DatabaseSpec dbSpec, 
            CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, string>();
            // DynamoDB provisioning handled by Terraform/CloudFormation
            files[$"config/database-{dbSpec.Name}-dynamodb.yaml"] = $"# DynamoDB: {dbSpec.Name} - provisioned via Terraform";
            return Task.FromResult(files);
        }
    }
}
