import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import adminApi, { Template, TemplateFile } from '../services/adminApi';
import './TemplateDetails.css';

const TemplateDetails: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [template, setTemplate] = useState<Template | null>(null);
  const [files, setFiles] = useState<TemplateFile[]>([]);
  const [selectedFile, setSelectedFile] = useState<TemplateFile | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingFiles, setLoadingFiles] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showContent, setShowContent] = useState(false);
  const [showFiles, setShowFiles] = useState(false);
  const [deleteConfirm, setDeleteConfirm] = useState(false);
  const [isEditing, setIsEditing] = useState(false);
  const [editedContent, setEditedContent] = useState<string>('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (id) {
      loadTemplate(id);
      loadFiles(id);
    }
  }, [id]);

  const loadTemplate = async (templateId: string) => {
    try {
      setLoading(true);
      const data = await adminApi.getTemplate(templateId);
      setTemplate(data);
      setError(null);
    } catch (err: any) {
      setError(err.message || 'Failed to load template');
    } finally {
      setLoading(false);
    }
  };

  const loadFiles = async (templateId: string) => {
    try {
      setLoadingFiles(true);
      const data = await adminApi.getTemplateFiles(templateId);
      setFiles(data.files);
      // Auto-select the first file (usually the entry point)
      if (data.files.length > 0) {
        const entryPoint = data.files.find(f => f.isEntryPoint) || data.files[0];
        setSelectedFile(entryPoint);
      }
    } catch (err: any) {
      console.error('Failed to load files:', err);
      console.error('Error details:', err.response?.data);
      console.error('Error status:', err.response?.status);
      // Don't set error state for files - just log it
    } finally {
      setLoadingFiles(false);
    }
  };

  const handleDelete = async () => {
    if (!id) return;
    
    try {
      await adminApi.deleteTemplate(id);
      navigate('/templates');
    } catch (err: any) {
      setError(err.message || 'Failed to delete template');
      setDeleteConfirm(false);
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
    alert('Copied to clipboard!');
  };

  const getFileIcon = (fileName: string): string => {
    const ext = fileName.split('.').pop()?.toLowerCase();
    const icons: Record<string, string> = {
      'tf': '🟦',
      'bicep': '💪',
      'yaml': '📋',
      'yml': '📋',
      'json': '{ }',
      'sh': '🐚',
      'ps1': '💠',
      'md': '📝',
      'cs': '🔷',
      'py': '🐍',
      'js': '📜',
      'ts': '📘'
    };
    return icons[ext || ''] || '📄';
  };

  const getLanguageClass = (fileName: string): string => {
    const ext = fileName.split('.').pop()?.toLowerCase();
    const languages: Record<string, string> = {
      'tf': 'language-hcl',
      'bicep': 'language-bicep',
      'yaml': 'language-yaml',
      'yml': 'language-yaml',
      'json': 'language-json',
      'sh': 'language-bash',
      'ps1': 'language-powershell',
      'md': 'language-markdown',
      'cs': 'language-csharp',
      'py': 'language-python',
      'js': 'language-javascript',
      'ts': 'language-typescript'
    };
    return languages[ext || ''] || 'language-plaintext';
  };

  const handleEditFile = () => {
    if (selectedFile) {
      setEditedContent(selectedFile.content);
      setIsEditing(true);
    }
  };

  const handleCancelEdit = () => {
    setIsEditing(false);
    setEditedContent('');
  };

  const handleSaveFile = async () => {
    if (!id || !selectedFile) return;

    try {
      setSaving(true);
      await adminApi.updateTemplateFile(id, selectedFile.fileName, editedContent);
      
      // Update the local file content
      setFiles(files.map(f => 
        f.fileName === selectedFile.fileName 
          ? { ...f, content: editedContent, size: editedContent.length }
          : f
      ));
      
      setSelectedFile({ ...selectedFile, content: editedContent, size: editedContent.length });
      setIsEditing(false);
      setError(null);
      
      // Show success message
      alert('File saved successfully!');
    } catch (err: any) {
      setError(err.message || 'Failed to save file');
      alert('Error saving file: ' + (err.message || 'Unknown error'));
    } finally {
      setSaving(false);
    }
  };

  const downloadFile = (file: TemplateFile) => {
    const blob = new Blob([file.content], { type: 'text/plain' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = file.fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  };

  const downloadAllFiles = () => {
    files.forEach(file => {
      downloadFile(file);
    });
  };

  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  if (loading) {
    return <div className="template-details-loading">⏳ Loading template details...</div>;
  }

  if (error) {
    return (
      <div className="template-details-error">
        <h2>⚠️ Error</h2>
        <p>{error}</p>
        <button onClick={() => navigate('/templates')} className="btn-secondary">
          ← Back to Templates
        </button>
      </div>
    );
  }

  if (!template) {
    return (
      <div className="template-details-error">
        <h2>❌ Template Not Found</h2>
        <p>The template you're looking for doesn't exist.</p>
        <button onClick={() => navigate('/templates')} className="btn-secondary">
          ← Back to Templates
        </button>
      </div>
    );
  }

  return (
    <div className="template-details">
      <div className="template-details-header">
        <div className="header-left">
          <button onClick={() => navigate('/templates')} className="btn-back">
            ← Back to Service Templates
          </button>
          <h2>🔍 {template.name}</h2>
        </div>
        <div className="header-actions">
          <button 
            onClick={() => navigate(`/templates/${id}/edit`)} 
            className="btn-edit"
            title="Edit Service Template"
          >
            ✏️ Edit
          </button>
          {deleteConfirm ? (
            <div className="delete-confirm-inline">
              <button onClick={handleDelete} className="btn-delete-confirm">
                ✓ Confirm Delete
              </button>
              <button onClick={() => setDeleteConfirm(false)} className="btn-cancel-inline">
                ✗ Cancel
              </button>
            </div>
          ) : (
            <button 
              onClick={() => setDeleteConfirm(true)} 
              className="btn-delete"
              title="Delete Template"
            >
              🗑️ Delete
            </button>
          )}
        </div>
      </div>

      <div className="template-details-content">
        {/* Overview Section */}
        <section className="details-section">
          <h3>📋 Overview</h3>
          <div className="details-grid">
            <div className="detail-item">
              <label>Template ID</label>
              <div className="detail-value">
                <code>{template.id}</code>
                <button 
                  onClick={() => copyToClipboard(template.id)}
                  className="btn-copy"
                  title="Copy ID"
                >
                  📋
                </button>
              </div>
            </div>
            <div className="detail-item">
              <label>Name</label>
              <div className="detail-value">{template.name}</div>
            </div>
            <div className="detail-item">
              <label>Type</label>
              <div className="detail-value">
                <span className="badge badge-type">{template.templateType}</span>
              </div>
            </div>
            <div className="detail-item">
              <label>Format</label>
              <div className="detail-value">
                <span className="badge badge-format">{template.format}</span>
              </div>
            </div>
            <div className="detail-item">
              <label>Version</label>
              <div className="detail-value">{template.version}</div>
            </div>
            <div className="detail-item">
              <label>Deployment Tier</label>
              <div className="detail-value">
                <span className="badge badge-tier">{template.deploymentTier || 'Standard'}</span>
              </div>
            </div>
            {template.description && (
              <div className="detail-item full-width">
                <label>Description</label>
                <div className="detail-value">{template.description}</div>
              </div>
            )}
          </div>
        </section>

        {/* Status & Properties Section */}
        <section className="details-section">
          <h3>⚙️ Properties</h3>
          <div className="properties-grid">
            <div className="property-card">
              <div className="property-label">Status</div>
              <div className="property-value">
                {template.isActive ? (
                  <span className="status-badge status-active">✅ Active</span>
                ) : (
                  <span className="status-badge status-inactive">❌ Inactive</span>
                )}
              </div>
            </div>
            <div className="property-card">
              <div className="property-label">Visibility</div>
              <div className="property-value">
                {template.isPublic ? (
                  <span className="status-badge status-public">🌐 Public</span>
                ) : (
                  <span className="status-badge status-private">🔒 Private</span>
                )}
              </div>
            </div>
            {template.azureService && (
              <div className="property-card">
                <div className="property-label">Azure Service</div>
                <div className="property-value">{template.azureService}</div>
              </div>
            )}
            {template.filesCount !== undefined && (
              <div className="property-card">
                <div className="property-label">Files Count</div>
                <div className="property-value">{template.filesCount}</div>
              </div>
            )}
            {template.mainFileType && (
              <div className="property-card">
                <div className="property-label">Main File Type</div>
                <div className="property-value">{template.mainFileType}</div>
              </div>
            )}
          </div>
        </section>

        {/* Metadata Section */}
        <section className="details-section">
          <h3>📅 Metadata</h3>
          <div className="details-grid">
            <div className="detail-item">
              <label>Created By</label>
              <div className="detail-value">{template.createdBy}</div>
            </div>
            <div className="detail-item">
              <label>Created At</label>
              <div className="detail-value">{formatDate(template.createdAt)}</div>
            </div>
            <div className="detail-item">
              <label>Last Updated</label>
              <div className="detail-value">{formatDate(template.updatedAt)}</div>
            </div>
          </div>
        </section>

        {/* Generated Files Section */}
        {files.length > 0 && (
          <section className="details-section">
            <div className="section-header">
              <h3>📁 Generated Files ({files.length})</h3>
              <div className="section-header-actions">
                <button 
                  onClick={() => setShowFiles(!showFiles)}
                  className="btn-toggle"
                >
                  {showFiles ? '▼ Hide Files' : '▶ Show Files'}
                </button>
                {showFiles && (
                  <button 
                    onClick={downloadAllFiles}
                    className="btn-download-all"
                  >
                    💾 Download All
                  </button>
                )}
              </div>
            </div>
            
            {showFiles && (
              <div className="files-container">
                <div className="files-list">
                  <div className="files-list-header">
                    <span>Files</span>
                  </div>
                  {files.map((file, index) => (
                    <div 
                      key={index}
                      className={`file-item ${selectedFile?.fileName === file.fileName ? 'selected' : ''}`}
                      onClick={() => setSelectedFile(file)}
                    >
                      <span className="file-icon">{getFileIcon(file.fileName)}</span>
                      <div className="file-info">
                        <span className="file-name">{file.fileName}</span>
                        <span className="file-size">{formatFileSize(file.size)}</span>
                      </div>
                      {file.isEntryPoint && (
                        <span className="entry-point-badge">Entry</span>
                      )}
                    </div>
                  ))}
                </div>
                
                <div className="file-viewer">
                  {selectedFile ? (
                    <>
                      <div className="file-viewer-header">
                        <div className="file-viewer-title">
                          <span className="file-icon">{getFileIcon(selectedFile.fileName)}</span>
                          <span>{selectedFile.fileName}</span>
                          <span className="file-type-badge">{selectedFile.fileType}</span>
                          {isEditing && <span className="editing-badge">✏️ Editing</span>}
                        </div>
                        <div className="file-viewer-actions">
                          {isEditing ? (
                            <>
                              <button 
                                onClick={handleSaveFile}
                                className="btn-save"
                                disabled={saving}
                              >
                                {saving ? '⏳ Saving...' : '💾 Save'}
                              </button>
                              <button 
                                onClick={handleCancelEdit}
                                className="btn-cancel"
                                disabled={saving}
                              >
                                ❌ Cancel
                              </button>
                            </>
                          ) : (
                            <>
                              <button 
                                onClick={handleEditFile}
                                className="btn-edit"
                              >
                                ✏️ Edit
                              </button>
                              <button 
                                onClick={() => copyToClipboard(selectedFile.content)}
                                className="btn-copy"
                              >
                                📋 Copy
                              </button>
                              <button 
                                onClick={() => downloadFile(selectedFile)}
                                className="btn-download"
                              >
                                💾 Download
                              </button>
                            </>
                          )}
                        </div>
                      </div>
                      {isEditing ? (
                        <textarea
                          className="code-editor"
                          value={editedContent}
                          onChange={(e) => setEditedContent(e.target.value)}
                          spellCheck={false}
                        />
                      ) : (
                        <pre className="code-viewer">
                          <code className={getLanguageClass(selectedFile.fileName)}>
                            {selectedFile.content}
                          </code>
                        </pre>
                      )}
                    </>
                  ) : (
                    <div className="file-viewer-empty">
                      <p>Select a file to view its content</p>
                    </div>
                  )}
                </div>
              </div>
            )}
          </section>
        )}

        {/* Content Section (Legacy - shows aggregated content if no files available) */}
        {files.length === 0 && (
          <section className="details-section">
            <div className="section-header">
              <h3>📄 Template Content</h3>
              <button 
                onClick={() => setShowContent(!showContent)}
                className="btn-toggle"
              >
                {showContent ? '▼ Hide' : '▶ Show'}
              </button>
            </div>
            
            {showContent && (
              <div className="content-viewer">
                <div className="content-toolbar">
                  <span className="content-info">
                    {template.content.length} characters
                  </span>
                  <button 
                    onClick={() => copyToClipboard(template.content)}
                    className="btn-copy-content"
                  >
                    📋 Copy Content
                  </button>
                </div>
                <pre className="content-code">
                  <code>{template.content}</code>
                </pre>
              </div>
            )}
          </section>
        )}

        {/* Actions Section */}
        <section className="details-section">
          <h3>🚀 Actions</h3>
          <div className="action-buttons">
            <button 
              onClick={() => navigate(`/templates/${id}/edit`)}
              className="btn-action btn-primary"
            >
              ✏️ Edit Service Template
            </button>
            <button 
              onClick={() => {
                // Future: Deploy template
                alert('Deploy functionality coming soon!');
              }}
              className="btn-action btn-secondary"
            >
              🚀 Deploy
            </button>
            <button 
              onClick={() => {
                // Future: Clone template
                alert('Clone functionality coming soon!');
              }}
              className="btn-action btn-secondary"
            >
              📋 Clone
            </button>
            <button 
              onClick={() => {
                // Future: Export template
                const dataStr = JSON.stringify(template, null, 2);
                const dataUri = 'data:application/json;charset=utf-8,'+ encodeURIComponent(dataStr);
                const exportFileDefaultName = `${template.name}.json`;
                const linkElement = document.createElement('a');
                linkElement.setAttribute('href', dataUri);
                linkElement.setAttribute('download', exportFileDefaultName);
                linkElement.click();
              }}
              className="btn-action btn-secondary"
            >
              💾 Export
            </button>
          </div>
        </section>
      </div>
    </div>
  );
};

export default TemplateDetails;
