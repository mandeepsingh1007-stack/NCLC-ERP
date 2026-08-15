/**
 * EmptyState — reusable empty placeholder.
 */
import React from 'react';
import { Empty as AntEmpty } from 'antd';

interface Props {
  description?: string;
  image?: string;
}

const EmptyState: React.FC<Props> = ({ description = 'No records found.' }) => (
  <AntEmpty description={description} />
);

export default EmptyState;
