/**
 * React error boundary — catches rendering errors in dynamic components.
 */
import React, { Component, type ErrorInfo, type ReactNode } from 'react';
import { Alert } from 'antd';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('[ErrorBoundary]', error.message, info.componentStack);
  }

  render() {
    if (this.state.hasError) {
      if (this.props.fallback) return this.props.fallback;

      return (
        <Alert
          message="Component Error"
          description={this.state.error?.message ?? 'An unexpected error occurred while rendering this component.'}
          type="error"
          showIcon
          action={
            <button
              type="button"
              onClick={() => this.setState({ hasError: false, error: null })}
            >
              Retry
            </button>
          }
        />
      );
    }

    return this.props.children;
  }
}

export default ErrorBoundary;
