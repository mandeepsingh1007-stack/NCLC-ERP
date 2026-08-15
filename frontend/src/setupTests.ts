// jest-dom adds custom jest matchers for asserting on DOM nodes.
// allows you to do things like:
// expect(element).toHaveTextContent(/react/i)
// learn more: https://github.com/testing-library/jest-dom
import React from 'react';
import '@testing-library/jest-dom';

// Mock matchMedia for Ant Design responsive breakpoints
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
});

// Mock all Ant Design icons to avoid ESM issues with @ant-design/colors
// This prevents Jest from trying to parse ESM files in node_modules
const MockIcon = (props: any) => React.createElement('span', props);
jest.mock('@ant-design/icons', () => ({
  ArrowLeftOutlined: MockIcon,
  SearchOutlined: MockIcon,
  ReloadOutlined: MockIcon,
  HomeOutlined: MockIcon,
  MenuOutlined: MockIcon,
  SettingOutlined: MockIcon,
  UserOutlined: MockIcon,
  TeamOutlined: MockIcon,
  FileTextOutlined: MockIcon,
  DashboardOutlined: MockIcon,
  Outlined: {},
  InternalFilled: {},
  Filled: {},
}));
