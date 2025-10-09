import React from 'react';
import { useNavigate } from 'react-router-dom';
import InsightsPanel from './InsightsPanel';
import './InsightsPage.css';

const InsightsPage: React.FC = () => {
  const navigate = useNavigate();

  return (
    <div className="insights-page">
      <div className="insights-page-header">
        <button className="back-button" onClick={() => navigate('/')}>
          ← Back to Dashboard
        </button>
        <h2>📊 Platform Insights & Analytics</h2>
        <p className="page-subtitle">
          Comprehensive analytics and usage data for your platform engineering workflow
        </p>
      </div>

      <InsightsPanel />
    </div>
  );
};

export default InsightsPage;
