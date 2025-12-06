export interface FeatureFlags {
  serviceCatalog: boolean;
  environments: boolean;
  provision: boolean;
  costInsights: boolean;
  platformInsights: boolean;
  globalSearch: boolean;
}

export const DEFAULT_FEATURE_FLAGS: FeatureFlags = {
  serviceCatalog: true,
  environments: true,
  provision: true,
  costInsights: true,
  platformInsights: true,
  globalSearch: true,
};

export const FEATURE_FLAG_LABELS: Record<keyof FeatureFlags, string> = {
  serviceCatalog: '📚 Service Catalog',
  environments: '🌍 Environments',
  provision: '🔧 Provision Infrastructure',
  costInsights: '💰 Cost Insights',
  platformInsights: '📈 Platform Insights',
  globalSearch: '🔍 Global Search',
};

export const FEATURE_FLAG_DESCRIPTIONS: Record<keyof FeatureFlags, string> = {
  serviceCatalog: 'Browse and manage service templates',
  environments: 'View and manage deployed environments',
  provision: 'Provision new infrastructure resources',
  costInsights: 'View cost analysis and optimization recommendations',
  platformInsights: 'View platform analytics and insights',
  globalSearch: 'Enable global search functionality (Ctrl/Cmd+K)',
};

const STORAGE_KEY = 'platformFeatureFlags';

export function loadFeatureFlags(): FeatureFlags {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored) {
      const parsed = JSON.parse(stored);
      // Merge with defaults to ensure all flags exist
      return { ...DEFAULT_FEATURE_FLAGS, ...parsed };
    }
  } catch (error) {
    console.error('Failed to load feature flags:', error);
  }
  return DEFAULT_FEATURE_FLAGS;
}

export function saveFeatureFlags(flags: FeatureFlags): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(flags));
    console.log('💾 Feature flags saved:', flags);
  } catch (error) {
    console.error('Failed to save feature flags:', error);
  }
}

export function resetFeatureFlags(): FeatureFlags {
  const flags = DEFAULT_FEATURE_FLAGS;
  saveFeatureFlags(flags);
  return flags;
}

export function enableAllFeatures(): FeatureFlags {
  const flags: FeatureFlags = {
    serviceCatalog: true,
    environments: true,
    provision: true,
    costInsights: true,
    platformInsights: true,
    globalSearch: true,
  };
  saveFeatureFlags(flags);
  return flags;
}

export function disableAllFeatures(): FeatureFlags {
  const flags: FeatureFlags = {
    serviceCatalog: false,
    environments: false,
    provision: false,
    costInsights: false,
    platformInsights: false,
    globalSearch: false,
  };
  saveFeatureFlags(flags);
  return flags;
}
