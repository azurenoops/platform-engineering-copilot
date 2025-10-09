using Platform.Engineering.Copilot.Core.Models.Onboarding;
using Platform.Engineering.Copilot.Core.Interfaces;

namespace Platform.Engineering.Copilot.Core.Models.Onboarding;

/// <summary>
/// Flankspeed onboarding workflow configuration
/// </summary>
public static class FlankspeedWorkflowConfig
{
    public static OnboardingWorkflowConfig GetConfiguration()
    {
        return new OnboardingWorkflowConfig
        {
            WorkflowId = "flankspeed",
            DisplayName = "Navy Flankspeed Onboarding",
            Description = "Onboarding workflow for Navy mission owners requesting Flankspeed platform access",
            TargetCommand = "NNWC",
            ApprovalAuthority = "NNWC Admin",
            WelcomeMessage = "🚀 **Welcome to Navy Flankspeed Mission Owner Onboarding!**\n\nI'll guide you through requesting access to the Flankspeed platform for your mission.",
            CompletionMessage = "✅ Your request has been submitted to NNWC for review. You'll receive an email notification when it's been reviewed.",
            
            Phases = new List<OnboardingPhase>
            {
                // Phase 1: Mission Details
                new OnboardingPhase
                {
                    PhaseId = "mission_details",
                    DisplayName = "Mission Details",
                    Order = 1,
                    Description = "Basic information about your mission and contact details",
                    InitialPrompt = "### 📋 Step 1: Mission Details\n\nPlease provide:\n- Mission/project name\n- Your name and rank\n- Your command\n- Classification level\n- Your email address",
                    RequiredFields = new List<string> { "missionName", "missionOwnerName", "rank", "command", "classificationLevel", "missionOwnerEmail" },
                    Fields = new List<OnboardingFieldDefinition>
                    {
                        new OnboardingFieldDefinition
                        {
                            FieldId = "missionName",
                            DisplayName = "Mission Name",
                            DataType = "string",
                            Description = "Name of your mission or project",
                            IsRequired = true,
                            DatabaseFieldName = "MissionName",
                            ExtractionPatterns = new List<string>
                            {
                                @"(?:mission|project)(?:\s+(?:is\s+)?called|\s+name(?:\s+is)?|\s+is)(?:\s+|:\s*|:\s+)([A-Za-z0-9][\sA-Za-z0-9-]*?)(?=\.|,|\s+for\s+|\s+at\s+|\s+from\s+|\s+we\s+need|\s+estimated|\s+network|\s+command|\s+classification|\s+your|\s+email|\s+subscription|\s+vnet|\s+data|\s+region|$)",
                                @"(?:for|to|setup|setting\s+up)\s+(?:the\s+)?(?:mission|project)\s+([A-Za-z0-9][\sA-Za-z0-9-]*?)(?=\.|,|\s+for\s+|\s+at\s+|\s+from\s+|\s+we\s+need|\s+command|\s+classification|$)",
                                @"mission(?:\s+name)?(?:\s*is|\s*:)\s*([A-Za-z0-9][\sA-Za-z0-9-]*?)(?=\.|,|\s+for\s+|\s+at\s+|\s+from\s+|\s+we\s+need|\s+command|\s+classification|\s+your|$)"
                            },
                            Examples = new List<string> { "Project Seawolf", "Alpha Mission", "TACNET Modernization", "AEGIS Integration" }
                        },
                        new OnboardingFieldDefinition
                        {
                            FieldId = "missionOwnerName",
                            DisplayName = "Your Name",
                            DataType = "string",
                            Description = "Full name of the mission owner",
                            IsRequired = true,
                            DatabaseFieldName = "MissionOwner",
                            ExtractionPatterns = new List<string>
                            {
                                @"(?:this\s+is\s+)?for\s+(?:the\s+)?(?:[A-Z][a-z]+\s+)?([A-Z][a-z]+\s+[A-Z][a-z]+)(?:\s+at\s+|\s+from\s+|\.|,|$)",
                                @"(?:your\s*name|i'?m|my\s*name\s*is|owner\s*is)(?:\s*is|\s*:)?\s*([A-Z][a-z]+(?:\s+[A-Z][a-z]+)+)(?=\.|,|\s+at\s+|\s+from\s+|command|email|$)",
                                @"(?:name|owner)(?:\s*is|\s*:)\s*([A-Z][a-z]+(?:\s+[A-Z][a-z]+)+)(?=\.|,|command|email|$)"
                            },
                            Examples = new List<string> { "John Smith", "Jane Doe", "Robert Johnson", "Sarah Chen" }
                        },
                        new OnboardingFieldDefinition
                        {
                            FieldId = "rank",
                            DisplayName = "Rank",
                            DataType = "string",
                            Description = "Military rank or civilian grade",
                            IsRequired = true,
                            DatabaseFieldName = "MissionOwnerRank",
                            TransformationType = "normalize_rank",
                            ExtractionPatterns = new List<string>
                            {
                                @"\b(Admiral|Vice Admiral|Rear Admiral|Captain|Commander|Lieutenant Commander|Lieutenant|Lieutenant Junior Grade|Ensign|Fleet Admiral|VADM|RADM|CAPT|CDR|LCDR|LT|LTJG|ENS)\b",
                                @"\b(General|Lieutenant General|Major General|Brigadier General|Colonel|Lieutenant Colonel|Major|First Lieutenant|Second Lieutenant|Gen|LtGen|MajGen|BGen|Col|LtCol|Maj|1stLt|2ndLt)\b",
                                @"\b(Master Chief Petty Officer|Senior Chief Petty Officer|Chief Petty Officer|Petty Officer First Class|Petty Officer Second Class|Petty Officer Third Class|Seaman|MCPO|SCPO|CPO|PO1|PO2|PO3)\b",
                                @"\b(Sergeant Major|Master Gunnery Sergeant|First Sergeant|Master Sergeant|Gunnery Sergeant|Staff Sergeant|Sergeant|Corporal|Lance Corporal|Private First Class|Private|SgtMaj|MGySgt|1stSgt|MSgt|GySgt|SSgt|Sgt|Cpl|LCpl|PFC|Pvt)\b",
                                @"\b(GS-1[0-5]|SES|Professor|Dr\.|Doctor)\b"
                            },
                            Examples = new List<string> { "Commander", "CDR", "Lieutenant", "GS-14", "Captain" }
                        },
                        new OnboardingFieldDefinition
                        {
                            FieldId = "command",
                            DisplayName = "Command",
                            DataType = "string",
                            Description = "Your military command or organization",
                            IsRequired = true,
                            DatabaseFieldName = "Command",
                            TransformationType = "uppercase",
                            ExtractionPatterns = new List<string>
                            {
                                @"\b(NAVWAR|NIWC|SPAWAR|NAVSEA|NAVAIR|NAVSUP|NSWC|NUWC|NNWC|CNRC|USFF|PACFLT|MARFORPAC|MARFORSOUTH)\b",
                                @"(?:at|from)\s+(?:the\s+)?([A-Z][A-Za-z]+)(?=\.|,|\s+the\s+mission|$)",
                                @"command(?:\s*is|\s*:)\s*([A-Za-z0-9\s-]+?)(?=\.|,|classification|email|$)"
                            },
                            Examples = new List<string> { "NNWC", "NAVWAR", "NIWC", "NAVSEA", "SPAWAR" }
                        },
                        new OnboardingFieldDefinition
                        {
                            FieldId = "classificationLevel",
                            DisplayName = "Classification Level",
                            DataType = "string",
                            Description = "Security classification level for your mission",
                            IsRequired = true,
                            DatabaseFieldName = "ClassificationLevel",
                            TransformationType = "uppercase",
                            ExtractionPatterns = new List<string>
                            {
                                @"classification(?:\s*level)?(?:\s*is|\s*:|\s*-)\s*(UNCLASS|SECRET|TOP SECRET|TS\/SCI|UNCLASSIFIED)(?:\s*,|\s*\n|email|$)",
                                @"\b(UNCLASS|SECRET|TOP SECRET|TS\/SCI|UNCLASSIFIED)\b"
                            },
                            Examples = new List<string> { "UNCLASS", "SECRET", "TOP SECRET" }
                        },
                        new OnboardingFieldDefinition
                        {
                            FieldId = "missionOwnerEmail",
                            DisplayName = "Email Address",
                            DataType = "string",
                            Description = "Your email address for notifications",
                            IsRequired = true,
                            DatabaseFieldName = "MissionOwnerEmail",
                            TransformationType = "lowercase",
                            ExtractionPatterns = new List<string>
                            {
                                @"(?:email|my\s*email)(?:\s*is|\s*:|\s*-)\s*([a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})(?:\s*,|\s*\n|$)",
                                @"\b([a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})\b"
                            },
                            Examples = new List<string> { "john.smith@navy.mil", "jane.doe@us.navy.mil" }
                        }
                    }
                },
                
                // Phase 2: Technical Requirements
                new OnboardingPhase
                {
                    PhaseId = "technical_requirements",
                    DisplayName = "Technical Requirements",
                    Order = 2,
                    Description = "Technical specifications for your environment",
                    InitialPrompt = "### ⚙️ Step 2: Technical Requirements\n\nPlease provide:\n- Subscription name\n- VNet CIDR (or 'default is fine')\n- Required Azure services\n- Estimated user count\n- Data volume\n- Region preference",
                    RequiredFields = new List<string> { "subscriptionName", "vnetCidr", "requiredServices", "estimatedUserCount", "dataVolumeTB" },
                    Fields = new List<OnboardingFieldDefinition>
                    {
                        new OnboardingFieldDefinition
                        {
                            FieldId = "subscriptionName",
                            DisplayName = "Subscription Name",
                            DatabaseFieldName = "RequestedSubscriptionName",
                            DataType = "string",
                            Description = "Name for your Azure subscription",
                            IsRequired = true,
                            ExtractionPatterns = new List<string>
                            {
                                @"subscription(?:\s*name)?(?:\s*is|\s*:|\s*-|\s*should\s*be\s*called|\s*called)\s*([A-Za-z0-9\s-]+?)(?:\s*,|\s*\n|\s+and\s+|vnet|required|estimated|data|region|$)"
                            },
                            Examples = new List<string> { "seawolf-prod", "alpha-mission", "tacnet-dev" }
                        },
                        new OnboardingFieldDefinition
                        {
                            FieldId = "vnetCidr",
                            DisplayName = "VNet CIDR",
                            DatabaseFieldName = "RequestedVNetCidr",
                            DataType = "string",
                            Description = "Virtual network CIDR block",
                            IsRequired = true,
                            DefaultValue = "10.100.0.0/16",
                            ExtractionPatterns = new List<string>
                            {
                                @"(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}/\d{1,2})",
                                @"(?:network|vnet|cidr)(?:\s+should\s+be|\s+is|\s+:)\s*(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}/\d{1,2})",
                                @"(?:vnet\s*)?cidr.*(?:default|fine|standard)"
                            },
                            Examples = new List<string> { "10.100.0.0/16", "10.150.0.0/16", "10.50.0.0/16", "default is fine" }
                        },
                        new OnboardingFieldDefinition
                        {
                            FieldId = "requiredServices",
                            DisplayName = "Required Services",
                            DatabaseFieldName = "RequiredServices",
                            DataType = "array",
                            Description = "List of Azure services needed",
                            IsRequired = true,
                            ExtractionPatterns = new List<string>
                            {
                                @"(?:we\s+need|require[ds]?)\s+([A-Za-z0-9\s,and]+?)(?=\.|,\s+estimated|\s+network|\s+about|\s+\d+\s+users|$)",
                                @"(?:required\s+)?services?(?:\s*is|\s*:|\s*-|:\s*)(?:i\s+need\s+)?([A-Za-z0-9\s,and]+?)(?=\.|estimated|data\s+volume|users|network|subscription|$)",
                                @"(?:with|using)\s+([A-Z][A-Za-z\s,and]+?)(?=\.|,\s+estimated|\s+network|\s+about|$)"
                            },
                            Examples = new List<string> { "AKS, Storage", "Azure Kubernetes Service with SQL Server and blob storage", "App Service and SQL", "VMs, Network, Storage" }
                        },
                        new OnboardingFieldDefinition
                        {
                            FieldId = "estimatedUserCount",
                            DisplayName = "Estimated Users",
                            DatabaseFieldName = "EstimatedUserCount",
                            DataType = "int",
                            Description = "Number of users who will access this environment",
                            IsRequired = true,
                            ExtractionPatterns = new List<string>
                            {
                                @"(?:estimated\s+)?(\d+)\s+users",
                                @"(?:users?|user\s+count)(?:\s*is|\s*:|\s*-|:\s*)?\s*(\d+)",
                                @"(?:about|around|approximately)\s+(\d+)\s+users"
                            },
                            Examples = new List<string> { "5", "10", "50", "200" }
                        },
                        new OnboardingFieldDefinition
                        {
                            FieldId = "dataVolumeTB",
                            DisplayName = "Data Volume (TB)",
                            DatabaseFieldName = "EstimatedDataVolumeTB",
                            DataType = "decimal",
                            Description = "Estimated data volume in terabytes",
                            IsRequired = true,
                            ExtractionPatterns = new List<string>
                            {
                                @"(\d+(?:\.\d+)?)\s*TB",
                                @"(\d+(?:\.\d+)?)\s*terabytes?",
                                @"(?:data\s+volume|volume)(?:\s*is|\s*:|\s*of)?\s*(?:about|around)?\s*(\d+(?:\.\d+)?)\s*(?:TB|terabytes?)",
                                @"(?:about|around|approximately)\s+(\d+(?:\.\d+)?)\s*TB"
                            },
                            Examples = new List<string> { "3TB", "5.5 TB", "10TB", "1TB" }
                        },
                        new OnboardingFieldDefinition
                        {
                            FieldId = "region",
                            DisplayName = "Region",
                            DatabaseFieldName = "Region",
                            DataType = "string",
                            Description = "Azure Government region",
                            IsRequired = false,
                            TransformationType = "lowercase",
                            DefaultValue = "usgovvirginia",
                            ExtractionPatterns = new List<string>
                            {
                                @"region(?:\s*is|\s*:|\s*-)\s*(usgovvirginia|usgovtexas|usgoviowa|usgovarizona)"
                            },
                            Examples = new List<string> { "usgovvirginia", "usgovtexas", "usgoviowa" }
                        }
                    }
                }
                // Additional phases (Compliance, Business Justification, Review) would be added here
            },

            FieldTransformations = new Dictionary<string, FieldTransformation>
            {
                ["rank"] = new FieldTransformation
                {
                    TransformationType = "normalize_rank",
                    Config = new Dictionary<string, object>
                    {
                        { "enableFuzzyMatching", true },
                        { "fuzzyMatchThreshold", 2 },
                        { "detectServiceBranch", true },
                        { "calculatePriority", true }
                    }
                },
                ["command"] = new FieldTransformation
                {
                    TransformationType = "uppercase"
                },
                ["classificationLevel"] = new FieldTransformation
                {
                    TransformationType = "uppercase"
                },
                ["missionOwnerEmail"] = new FieldTransformation
                {
                    TransformationType = "lowercase"
                },
                ["region"] = new FieldTransformation
                {
                    TransformationType = "lowercase"
                }
            }
        };
    }
}
