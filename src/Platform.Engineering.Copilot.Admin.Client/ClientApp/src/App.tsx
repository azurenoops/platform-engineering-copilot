import React, { useState, useEffect } from 'react';
import { BrowserRouter as Router, Routes, Route, Link, Navigate } from 'react-router-dom';
import './App.css';
import './theme.css';
import Dashboard from './components/Dashboard';
import TemplateList from './components/TemplateList';
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

function App() {
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);

  // Keyboard shortcut for global search (Ctrl/Cmd+K)
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault();
        setSearchOpen(true);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);
  return (
    <Router>
      <div className="App">
        <nav className="navbar">
          <div className="navbar-brand">
            <h1>🔧 Platform Engineering Admin Console</h1>
          </div>
          <ul className="navbar-menu">
            <li><Link to="/">Dashboard</Link></li>
            <li><Link to="/catalog">Service Catalog</Link></li>            
            <li><Link to="/environments">Environments</Link></li>
            <li><Link to="/infrastructure">Provision</Link></li>
            <li><Link to="/costs">Cost Insights</Link></li>
            <li><Link to="/insights">Platform Insights</Link></li>
            <li>
              <button 
                onClick={() => setSearchOpen(true)} 
                className="search-icon-btn"
                title="Search (Ctrl/Cmd+K)"
              >
                🔍
              </button>
            </li>
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
            <Route path="/" element={<Dashboard />} />
            <Route path="/insights" element={<InsightsPage />} />
            <Route path="/catalog" element={<ServiceCatalog />} />
            <Route path="/templates" element={<Navigate to="/catalog" replace />} />
            <Route path="/templates/create" element={<CreateTemplate />} />
            <Route path="/templates/:id/edit" element={<CreateTemplate />} />
            <Route path="/templates/:id" element={<TemplateDetails />} />
            <Route path="/environments" element={<EnvironmentList />} />
            <Route path="/environments/create" element={<CreateEnvironment />} />
            <Route path="/environments/:name" element={<EnvironmentDetails />} />
            <Route path="/infrastructure" element={<InfrastructureManagement />} />
            <Route path="/costs" element={<CostInsights />} />
          </Routes>
        </main>

        <footer className="footer">
          <p>Platform Engineering Admin Console v0.4 | Port 7003 | Admin API: http://localhost:7002</p>
        </footer>

        <Settings isOpen={settingsOpen} onClose={() => setSettingsOpen(false)} />
        <GlobalSearch isOpen={searchOpen} onClose={() => setSearchOpen(false)} />
      </div>
    </Router>
  );
}

export default App;
