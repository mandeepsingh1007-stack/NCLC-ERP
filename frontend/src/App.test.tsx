/**
 * App Component Test
 *
 * Verifies that the App shell renders without crashing.
 * React router is mocked since we test routing behavior via
 * component-level tests (MainWindow, MainGrid, MenuNavigation).
 */
import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';

// Mock react-router-dom hooks to avoid Jest/Node.js incompatibility with
// react-router v7 SSR runtime (crypto.subtle dependency).
jest.mock('react-router-dom', () => ({
  BrowserRouter: ({ children }: { children: React.ReactNode }) => <div data-testid="browser-router">{children}</div>,
  Routes: ({ children }: { children: React.ReactNode }) => <div data-testid="routes">{children}</div>,
  Route: ({ element }: { element: React.ReactNode }) => element,
  useParams: () => ({ windowId: '1' }),
  useNavigate: () => jest.fn(),
}));

import App from './App';

test('renders without crashing', () => {
  render(<App />);
  // Verify the AppLayout shell rendered — ConfigProvider adds an ant-pro-layout class
  // or we fall back to checking the browser router wrapper exists
  expect(screen.getByTestId('browser-router')).toBeInTheDocument();
});
