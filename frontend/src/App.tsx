import React, { Suspense } from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ConfigProvider, Spin } from 'antd';

// Lazy-loaded route components for code splitting
const AppLayout = React.lazy(() => import('./components/AppLayout'));
const MainWindow = React.lazy(() => import('./pages/MainWindow'));
const MainGrid = React.lazy(() => import('./pages/MainGrid'));

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

const LoadingFallback = () => (
  <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}>
    <Spin size="large" />
  </div>
);

function App() {
  return (
    <ConfigProvider
      theme={{
        token: {
          colorPrimary: '#1677FF',
          borderRadius: 6,
          fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
          fontSize: 14,
        },
      }}
    >
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <Suspense fallback={<LoadingFallback />}>
            <Routes>
              <Route path="*" element={<AppLayout />} />
              <Route path="/window/:windowId" element={<MainWindow />} />
              <Route path="/grid/:windowId" element={<MainGrid />} />
            </Routes>
          </Suspense>
        </BrowserRouter>
      </QueryClientProvider>
    </ConfigProvider>
  );
}

export default App;
