import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import adminApi, { TemplateStats } from '../services/adminApi';
import './InsightsPanel.css';

interface UsageMetrics {
  totalUsers: number;
  activeUsersToday: number;
  activeUsersWeek: number;
  activeUsersMonth: number;
  templateCreations: number;
  templateCreationsThisWeek: number;
  searchQueries: number;
  searchQueriesThisWeek: number;
  deployments: number;
  deploymentsThisWeek: number;
}

const InsightsPanel: React.FC = () => {
  const navigate = useNavigate();
  const [stats, setStats] = useState<TemplateStats | null>(null);
  const [selectedTimeRange, setSelectedTimeRange] = useState<'day' | 'week' | 'month'>('week');
  const [loading, setLoading] = useState(true);

  // Mock usage data - in production, this would come from an analytics API
  const [usageMetrics] = useState<UsageMetrics>({
    totalUsers: 156,
    activeUsersToday: 42,
    activeUsersWeek: 89,
    activeUsersMonth: 134,
    templateCreations: 12,
    templateCreationsThisWeek: 8,
    searchQueries: 324,
    searchQueriesThisWeek: 156,
    deployments: 45,
    deploymentsThisWeek: 23,
  });

  useEffect(() => {
    loadInsightsData();
  }, []);

  const loadInsightsData = async () => {
    try {
      setLoading(true);
      const statsData = await adminApi.getStats();
      setStats(statsData);
    } catch (err) {
      console.error('Failed to load insights data:', err);
    } finally {
      setLoading(false);
    }
  };

  const getActiveUsers = () => {
    switch (selectedTimeRange) {
      case 'day':
        return usageMetrics.activeUsersToday;
      case 'week':
        return usageMetrics.activeUsersWeek;
      case 'month':
        return usageMetrics.activeUsersMonth;
      default:
        return usageMetrics.activeUsersWeek;
    }
  };

  const getGrowthPercentage = (current: number, previous: number) => {
    if (previous === 0) return 0;
    return Math.round(((current - previous) / previous) * 100);
  };

  const getTemplateAdoptionRate = () => {
    if (stats) {
      const totalTemplates = stats.totalTemplates;
      const activeTemplates = stats.activeTemplates;
      return totalTemplates > 0 ? Math.round((activeTemplates / totalTemplates) * 100) : 0;
    }
    return 0;
  };

  if (loading) {
    return <div className="insights-loading">Loading insights...</div>;
  }

  return (
    <div className="insights-panel">
      <div className="insights-header">
        <h3>📊 Platform Insights & Analytics</h3>
        <div className="time-range-selector">
          <button
            className={selectedTimeRange === 'day' ? 'active' : ''}
            onClick={() => setSelectedTimeRange('day')}
          >
            Today
          </button>
          <button
            className={selectedTimeRange === 'week' ? 'active' : ''}
            onClick={() => setSelectedTimeRange('week')}
          >
            This Week
          </button>
          <button
            className={selectedTimeRange === 'month' ? 'active' : ''}
            onClick={() => setSelectedTimeRange('month')}
          >
            This Month
          </button>
        </div>
      </div>

      {/* User Activity Section */}
      <div className="insights-section">
        <div className="section-title">
          <h4>👥 User Activity</h4>
          <span className="section-subtitle">Active user trends and engagement</span>
        </div>
        <div className="insights-metrics-grid">
          <div className="insight-metric-card primary">
            <div className="metric-icon">📈</div>
            <div className="metric-content">
              <div className="metric-label">Active Users</div>
              <div className="metric-value">{getActiveUsers()}</div>
              <div className="metric-subtitle">
                {selectedTimeRange === 'day' && 'today'}
                {selectedTimeRange === 'week' && 'this week'}
                {selectedTimeRange === 'month' && 'this month'}
              </div>
            </div>
            <div className="metric-trend positive">
              <span className="trend-icon">↗</span>
              <span className="trend-value">+12%</span>
            </div>
          </div>

          <div className="insight-metric-card">
            <div className="metric-icon">🎯</div>
            <div className="metric-content">
              <div className="metric-label">Total Users</div>
              <div className="metric-value">{usageMetrics.totalUsers}</div>
              <div className="metric-subtitle">registered users</div>
            </div>
          </div>

          <div className="insight-metric-card">
            <div className="metric-icon">⚡</div>
            <div className="metric-content">
              <div className="metric-label">Engagement Rate</div>
              <div className="metric-value">
                {Math.round((getActiveUsers() / usageMetrics.totalUsers) * 100)}%
              </div>
              <div className="metric-subtitle">user engagement</div>
            </div>
          </div>
        </div>
      </div>

      {/* Feature Adoption Section */}
      <div className="insights-section">
        <div className="section-title">
          <h4>🚀 Feature Adoption</h4>
          <span className="section-subtitle">Key features driving platform usage</span>
        </div>
        <div className="insights-metrics-grid">
          <div className="insight-metric-card feature-card" onClick={() => navigate('/templates')}>
            <div className="feature-header">
              <div className="feature-icon">📝</div>
              <div className="feature-name">Templates</div>
            </div>
            <div className="feature-stats">
              <div className="feature-stat">
                <div className="stat-value">{stats?.totalTemplates || 0}</div>
                <div className="stat-label">Total Templates</div>
              </div>
              <div className="feature-stat">
                <div className="stat-value">{usageMetrics.templateCreationsThisWeek}</div>
                <div className="stat-label">Created This Week</div>
              </div>
            </div>
            <div className="feature-adoption">
              <div className="adoption-bar">
                <div
                  className="adoption-fill"
                  style={{ width: `${getTemplateAdoptionRate()}%` }}
                ></div>
              </div>
              <div className="adoption-label">{getTemplateAdoptionRate()}% Adoption Rate</div>
            </div>
            <div className="feature-trend positive">
              <span className="trend-icon">↗</span>
              <span>+{usageMetrics.templateCreationsThisWeek} this week</span>
            </div>
          </div>

          <div className="insight-metric-card feature-card" onClick={() => navigate('/templates')}>
            <div className="feature-header">
              <div className="feature-icon">🔍</div>
              <div className="feature-name">Search</div>
            </div>
            <div className="feature-stats">
              <div className="feature-stat">
                <div className="stat-value">{usageMetrics.searchQueries}</div>
                <div className="stat-label">Total Searches</div>
              </div>
              <div className="feature-stat">
                <div className="stat-value">{usageMetrics.searchQueriesThisWeek}</div>
                <div className="stat-label">This Week</div>
              </div>
            </div>
            <div className="feature-adoption">
              <div className="adoption-bar">
                <div
                  className="adoption-fill"
                  style={{ width: '78%' }}
                ></div>
              </div>
              <div className="adoption-label">78% Search Adoption</div>
            </div>
            <div className="feature-trend positive">
              <span className="trend-icon">↗</span>
              <span>+{usageMetrics.searchQueriesThisWeek} this week</span>
            </div>
          </div>

          <div className="insight-metric-card feature-card" onClick={() => navigate('/environments')}>
            <div className="feature-header">
              <div className="feature-icon">🚀</div>
              <div className="feature-name">Deployments</div>
            </div>
            <div className="feature-stats">
              <div className="feature-stat">
                <div className="stat-value">{usageMetrics.deployments}</div>
                <div className="stat-label">Total Deployments</div>
              </div>
              <div className="feature-stat">
                <div className="stat-value">{usageMetrics.deploymentsThisWeek}</div>
                <div className="stat-label">This Week</div>
              </div>
            </div>
            <div className="feature-adoption">
              <div className="adoption-bar">
                <div
                  className="adoption-fill"
                  style={{ width: '85%' }}
                ></div>
              </div>
              <div className="adoption-label">85% Deployment Success</div>
            </div>
            <div className="feature-trend positive">
              <span className="trend-icon">↗</span>
              <span>+{usageMetrics.deploymentsThisWeek} this week</span>
            </div>
          </div>
        </div>
      </div>

      {/* Template Breakdown Section */}
      <div className="insights-section">
        <div className="section-title">
          <h4>📚 Template Distribution</h4>
          <span className="section-subtitle">Templates by type and format</span>
        </div>
        <div className="insights-breakdown-grid">
          <div className="breakdown-card">
            <h5>By Type</h5>
            <div className="breakdown-list">
              {stats?.byType && stats.byType.map((item) => (
                <div key={item.type} className="breakdown-item">
                  <div className="breakdown-label">
                    <span className="breakdown-dot"></span>
                    {item.type}
                  </div>
                  <div className="breakdown-value-container">
                    <div
                      className="breakdown-bar"
                      style={{
                        width: `${(item.count / stats.totalTemplates) * 100}%`,
                      }}
                    ></div>
                    <span className="breakdown-count">{item.count}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="breakdown-card">
            <h5>By Format</h5>
            <div className="breakdown-list">
              {stats?.byFormat && stats.byFormat.map((item) => (
                <div key={item.format} className="breakdown-item">
                  <div className="breakdown-label">
                    <span className="breakdown-dot format"></span>
                    {item.format}
                  </div>
                  <div className="breakdown-value-container">
                    <div
                      className="breakdown-bar format"
                      style={{
                        width: `${(item.count / stats.totalTemplates) * 100}%`,
                      }}
                    ></div>
                    <span className="breakdown-count">{item.count}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="breakdown-card">
            <h5>By Cloud Provider</h5>
            <div className="breakdown-list">
              {stats?.byCloudProvider && stats.byCloudProvider.map((item) => (
                <div key={item.provider || 'unspecified'} className="breakdown-item">
                  <div className="breakdown-label">
                    <span className="breakdown-dot provider"></span>
                    {item.provider || 'Unspecified'}
                  </div>
                  <div className="breakdown-value-container">
                    <div
                      className="breakdown-bar provider"
                      style={{
                        width: `${(item.count / stats.totalTemplates) * 100}%`,
                      }}
                    ></div>
                    <span className="breakdown-count">{item.count}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* Quick Insights */}
      <div className="insights-section">
        <div className="section-title">
          <h4>💡 Quick Insights</h4>
          <span className="section-subtitle">Key takeaways and recommendations</span>
        </div>
        <div className="quick-insights-grid">
          <div className="quick-insight success">
            <div className="insight-icon">✅</div>
            <div className="insight-content">
              <div className="insight-title">High Template Adoption</div>
              <div className="insight-description">
                {getTemplateAdoptionRate()}% of templates are actively used - great platform engagement!
              </div>
            </div>
          </div>

          <div className="quick-insight info">
            <div className="insight-icon">📈</div>
            <div className="insight-content">
              <div className="insight-title">Growing User Base</div>
              <div className="insight-description">
                {getActiveUsers()} active users this {selectedTimeRange}, showing steady platform growth
              </div>
            </div>
          </div>

          <div className="quick-insight warning">
            <div className="insight-icon">🎯</div>
            <div className="insight-content">
              <div className="insight-title">Opportunity: Search Usage</div>
              <div className="insight-description">
                Search is heavily used ({usageMetrics.searchQueriesThisWeek} queries this week) - consider enhancing search features
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default InsightsPanel;
