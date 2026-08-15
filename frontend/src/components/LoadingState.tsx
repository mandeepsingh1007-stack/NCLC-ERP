/**
 * LoadingState — reusable loading overlay.
 */
import React from 'react';
import { Spin } from 'antd';

interface Props {
  spinning?: boolean;
  size?: 'small' | 'default' | 'large';
  tip?: string;
}

const LoadingState: React.FC<Props> = ({ spinning = true, size = 'large', tip }) => (
  <Spin spinning={spinning} size={size} tip={tip} style={{ display: 'block' }} />
);

export default LoadingState;
