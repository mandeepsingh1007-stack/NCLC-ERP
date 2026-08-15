/**
 * DynamicField Component Tests
 *
 * Tests all control types and rendering behavior.
 * useDisplayLogic is mocked to control visibility/override behavior.
 */
import React from 'react';
import { render } from '@testing-library/react';
import '@testing-library/jest-dom';
import DynamicField from '../components/DynamicField';
import { useDisplayLogic } from '../hooks/useDisplayLogic';
import type { FieldContract } from '../api/contracts/window';

// Default mock: show field, no read-only/mandatory overrides
jest.mock('../hooks/useDisplayLogic', () => ({
  useDisplayLogic: jest.fn(() => true),
}));

const mockUseDisplayLogic = useDisplayLogic as jest.MockedFunction<typeof useDisplayLogic>;

function makeField(overrides: Partial<FieldContract> = {}): FieldContract {
  return {
    columnName: 'TestField',
    label: 'Test Field',
    controlType: 'TextInput',
    isMandatory: false,
    isReadOnly: false,
    isMandatoryOverride: false,
    isReadOnlyOverride: false,
    colSpan: 6,
    rowSpan: 1,
    ...overrides,
  };
}

describe('DynamicField', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    // Default: show field, no overrides
    mockUseDisplayLogic.mockImplementation((expression) => {
      if (expression == null) return true;
      return true;
    });
  });

  // ─── TextInput ──────────────────────────────────────────────────────

  describe('TextInput', () => {
    it('renders a TextInput', () => {
      const field = makeField({ controlType: 'TextInput', label: 'Name' });
      const { container } = render(<DynamicField field={field} formData={{}} context={{}} />);
      const input = container.querySelector('input');
      expect(input).toBeInTheDocument();
    });

    it('renders a TextArea', () => {
      const field = makeField({ controlType: 'TextArea', label: 'Notes' });
      const { container } = render(<DynamicField field={field} formData={{}} context={{}} />);
      const textarea = container.querySelector('textarea');
      expect(textarea).toBeInTheDocument();
    });

    it('renders a NumberInput', () => {
      const field = makeField({ controlType: 'NumberInput', label: 'Count' });
      const { container } = render(<DynamicField field={field} formData={{}} context={{}} />);
      const input = container.querySelector('input');
      expect(input).toBeInTheDocument();
    });

    it('renders a YesNoToggle (Switch)', () => {
      const field = makeField({ controlType: 'YesNoToggle', label: 'Active' });
      const { container } = render(<DynamicField field={field} formData={{}} context={{}} />);
      const switchEl = container.querySelector('.ant-switch');
      expect(switchEl).toBeInTheDocument();
    });

    it('renders an Email input', () => {
      const field = makeField({ controlType: 'Email', label: 'Email' });
      const { container } = render(<DynamicField field={field} formData={{}} context={{}} />);
      const input = container.querySelector('input[type="email"]');
      expect(input).toBeInTheDocument();
    });

    it('renders a URL input', () => {
      const field = makeField({ controlType: 'URL', label: 'Website' });
      const { container } = render(<DynamicField field={field} formData={{}} context={{}} />);
      const input = container.querySelector('input[type="url"]');
      expect(input).toBeInTheDocument();
    });

    it('renders a Password input', () => {
      const field = makeField({ controlType: 'Password', label: 'Password' });
      const { container } = render(<DynamicField field={field} formData={{}} context={{}} />);
      const inputs = container.querySelectorAll('input');
      expect(inputs.length).toBeGreaterThan(0);
    });
  });

  // ─── Dropdowns ──────────────────────────────────────────────────────

  describe('Dropdowns', () => {
    it('renders a ListDropdown with lookup data', () => {
      const field = makeField({
        controlType: 'ListDropdown',
        label: 'Status',
        columnName: 'Status',
      });
      const lookupData = [
        { value: 'Active', display: 'Active' },
        { value: 'Inactive', display: 'Inactive' },
      ];
      const { container } = render(
        <DynamicField field={field} formData={{}} context={{}} lookupData={lookupData} />,
      );
      const select = container.querySelector('.ant-select');
      expect(select).toBeInTheDocument();
    });

    it('renders a MultiSelect', () => {
      const field = makeField({
        controlType: 'MultiSelect',
        label: 'Tags',
        columnName: 'Tags',
      });
      const lookupData = [
        { value: 'tag1', display: 'Tag 1' },
        { value: 'tag2', display: 'Tag 2' },
      ];
      const { container } = render(
        <DynamicField field={field} formData={{}} context={{}} lookupData={lookupData} />,
      );
      const select = container.querySelector('.ant-select');
      expect(select).toBeInTheDocument();
    });

    it('renders a TableLookup', () => {
      const field = makeField({
        controlType: 'TableLookup',
        label: 'Account',
        columnName: 'AccountId',
      });
      const lookupData = [
        { value: 1, display: 'Account A' },
        { value: 2, display: 'Account B' },
      ];
      const { container } = render(
        <DynamicField field={field} formData={{}} context={{}} lookupData={lookupData} />,
      );
      const select = container.querySelector('.ant-select');
      expect(select).toBeInTheDocument();
    });
  });

  // ─── Date Inputs ────────────────────────────────────────────────────

  describe('Date Inputs', () => {
    it('renders a Date input', () => {
      const field = makeField({ controlType: 'Date', label: 'Date' });
      const { container } = render(<DynamicField field={field} formData={{}} context={{}} />);
      const input = container.querySelector('input');
      expect(input).toBeInTheDocument();
    });

    it('renders a DateTime input', () => {
      const field = makeField({ controlType: 'DateTime', label: 'Date/Time' });
      const { container } = render(<DynamicField field={field} formData={{}} context={{}} />);
      const input = container.querySelector('input');
      expect(input).toBeInTheDocument();
    });

    it('renders a Time input', () => {
      const field = makeField({ controlType: 'Time', label: 'Time' });
      const { container } = render(<DynamicField field={field} formData={{}} context={{}} />);
      const input = container.querySelector('input');
      expect(input).toBeInTheDocument();
    });
  });

  // ─── Display Logic ──────────────────────────────────────────────────

  describe('Display Logic', () => {
    it('hides field when displayLogic evaluates to false', () => {
      mockUseDisplayLogic.mockImplementation(() => false);
      const field = makeField({
        controlType: 'TextInput',
        label: 'Secret',
      });
      const { container } = render(<DynamicField field={field} formData={{}} context={{}} />);
      expect(container.firstChild).toBeNull();
    });

    it('shows field when displayLogic evaluates to true', () => {
      mockUseDisplayLogic.mockImplementation(() => true);
      const field = makeField({ controlType: 'TextInput', label: 'Normal' });
      const { container } = render(<DynamicField field={field} formData={{}} context={{}} />);
      expect(container.firstChild).not.toBeNull();
    });
  });

  // ─── Read-Only Logic ────────────────────────────────────────────────

  describe('Read-Only Logic', () => {
    it('applies base isReadOnly', () => {
      const field = makeField({ controlType: 'TextInput', label: 'Name', isReadOnly: true });
      const { container } = render(<DynamicField field={field} formData={{}} context={{}} />);
      const input = container.querySelector('input');
      expect(input).toBeDisabled();
    });

    it('respects readOnlyLogic return value', () => {
      // displayLogic returns true (show), readOnlyLogic returns false (not overridden), mandatoryLogic returns false
      const calls: string[] = [];
      mockUseDisplayLogic.mockImplementation((expression) => {
        if (expression == null || expression === '') {
          // displayLogic is null/undefined — show field
          calls.push('display');
          return true;
        }
        // readOnlyLogic or mandatoryLogic called — return false (no override)
        calls.push('logic');
        return false;
      });
      const field = makeField({
        controlType: 'TextInput',
        label: 'Name',
        readOnlyLogic: '$Locked == true',
      });
      const { container } = render(
        <DynamicField field={field} formData={{}} context={{}} />,
      );
      const input = container.querySelector('input');
      expect(input).not.toBeDisabled();
    });
  });

  // ─── Unknown Control Type ───────────────────────────────────────────

  describe('Unknown Control Type', () => {
    it('falls back to regular Input for unknown control types', () => {
      const field = makeField({
        controlType: 'CustomControl' as unknown as 'TextInput',
        label: 'Custom',
      });
      const { container } = render(<DynamicField field={field} formData={{}} context={{}} />);
      // Should render the fallback Input, not crash
      expect(container.querySelector('input')).toBeInTheDocument();
    });
  });
});
