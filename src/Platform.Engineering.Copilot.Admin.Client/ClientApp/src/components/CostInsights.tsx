import React, { useState, useEffect } from 'react';
import adminApi from '../services/adminApi';
import './CostInsights.css';

interface CostData {
  environmentId: string;
  environmentName: string;
  resourceGroup: string;
  currentCost: number;
  dailyCosts: DailyCost[];
  costByService: ServiceCost[];
  budget?: number;
  trend: 'up' | 'down' | 'stable';
  projectedMonthlyCost: number;
}

interface DailyCost {
  date: string;
  cost: number;
}

interface ServiceCost {
  serviceName: string;
  cost: number;
  percentage: number;
}

interface CostSummary {
  totalCost: number;
  environmentCount: number;
  avgCostPerEnvironment: number;
  topCostEnvironments: CostData[];
}

const CostInsights: React.FC = () => {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [environments, setEnvironments] = useState<any[]>([]);
  const [costData, setCostData] = useState<CostData[]>([]);
  const [summary, setSummary] = useState<CostSummary | null>(null);
  const [timePeriod, setTimePeriod] = useState<'7d' | '30d' | '90d'>('30d');
  const [selectedEnvironment, setSelectedEnvironment] = useState<string | null>(null);

  useEffect(() => {
    loadData();
  }, [timePeriod]);

  const loadData = async () => {
    try {
      setLoading(true);
      setError(null);

      console.log('CostInsights: Loading cost data...');

      // Load cost summary from real API
      const days = timePeriod === '7d' ? 7 : timePeriod === '30d' ? 30 : 90;
      console.log('CostInsights: Calling getCostSummary with days:', days);
      
      const costSummary = await adminApi.getCostSummary(days);
      console.log('CostInsights: Received cost summary:', costSummary);

      // Load environments
      const envListResponse = await adminApi.listEnvironments();
      const envs = envListResponse.environments || [];
      console.log('CostInsights: Loaded environments:', envs.length);
      setEnvironments(envs);

      // Transform API data to match our CostData interface
      const transformedCostData: CostData[] = envs.map((env: any) => {
        // For each environment, we'll show a portion of the total costs
        // In a real scenario, you'd call getEnvironmentCost for each env
        const envCost = costSummary.totalCost / Math.max(envs.length, 1);
        const trend = costSummary.trendPercentage > 5 ? 'up' : 
                     costSummary.trendPercentage < -5 ? 'down' : 'stable';

        return {
          environmentId: env.id,
          environmentName: env.name,
          resourceGroup: env.resourceGroup || 'N/A',
          currentCost: envCost,
          dailyCosts: costSummary.dailyCosts.map(dc => ({
            date: dc.date,
            cost: dc.cost / Math.max(envs.length, 1), // Distribute costs across environments
          })),
          costByService: costSummary.topServices.slice(0, 5).map(svc => ({
            serviceName: svc.serviceName,
            cost: svc.cost,
            percentage: (svc.cost / costSummary.totalCost) * 100,
          })),
          budget: undefined, // Will be populated from budget data if available
          trend,
          projectedMonthlyCost: envCost * 30,
        };
      });

      setCostData(transformedCostData);

      // Set summary from API data
      const totalCost = costSummary.totalCost;
      const avgCost = totalCost / Math.max(envs.length, 1);
      const topCosts = [...transformedCostData]
        .sort((a, b) => b.currentCost - a.currentCost)
        .slice(0, 5);

      setSummary({
        totalCost,
        environmentCount: envs.length,
        avgCostPerEnvironment: avgCost,
        topCostEnvironments: topCosts,
      });
    } catch (err) {
      console.error('CostInsights: Failed to load cost data:', err);
      console.error('CostInsights: Error details:', err);
      if (err instanceof Error) {
        console.error('CostInsights: Error message:', err.message);
        console.error('CostInsights: Error stack:', err.stack);
      }
      setError('Failed to load cost data. Please try again.');
      // Fallback to mock data on error
      console.log('CostInsights: Falling back to mock data');
      loadMockData();
    } finally {
      setLoading(false);
    }
  };

  // Fallback method for when API is unavailable
  const loadMockData = async () => {
    const envListResponse = await adminApi.listEnvironments();
    const envs = envListResponse.environments || [];
    setEnvironments(envs);

    const mockCostData = generateMockCostData(envs);
    setCostData(mockCostData);

    const totalCost = mockCostData.reduce((sum, env) => sum + env.currentCost, 0);
    const avgCost = totalCost / Math.max(mockCostData.length, 1);
    const topCosts = [...mockCostData]
      .sort((a, b) => b.currentCost - a.currentCost)
      .slice(0, 5);

    setSummary({
      totalCost,
      environmentCount: mockCostData.length,
      avgCostPerEnvironment: avgCost,
      topCostEnvironments: topCosts,
    });
  };

  const generateMockCostData = (envs: any[]): CostData[] => {
    const days = timePeriod === '7d' ? 7 : timePeriod === '30d' ? 30 : 90;
    
    return envs.map((env) => {
      const baseCost = Math.random() * 500 + 100; // $100-$600/day
      const dailyCosts: DailyCost[] = [];
      
      for (let i = days - 1; i >= 0; i--) {
        const date = new Date();
        date.setDate(date.getDate() - i);
        const variation = (Math.random() - 0.5) * 50;
        dailyCosts.push({
          date: date.toISOString().split('T')[0],
          cost: Math.max(0, baseCost + variation),
        });
      }

      const currentCost = dailyCosts[dailyCosts.length - 1]?.cost || 0;
      const previousCost = dailyCosts[dailyCosts.length - 2]?.cost || 0;
      const trend = currentCost > previousCost * 1.05 ? 'up' : 
                    currentCost < previousCost * 0.95 ? 'down' : 'stable';

      const services = ['Compute', 'Storage', 'Networking', 'Database', 'Monitoring'];
      const costByService = services.map((service) => {
        const serviceCost = (Math.random() * currentCost) / services.length;
        return {
          serviceName: service,
          cost: serviceCost,
          percentage: (serviceCost / currentCost) * 100,
        };
      });

      return {
        environmentId: env.id,
        environmentName: env.name,
        resourceGroup: env.resourceGroup || 'N/A',
        currentCost,
        dailyCosts,
        costByService,
        budget: Math.random() > 0.5 ? baseCost * 1.2 : undefined,
        trend,
        projectedMonthlyCost: currentCost * 30,
      };
    });
  };

  const formatCurrency = (amount: number): string => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(amount);
  };

  const getTrendIcon = (trend: string): string => {
    switch (trend) {
      case 'up': return '📈';
      case 'down': return '📉';
      default: return '➡️';
    }
  };

  const getTrendColor = (trend: string): string => {
    switch (trend) {
      case 'up': return '#dc3545';
      case 'down': return '#28a745';
      default: return '#6c757d';
    }
  };

  if (loading) {
    return <div className="cost-loading">⏳ Loading cost data...</div>;
  }

  if (error) {
    return <div className="cost-error">{error}</div>;
  }

  const selectedCostData = selectedEnvironment
    ? costData.find((cd) => cd.environmentId === selectedEnvironment)
    : null;

  return (
    <div className="cost-insights">
      <div className="cost-header">
        <div>
          <h2>💰 Cost Insights</h2>
          <p className="cost-subtitle">Track and optimize your Azure spending</p>
        </div>
        
        <div className="time-period-selector">
          <button
            className={`period-btn ${timePeriod === '7d' ? 'active' : ''}`}
            onClick={() => setTimePeriod('7d')}
          >
            Last 7 Days
          </button>
          <button
            className={`period-btn ${timePeriod === '30d' ? 'active' : ''}`}
            onClick={() => setTimePeriod('30d')}
          >
            Last 30 Days
          </button>
          <button
            className={`period-btn ${timePeriod === '90d' ? 'active' : ''}`}
            onClick={() => setTimePeriod('90d')}
          >
            Last 90 Days
          </button>
        </div>
      </div>

      {summary && (
        <div className="cost-summary">
          <div className="summary-card">
            <div className="summary-label">Total Cost</div>
            <div className="summary-value">{formatCurrency(summary.totalCost)}</div>
            <div className="summary-subtitle">Across all environments</div>
          </div>
          
          <div className="summary-card">
            <div className="summary-label">Environments</div>
            <div className="summary-value">{summary.environmentCount}</div>
            <div className="summary-subtitle">Active environments</div>
          </div>
          
          <div className="summary-card">
            <div className="summary-label">Avg per Environment</div>
            <div className="summary-value">{formatCurrency(summary.avgCostPerEnvironment)}</div>
            <div className="summary-subtitle">Daily average</div>
          </div>
        </div>
      )}

      <div className="cost-content">
        <div className="cost-environments">
          <div className="section-header">
            <h3>Environments</h3>
            <span className="section-count">{costData.length} total</span>
          </div>
          
          <div className="environment-list">
            {costData.map((env) => (
              <div
                key={env.environmentId}
                className={`environment-cost-item ${
                  selectedEnvironment === env.environmentId ? 'selected' : ''
                }`}
                onClick={() => setSelectedEnvironment(env.environmentId)}
              >
                <div className="env-cost-header">
                  <div className="env-name">{env.environmentName}</div>
                  <div className="env-trend" style={{ color: getTrendColor(env.trend) }}>
                    {getTrendIcon(env.trend)}
                  </div>
                </div>
                
                <div className="env-cost-amount">{formatCurrency(env.currentCost)}</div>
                
                <div className="env-cost-footer">
                  <span className="env-rg">📦 {env.resourceGroup}</span>
                  {env.budget && (
                    <span className="env-budget">
                      Budget: {formatCurrency(env.budget)}
                    </span>
                  )}
                </div>
                
                {env.budget && env.currentCost > env.budget * 0.8 && (
                  <div className="budget-warning">
                    ⚠️ {((env.currentCost / env.budget) * 100).toFixed(0)}% of budget used
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>

        <div className="cost-details">
          {selectedCostData ? (
            <>
              <div className="section-header">
                <h3>{selectedCostData.environmentName}</h3>
                <button
                  className="close-details-btn"
                  onClick={() => setSelectedEnvironment(null)}
                >
                  ✕
                </button>
              </div>

              <div className="detail-cards">
                <div className="detail-card">
                  <div className="detail-label">Current Daily Cost</div>
                  <div className="detail-value">
                    {formatCurrency(selectedCostData.currentCost)}
                  </div>
                </div>
                
                <div className="detail-card">
                  <div className="detail-label">Projected Monthly</div>
                  <div className="detail-value">
                    {formatCurrency(selectedCostData.projectedMonthlyCost)}
                  </div>
                </div>
                
                <div className="detail-card">
                  <div className="detail-label">Trend</div>
                  <div
                    className="detail-value"
                    style={{ color: getTrendColor(selectedCostData.trend) }}
                  >
                    {getTrendIcon(selectedCostData.trend)} {selectedCostData.trend}
                  </div>
                </div>
              </div>

              <div className="cost-chart">
                <h4>Cost Trend</h4>
                <div className="chart-container">
                  {selectedCostData.dailyCosts.map((daily, index) => {
                    const maxCost = Math.max(...selectedCostData.dailyCosts.map((d) => d.cost));
                    const height = (daily.cost / maxCost) * 100;
                    
                    return (
                      <div key={index} className="chart-bar-wrapper">
                        <div
                          className="chart-bar"
                          style={{ height: `${height}%` }}
                          title={`${daily.date}: ${formatCurrency(daily.cost)}`}
                        />
                        {index % Math.floor(selectedCostData.dailyCosts.length / 7) === 0 && (
                          <div className="chart-label">
                            {new Date(daily.date).toLocaleDateString('en-US', {
                              month: 'short',
                              day: 'numeric',
                            })}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              </div>

              <div className="service-breakdown">
                <h4>Cost by Service</h4>
                <div className="service-list">
                  {selectedCostData.costByService
                    .sort((a, b) => b.cost - a.cost)
                    .map((service, index) => (
                      <div key={index} className="service-item">
                        <div className="service-info">
                          <div className="service-name">{service.serviceName}</div>
                          <div className="service-cost">
                            {formatCurrency(service.cost)}
                          </div>
                        </div>
                        <div className="service-bar-container">
                          <div
                            className="service-bar"
                            style={{ width: `${service.percentage}%` }}
                          />
                        </div>
                        <div className="service-percentage">
                          {service.percentage.toFixed(1)}%
                        </div>
                      </div>
                    ))}
                </div>
              </div>

              <div className="optimization-tips">
                <h4>💡 Optimization Tips</h4>
                <ul>
                  <li>
                    <strong>Right-size VMs:</strong> Review VM utilization and consider scaling
                    down oversized instances
                  </li>
                  <li>
                    <strong>Use Reserved Instances:</strong> Save up to 72% by committing to 1 or 3
                    year terms for predictable workloads
                  </li>
                  <li>
                    <strong>Enable Auto-shutdown:</strong> Schedule VMs to shut down during
                    off-hours to reduce costs
                  </li>
                  <li>
                    <strong>Delete Unused Resources:</strong> Identify and remove orphaned disks,
                    NICs, and public IPs
                  </li>
                  <li>
                    <strong>Optimize Storage:</strong> Use appropriate storage tiers and enable
                    lifecycle management
                  </li>
                </ul>
              </div>
            </>
          ) : (
            <div className="no-selection">
              <p>📊 Select an environment to view detailed cost breakdown</p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default CostInsights;
