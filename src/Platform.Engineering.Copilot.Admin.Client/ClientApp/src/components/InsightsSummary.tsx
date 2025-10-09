import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import adminApi, { TemplateStats } from '../services/adminApi';
import './InsightsSummary.css';

interface UsageMetrics {
  activeUsersWeek: number;
  templateCreationsThisWeek: number;
  deploymentsThisWeek: number;
}

const InsightsSummary: React.FC = () => {
  const navigate = useNavigate();
  const [stats, setStats] = useState<TemplateStats | null>(null);
  
  // Mock usage data - in production, this would come from an analytics API
  const [usageMetrics] = useState<UsageMetrics>({
    activeUsersWeek: 89,
    templateCreationsThisWeek: 8,
    deploymentsThisWeek: 23,
  });

  useEffect(() => {
    loadStats();
  }, []);

  const loadStats = async () => {
    try {
      const statsData = await adminApi.getStats();
      setStats(statsData);
    } catch (err) {
      console.error('Failed to load insights data:', err);
    }
  };

  const getTemplateAdoptionRate = () => {
    if (stats) {
      const totalTemplates = stats.totalTemplates;
      const activeTemplates = stats.activeTemplates;
      return totalTemplates > 0 ? Math.round((activeTemplates / totalTemplates) * 100) : 0;
    }
    return 0;
  };

  return (
    <div className="insights-summary">
      <div className="insights-summary-header">
        <h3>📊 Platform Insights & Analytics</h3>
        <button 
          className="view-details-btn"
          onClick={() => navigate('/insights')}
        >
          View Details →
        </button>
      </div>

      <div className="insights-summary-grid">
        <div className="summary-metric primary">
          <div className="metric-icon">👥</div>
          <div className="metric-info">
            <div className="metric-value">{usageMetrics.activeUsersWeek}</div>
            <div className="metric-label">Active Users</div>
            <div className="metric-detail">this week</div>
          </div>
          <div className="metric-trend">
            <span className="trend-indicator up">↗ +12%</span>
          </div>
        </div>

        <div className="summary-metric">
          <div className="metric-icon">📝</div>
          <div className="metric-info">
            <div className="metric-value">{stats?.totalTemplates || 0}</div>
            <div className="metric-label">Templates</div>
            <div className="metric-detail">{getTemplateAdoptionRate()}% adoption</div>
          </div>
          <div className="metric-trend">
            <span className="trend-indicator up">+{usageMetrics.templateCreationsThisWeek} this week</span>
          </div>
        </div>

        <div className="summary-metric">
          <div className="metric-icon">🚀</div>
          <div className="metric-info">
            <div className="metric-value">{usageMetrics.deploymentsThisWeek}</div>
            <div className="metric-label">Deployments</div>
            <div className="metric-detail">this week</div>
          </div>
          <div className="metric-trend">
            <span className="trend-indicator up">85% success</span>
          </div>
        </div>
      </div>
    </div>
  );
};

export default InsightsSummary;
