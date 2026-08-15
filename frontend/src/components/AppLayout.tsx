/**
 * Main application layout with MenuNavigation sidebar and content area.
 */
import React from 'react';
import { Outlet, useNavigate } from 'react-router-dom';
import { Layout, Spin } from 'antd';
import MenuNavigation from './MenuNavigation';
import ErrorBoundary from './ErrorBoundary';
import { useMenu } from '../api/metaApi';
import { useMenuTree } from '../hooks/useMenuTree';

const { Sider } = Layout;

const AppLayout: React.FC = () => {
  const { data: menuData, isLoading, error } = useMenu();
  const menuTree = useMenuTree(menuData?.items ?? []);
  const navigate = useNavigate();

  const handleMenuClick = (menuId: number, windowId: number | null) => {
    if (windowId) {
      navigate(`/window/${windowId}`);
    }
  };

  if (isLoading) {
    return (
      <Layout style={{ minHeight: '100vh' }}>
        <Spin size="large" style={{ display: 'block', margin: '200px auto' }} />
      </Layout>
    );
  }

  if (error) {
    return (
      <Layout style={{ minHeight: '100vh' }}>
        <Layout.Content style={{ padding: 24 }}>
          <div style={{ textAlign: 'center', padding: 40, color: '#ff4d4f' }}>
            <h3>Error loading menu</h3>
            <p>{error.message ?? 'Failed to load menu metadata.'}</p>
          </div>
        </Layout.Content>
      </Layout>
    );
  }

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider
        width={250}
        style={{ overflow: 'auto', height: '100vh', position: 'fixed', left: 0, top: 0, bottom: 0 }}
        collapsible
      >
        <div style={{ padding: '16px 12px', fontSize: 18, fontWeight: 600, color: '#fff', background: '#001529' }}>
          No-Code Platform
        </div>
        <MenuNavigation
          tree={menuTree}
          onSelect={handleMenuClick}
        />
      </Sider>
      <Layout.Sider style={{ marginLeft: 250 }} />
      <Layout style={{ marginLeft: 250 }}>
        <ErrorBoundary>
          <Layout.Content style={{ padding: 24 }}>
            <Outlet />
          </Layout.Content>
        </ErrorBoundary>
      </Layout>
    </Layout>
  );
};

export default AppLayout;
