import React, { useState, useEffect } from 'react';
import { BrowserRouter as Router, Routes, Route, Link, Navigate } from 'react-router-dom';
import './App.css';
import './theme.css';
import Dashboard from './components/Dashboard';
import CreateTemplate from './components/CreateTemplate';
import TemplateDetails from './components/TemplateDetails';
import InfrastructureManagement from './components/InfrastructureManagement';
import EnvironmentList from './components/EnvironmentList';
import CreateEnvironment from './components/CreateEnvironment';
import EnvironmentDetails from './components/EnvironmentDetails';
import InsightsPage from './components/InsightsPage';
import Settings from './components/Settings';
import GlobalSearch from './components/GlobalSearch';
import ServiceCatalog from './components/ServiceCatalog';
import CostInsights from './components/CostInsights';
import Agents from './components/Agents';
import { FeatureFlags, loadFeatureFlags } from './utils/featureFlags';

function App() {
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const [featureFlags, setFeatureFlags] = useState<FeatureFlags>(loadFeatureFlags());

  // Listen for storage changes to sync feature flags across components
  useEffect(() => {
    const handleStorageChange = () => {
      setFeatureFlags(loadFeatureFlags());
    };

    window.addEventListener('storage', handleStorageChange);

    // Also listen for custom event when settings change in same window
    const handleFeatureFlagsUpdate = () => {
      setFeatureFlags(loadFeatureFlags());
    };

    window.addEventListener('featureFlagsUpdated', handleFeatureFlagsUpdate);

    return () => {
      window.removeEventListener('storage', handleStorageChange);
      window.removeEventListener('featureFlagsUpdated', handleFeatureFlagsUpdate);
    };
  }, []);

  // Keyboard shortcut for global search (Ctrl/Cmd+K)
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (featureFlags.globalSearch && (e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault();
        setSearchOpen(true);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [featureFlags.globalSearch]);

  // When settings close, reload feature flags
  const handleSettingsClose = () => {
    setSettingsOpen(false);
    setFeatureFlags(loadFeatureFlags());
  };

  // Find first enabled feature for default redirect
  const getDefaultRoute = () => {
    '/dashboard';
    '/agents';
    if (featureFlags.serviceCatalog) return '/catalog';
    if (featureFlags.environments) return '/environments';
    if (featureFlags.provision) return '/infrastructure';
    if (featureFlags.costInsights) return '/costs';
    if (featureFlags.platformInsights) return '/insights';
    return '/';
  };
  return (
    <Router>
      <div className="App">
        <nav className="navbar">
          <div className="navbar-brand">
            <h1>🔧 Platform Engineering Copilot Admin Console</h1>
          </div>
          <ul className="navbar-menu">
            <li><Link to="/dashboard">Dashboard</Link></li>
            <li><Link to="/agents">Agents</Link></li>
            {featureFlags.serviceCatalog && <li><Link to="/catalog">Service Catalog</Link></li>}
            {featureFlags.environments && <li><Link to="/environments">Environments</Link></li>}
            {featureFlags.provision && <li><Link to="/infrastructure">Provision</Link></li>}
            {featureFlags.costInsights && <li><Link to="/costs">Cost Insights</Link></li>}
            {featureFlags.platformInsights && <li><Link to="/insights">Platform Insights</Link></li>}
            {featureFlags.globalSearch && (
              <li>
                <button
                  onClick={() => setSearchOpen(true)}
                  className="search-icon-btn"
                  title="Search (Ctrl/Cmd+K)"
                >
                  🔍
                </button>
              </li>
            )}
            <li>
              <button
                onClick={() => setSettingsOpen(true)}
                className="settings-gear-btn"
                title="Settings"
              >
                ⚙️
              </button>
            </li>
          </ul>
        </nav>

        <main className="main-content">
          <Routes>
            {/* Default route - redirect to first enabled feature */}
            <Route path="/" element={
              <Navigate to={getDefaultRoute()} replace />
            } />

            {/* Dashboard */}
            <Route path="/dashboard" element={<Dashboard />} />

            {/* Agent Management */}
            <Route path="/agents" element={<Agents />} />

            {/* Platform Insights */}
            {featureFlags.platformInsights && <Route path="/insights" element={<InsightsPage />} />}

            {/* Service Catalog and Templates */}
            {featureFlags.serviceCatalog && (
              <>
                <Route path="/catalog" element={<ServiceCatalog />} />
                <Route path="/templates" element={<Navigate to="/catalog" replace />} />
                <Route path="/templates/create" element={<CreateTemplate />} />
                <Route path="/templates/:id/edit" element={<CreateTemplate />} />
                <Route path="/templates/:id" element={<TemplateDetails />} />
              </>
            )}

            {/* Environments */}
            {featureFlags.environments && (
              <>
                <Route path="/environments" element={<EnvironmentList />} />
                <Route path="/environments/create" element={<CreateEnvironment />} />
                <Route path="/environments/:name" element={<EnvironmentDetails />} />
              </>
            )}

            {/* Infrastructure Provisioning */}
            {featureFlags.provision && <Route path="/infrastructure" element={<InfrastructureManagement />} />}

            {/* Cost Insights */}
            {featureFlags.costInsights && <Route path="/costs" element={<CostInsights />} />}

            {/* Catch-all redirect to default route for disabled features */}
            <Route path="*" element={<Navigate to={getDefaultRoute()} replace />} />
          </Routes>
        </main>

        <footer className="footer">
          <p>Platform Engineering Copilot Admin Console v0.4 | Port 5003 | Admin API: http://localhost:5002</p>
        </footer>

        <Settings isOpen={settingsOpen} onClose={handleSettingsClose} />
        {featureFlags.globalSearch && <GlobalSearch isOpen={searchOpen} onClose={() => setSearchOpen(false)} />}
      </div>
    </Router>
  );
}

export default App;
