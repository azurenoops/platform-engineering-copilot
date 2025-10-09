import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import adminApi, { Template } from '../services/adminApi';
import './TemplateList.css';

const TemplateList: React.FC = () => {
  const [templates, setTemplates] = useState<Template[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    loadTemplates();
  }, []);

  const loadTemplates = async (search?: string) => {
    try {
      setLoading(true);
      const data = await adminApi.listTemplates(search);
      // API returns { count, templates } format
      const templateList = Array.isArray(data) ? data : (data as any).templates || [];
      setTemplates(templateList);
      setError(null);
    } catch (err: any) {
      setError(err.message || 'Failed to load templates');
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    loadTemplates(searchTerm);
  };

  const handleDelete = async (templateId: string) => {
    try {
      await adminApi.deleteTemplate(templateId);
      setDeleteConfirm(null);
      loadTemplates(searchTerm);
    } catch (err: any) {
      setError(err.message || 'Failed to delete template');
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  if (loading) {
    return <div className="template-list-loading">⏳ Loading templates...</div>;
  }

  return (
    <div className="template-list">
      <div className="template-list-header">
        <h2>📋 Service Templates</h2>
        <button 
          className="btn-primary"
          onClick={() => navigate('/templates/create')}
        >
          ➕ Create New Template
        </button>
      </div>

      <form onSubmit={handleSearch} className="search-form">
        <input
          type="text"
          placeholder="Search templates by name, type, or format..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          className="search-input"
        />
        <button type="submit" className="btn-search">🔍 Search</button>
        <button 
          type="button" 
          onClick={() => {
            setSearchTerm('');
            loadTemplates();
          }}
          className="btn-clear"
        >
          Clear
        </button>
      </form>

      {error && (
        <div className="error-message">
          ⚠️ {error}
          <button onClick={() => loadTemplates(searchTerm)}>Retry</button>
        </div>
      )}

      {templates.length === 0 ? (
        <div className="no-templates">
          <p>No templates found.</p>
          <button onClick={() => navigate('/templates/create')} className="btn-primary">
            Create your first template
          </button>
        </div>
      ) : (
        <div className="templates-table-container">
          <table className="templates-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Type</th>
                <th>Format</th>
                <th>Version</th>
                <th>Created</th>
                <th>Status</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {templates.map((template) => {
                const isInfrastructure = template.templateType === 'infrastructure' || 
                                        ['Bicep', 'ARM', 'Terraform'].includes(template.format);
                
                return (
                <tr key={template.id} className={isInfrastructure ? 'infrastructure-template' : ''}>
                  <td>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      {isInfrastructure && <span title="Infrastructure Template">🏗️</span>}
                      <div>
                        <strong>{template.name}</strong>
                        {template.description && (
                          <div className="template-description">{template.description}</div>
                        )}
                      </div>
                    </div>
                  </td>
                  <td>
                    <span className={`badge badge-type ${isInfrastructure ? 'badge-infrastructure' : ''}`}>
                      {template.templateType}
                    </span>
                  </td>
                  <td>
                    <span className="badge badge-format">{template.format}</span>
                  </td>
                  <td>{template.version}</td>
                  <td>
                    <div className="date-info">
                      {formatDate(template.createdAt)}
                      <div className="created-by">by {template.createdBy}</div>
                    </div>
                  </td>
                  <td>
                    {template.isActive ? (
                      <span className="status status-active">✅ Active</span>
                    ) : (
                      <span className="status status-inactive">❌ Inactive</span>
                    )}
                    {template.isPublic && (
                      <span className="badge badge-public">🌐 Public</span>
                    )}
                  </td>
                  <td className="actions">
                    <button
                      onClick={() => navigate(`/templates/${template.id}`)}
                      className="btn-action btn-view"
                      title="View Details"
                    >
                      👁️ View
                    </button>
                    {deleteConfirm === template.id ? (
                      <div className="delete-confirm">
                        <button
                          onClick={() => handleDelete(template.id)}
                          className="btn-action btn-confirm-delete"
                        >
                          ✓ Confirm
                        </button>
                        <button
                          onClick={() => setDeleteConfirm(null)}
                          className="btn-action btn-cancel"
                        >
                          ✗ Cancel
                        </button>
                      </div>
                    ) : (
                      <button
                        onClick={() => setDeleteConfirm(template.id)}
                        className="btn-action btn-delete"
                        title="Delete Template"
                      >
                        🗑️ Delete
                      </button>
                    )}
                  </td>
                </tr>
              );
              })}
            </tbody>
          </table>
        </div>
      )}

      <div className="template-list-summary">
        Showing {templates.length} template(s)
      </div>
    </div>
  );
};

export default TemplateList;
