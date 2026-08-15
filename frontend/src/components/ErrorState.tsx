/**
 * ErrorState — reusable error display with retry.
 */
import React from 'react';
import { Alert, Button } from 'antd';

interface Props {
  message: string;
  description?: string;
  onRetry?: () => void;
}

const ErrorState: React.FC<Props> = ({ message, description, onRetry }) => (
  <Alert
    message={message}
    description={description}
    type="error"
    showIcon
    action={
      onRetry && (
        <Button size="small" onClick={onRetry}>
          Retry
        </Button>
      )
    }
  />
);

export default ErrorState;
