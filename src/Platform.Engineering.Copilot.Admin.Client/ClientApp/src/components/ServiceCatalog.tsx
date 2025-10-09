import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import adminApi, { Template } from '../services/adminApi';
import './ServiceCatalog.css';

interface CatalogItem {
  id: string;
  name: string;
  type: 'service' | 'infrastructure' | 'api' | 'library';
  description: string;
  owner?: string;
  status: 'active' | 'inactive' | 'deprecated';
  tags: string[];
  createdAt: string;
  updatedAt?: string;
  links: {
    template?: string;
    documentation?: string;
    repository?: string;
    monitoring?: string;
  };
  metadata?: any;
}

const ServiceCatalog: React.FC = () => {
  const navigate = useNavigate();
  const [catalogItems, setCatalogItems] = useState<CatalogItem[]>([]);
  const [filteredItems, setFilteredItems] = useState<CatalogItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  
  // Filters
  const [searchQuery, setSearchQuery] = useState('');
  const [typeFilter, setTypeFilter] = useState<string>('all');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [tagFilter, setTagFilter] = useState<string>('');
  
  // View mode
  const [viewMode, setViewMode] = useState<'grid' | 'list'>('grid');

  useEffect(() => {
    loadCatalog();
  }, []);

  useEffect(() => {
    applyFilters();
  }, [catalogItems, searchQuery, typeFilter, statusFilter, tagFilter]);

  const loadCatalog = async () => {
    try {
      setLoading(true);
      
      // Load templates and convert to catalog items
      const templates = await adminApi.listTemplates();
      const environments = await adminApi.listEnvironments();
      
      const items: CatalogItem[] = [];
      
      // Add templates as catalog items
      templates.forEach((template: Template) => {
        const templateTags = (template as any).tags;
        items.push({
          id: `template-${template.id}`,
          name: template.name,
          type: template.templateType === 'infrastructure' ? 'infrastructure' : 'service',
          description: template.description || 'No description',
          owner: (template as any).owner || 'Platform Team',
          status: template.isActive ? 'active' : 'inactive',
          tags: Array.isArray(templateTags) ? templateTags : [],
          createdAt: template.createdAt || new Date().toISOString(),
          updatedAt: template.updatedAt,
          links: {
            template: `/templates/${template.id}`,
            documentation: (template as any).documentationUrl,
            repository: (template as any).repositoryUrl
          },
          metadata: {
            format: template.format,
            version: template.version,
            templateType: template.templateType
          }
        });
      });
      
      // Add environments as catalog items
      environments.environments.forEach((env: any) => {
        const envTags = env.tags;
        const tagsArray = Array.isArray(envTags) ? envTags : [env.environmentType, env.location].filter(Boolean);
        
        items.push({
          id: `env-${env.id}`,
          name: env.name,
          type: 'service',
          description: `${env.environmentType} environment in ${env.location}`,
          owner: env.owner || 'Unknown',
          status: env.status?.toLowerCase() === 'running' ? 'active' : 'inactive',
          tags: tagsArray,
          createdAt: env.createdAt,
          updatedAt: env.updatedAt,
          links: {
            template: `/environments/${env.name}`,
            monitoring: env.monitoringUrl
          },
          metadata: {
            resourceGroup: env.resourceGroup,
            location: env.location,
            environmentType: env.environmentType
          }
        });
      });
      
      setCatalogItems(items);
      setError(null);
    } catch (err: any) {
      setError(err.message || 'Failed to load catalog');
    } finally {
      setLoading(false);
    }
  };

  const applyFilters = () => {
    let filtered = [...catalogItems];
    
    // Search filter
    if (searchQuery) {
      const query = searchQuery.toLowerCase();
      filtered = filtered.filter(item =>
        item.name.toLowerCase().includes(query) ||
        item.description.toLowerCase().includes(query) ||
        item.owner?.toLowerCase().includes(query) ||
        item.tags.some(tag => tag.toLowerCase().includes(query))
      );
    }
    
    // Type filter
    if (typeFilter !== 'all') {
      filtered = filtered.filter(item => item.type === typeFilter);
    }
    
    // Status filter
    if (statusFilter !== 'all') {
      filtered = filtered.filter(item => item.status === statusFilter);
    }
    
    // Tag filter
    if (tagFilter) {
      filtered = filtered.filter(item =>
        item.tags.some(tag => tag.toLowerCase().includes(tagFilter.toLowerCase()))
      );
    }
    
    setFilteredItems(filtered);
  };

  const getTypeIcon = (type: string) => {
    switch (type) {
      case 'service': return '🚀';
      case 'infrastructure': return '🏗️';
      case 'api': return '🔌';
      case 'library': return '📦';
      default: return '📄';
    }
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'active': return '#28a745';
      case 'inactive': return '#6c757d';
      case 'deprecated': return '#dc3545';
      default: return '#6c757d';
    }
  };

  const allTags = Array.from(new Set(catalogItems.flatMap(item => item.tags)));

  if (loading) {
    return <div className="catalog-loading">Loading service catalog...</div>;
  }

  if (error) {
    return <div className="catalog-error">Error: {error}</div>;
  }

  return (
    <div className="service-catalog">
      <div className="catalog-header">
        <div>
          <h2>📚 Service Template Catalog</h2>
          <p className="catalog-subtitle">
            Explore all service templates in one place
          </p>
        </div>
        <div className="catalog-header-actions">
          <div className="catalog-stats">
            <div className="stat-item">
              <span className="stat-label">Total</span>
              <span className="stat-value">{catalogItems.length}</span>
            </div>
            <div className="stat-item">
              <span className="stat-label">Active</span>
              <span className="stat-value" style={{color: '#28a745'}}>
                {catalogItems.filter(i => i.status === 'active').length}
              </span>
            </div>
          </div>
          <button 
            className="create-template-btn"
            onClick={() => navigate('/templates/create')}
            title="Create new service template"
          >
            ➕ Create Template
          </button>
        </div>
      </div>

      <div className="catalog-filters">
        <div className="filter-row">
          <div className="search-box">
            <span className="search-icon">🔍</span>
            <input
              type="text"
              placeholder="Search by name, owner, description, or tags..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="filter-input"
            />
          </div>
          
          <select
            value={typeFilter}
            onChange={(e) => setTypeFilter(e.target.value)}
            className="filter-select"
          >
            <option value="all">All Types</option>
            <option value="service">Services</option>
            <option value="infrastructure">Infrastructure</option>
            <option value="api">APIs</option>
            <option value="library">Libraries</option>
          </select>
          
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            className="filter-select"
          >
            <option value="all">All Status</option>
            <option value="active">Active</option>
            <option value="inactive">Inactive</option>
            <option value="deprecated">Deprecated</option>
          </select>
          
          <div className="view-toggle">
            <button
              className={`view-btn ${viewMode === 'grid' ? 'active' : ''}`}
              onClick={() => setViewMode('grid')}
              title="Grid view"
            >
              ⊞
            </button>
            <button
              className={`view-btn ${viewMode === 'list' ? 'active' : ''}`}
              onClick={() => setViewMode('list')}
              title="List view"
            >
              ☰
            </button>
          </div>
        </div>
        
        {allTags.length > 0 && (
          <div className="tag-filters">
            <span className="tag-label">Filter by tag:</span>
            {allTags.slice(0, 10).map(tag => (
              <button
                key={tag}
                className={`tag-filter-btn ${tagFilter === tag ? 'active' : ''}`}
                onClick={() => setTagFilter(tagFilter === tag ? '' : tag)}
              >
                {tag}
              </button>
            ))}
          </div>
        )}
      </div>

      <div className="catalog-results">
        <div className="results-header">
          <span>Showing {filteredItems.length} of {catalogItems.length} items</span>
          {(searchQuery || typeFilter !== 'all' || statusFilter !== 'all' || tagFilter) && (
            <button
              className="clear-filters-btn"
              onClick={() => {
                setSearchQuery('');
                setTypeFilter('all');
                setStatusFilter('all');
                setTagFilter('');
              }}
            >
              Clear Filters
            </button>
          )}
        </div>

        <div className={`catalog-items ${viewMode}`}>
          {filteredItems.map(item => (
            <div
              key={item.id}
              className="catalog-item"
              onClick={() => item.links.template && navigate(item.links.template)}
            >
              <div className="item-header">
                <span className="item-icon">{getTypeIcon(item.type)}</span>
                <div className="item-title-section">
                  <h3 className="item-title">{item.name}</h3>
                  <span className="item-type">{item.type}</span>
                </div>
                <span
                  className="item-status"
                  style={{ background: getStatusColor(item.status) }}
                >
                  {item.status}
                </span>
              </div>

              <p className="item-description">{item.description}</p>

              {item.owner && (
                <div className="item-owner">
                  <span className="owner-icon">👤</span>
                  <span>{item.owner}</span>
                </div>
              )}

              {item.tags.length > 0 && (
                <div className="item-tags">
                  {item.tags.slice(0, 5).map(tag => (
                    <span key={tag} className="item-tag">{tag}</span>
                  ))}
                </div>
              )}

              <div className="item-links">
                {item.links.documentation && (
                  <a href={item.links.documentation} className="item-link" onClick={(e) => e.stopPropagation()}>
                    📖 Docs
                  </a>
                )}
                {item.links.repository && (
                  <a href={item.links.repository} className="item-link" onClick={(e) => e.stopPropagation()}>
                    🔗 Repo
                  </a>
                )}
                {item.links.monitoring && (
                  <a href={item.links.monitoring} className="item-link" onClick={(e) => e.stopPropagation()}>
                    📊 Monitor
                  </a>
                )}
              </div>

              <div className="item-footer">
                <small>Created {new Date(item.createdAt).toLocaleDateString()}</small>
              </div>
            </div>
          ))}
        </div>

        {filteredItems.length === 0 && (
          <div className="no-results">
            <p>No items found matching your filters</p>
            <button
              className="btn-primary"
              onClick={() => {
                setSearchQuery('');
                setTypeFilter('all');
                setStatusFilter('all');
                setTagFilter('');
              }}
            >
              Clear Filters
            </button>
          </div>
        )}
      </div>
    </div>
  );
};

export default ServiceCatalog;
