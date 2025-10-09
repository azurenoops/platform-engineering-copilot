import React from 'react';
import { ValidationResult, ValidationError, ValidationWarning, ValidationRecommendation } from '../services/adminApi';
import './ValidationResults.css';

interface ValidationResultsProps {
  validationResult: ValidationResult;
  onDismiss: () => void;
}

const ValidationResults: React.FC<ValidationResultsProps> = ({ validationResult, onDismiss }) => {
  const { isValid, errors, warnings, recommendations, platform, validationTimeMs } = validationResult;

  return (
    <div className="validation-results">
      <div className="validation-header">
        <div className="validation-title">
          {isValid ? (
            <>
              <span className="validation-icon success">✅</span>
              <h3>Validation Passed</h3>
            </>
          ) : (
            <>
              <span className="validation-icon error">❌</span>
              <h3>Validation Failed</h3>
            </>
          )}
        </div>
        <button className="btn-close" onClick={onDismiss}>✕</button>
      </div>

      <div className="validation-meta">
        {platform && <span className="platform-badge">{platform}</span>}
        <span className="validation-time">{validationTimeMs}ms</span>
      </div>

      {errors.length > 0 && (
        <div className="validation-section errors">
          <h4>
            <span className="section-icon">🚫</span>
            Errors ({errors.length})
          </h4>
          <p className="section-description">These issues must be fixed before deployment</p>
          {errors.map((error, index) => (
            <ValidationErrorCard key={index} error={error} />
          ))}
        </div>
      )}

      {warnings.length > 0 && (
        <div className="validation-section warnings">
          <h4>
            <span className="section-icon">⚠️</span>
            Warnings ({warnings.length})
          </h4>
          <p className="section-description">Consider addressing these concerns</p>
          {warnings.map((warning, index) => (
            <ValidationWarningCard key={index} warning={warning} />
          ))}
        </div>
      )}

      {recommendations.length > 0 && (
        <div className="validation-section recommendations">
          <h4>
            <span className="section-icon">💡</span>
            Recommendations ({recommendations.length})
          </h4>
          <p className="section-description">Suggestions to optimize your configuration</p>
          {recommendations.map((recommendation, index) => (
            <ValidationRecommendationCard key={index} recommendation={recommendation} />
          ))}
        </div>
      )}

      {isValid && errors.length === 0 && warnings.length === 0 && recommendations.length === 0 && (
        <div className="validation-empty">
          <span className="empty-icon">✨</span>
          <p>Your configuration looks great! No issues found.</p>
        </div>
      )}
    </div>
  );
};

const ValidationErrorCard: React.FC<{ error: ValidationError }> = ({ error }) => {
  return (
    <div className="validation-card error-card">
      <div className="card-header">
        <code className="field-name">{error.field}</code>
        <span className="error-code">{error.code}</span>
      </div>
      <p className="card-message">{error.message}</p>
      {error.currentValue && (
        <div className="card-detail">
          <span className="detail-label">Current:</span>
          <code>{error.currentValue}</code>
        </div>
      )}
      {error.expectedValue && (
        <div className="card-detail">
          <span className="detail-label">Expected:</span>
          <code>{error.expectedValue}</code>
        </div>
      )}
      {error.documentationUrl && (
        <a 
          href={error.documentationUrl} 
          target="_blank" 
          rel="noopener noreferrer" 
          className="doc-link"
        >
          📖 View Documentation →
        </a>
      )}
    </div>
  );
};

const ValidationWarningCard: React.FC<{ warning: ValidationWarning }> = ({ warning }) => {
  const getSeverityClass = (severity: string) => {
    return `severity-${severity.toLowerCase()}`;
  };

  return (
    <div className={`validation-card warning-card ${getSeverityClass(warning.severity)}`}>
      <div className="card-header">
        <code className="field-name">{warning.field}</code>
        <span className="warning-severity">{warning.severity}</span>
      </div>
      <p className="card-message">{warning.message}</p>
      {warning.impact && (
        <div className="card-impact">
          <span className="impact-label">Impact:</span>
          <p>{warning.impact}</p>
        </div>
      )}
    </div>
  );
};

const ValidationRecommendationCard: React.FC<{ recommendation: ValidationRecommendation }> = ({ recommendation }) => {
  return (
    <div className="validation-card recommendation-card">
      <div className="card-header">
        <code className="field-name">{recommendation.field}</code>
        <span className="recommendation-badge">Optimization</span>
      </div>
      <p className="card-message">{recommendation.message}</p>
      <div className="recommendation-details">
        {recommendation.currentValue && (
          <div className="detail-row">
            <span className="detail-label">Current:</span>
            <code>{recommendation.currentValue}</code>
          </div>
        )}
        {recommendation.recommendedValue && (
          <div className="detail-row">
            <span className="detail-label">Recommended:</span>
            <code className="recommended-value">{recommendation.recommendedValue}</code>
          </div>
        )}
        {recommendation.reason && (
          <div className="detail-row">
            <span className="detail-label">Why:</span>
            <p>{recommendation.reason}</p>
          </div>
        )}
        {recommendation.benefit && (
          <div className="benefit-highlight">
            <span className="benefit-icon">✨</span>
            <p>{recommendation.benefit}</p>
          </div>
        )}
      </div>
    </div>
  );
};

export default ValidationResults;
