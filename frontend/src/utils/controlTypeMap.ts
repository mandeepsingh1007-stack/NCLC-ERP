/**
 * Maps metadata ControlType to Ant Design component and input config.
 */
import type { ControlType } from '../api/contracts/window';

export type InputType =
  | 'text'
  | 'number'
  | 'password'
  | 'email'
  | 'url'
  | 'textarea';

export interface ControlConfig {
  inputType: InputType | undefined;
  isTextArea: boolean;
  isPassword: boolean;
  isEmail: boolean;
  isURL: boolean;
}

export const CONTROL_CONFIG: Record<ControlType, ControlConfig> = {
  TextInput: { inputType: 'text', isTextArea: false, isPassword: false, isEmail: false, isURL: false },
  TextArea: { inputType: 'text', isTextArea: true, isPassword: false, isEmail: false, isURL: false },
  NumberInput: { inputType: 'number', isTextArea: false, isPassword: false, isEmail: false, isURL: false },
  DateInput: { inputType: undefined, isTextArea: false, isPassword: false, isEmail: false, isURL: false },
  YesNoToggle: { inputType: undefined, isTextArea: false, isPassword: false, isEmail: false, isURL: false },
  ListDropdown: { inputType: undefined, isTextArea: false, isPassword: false, isEmail: false, isURL: false },
  TableLookup: { inputType: undefined, isTextArea: false, isPassword: false, isEmail: false, isURL: false },
  SearchPopup: { inputType: undefined, isTextArea: false, isPassword: false, isEmail: false, isURL: false },
  MultiSelect: { inputType: undefined, isTextArea: false, isPassword: false, isEmail: false, isURL: false },
  Email: { inputType: 'email', isTextArea: false, isPassword: false, isEmail: true, isURL: false },
  URL: { inputType: 'url', isTextArea: false, isPassword: false, isEmail: false, isURL: true },
  Password: { inputType: 'password', isTextArea: false, isPassword: true, isEmail: false, isURL: false },
  RichText: { inputType: undefined, isTextArea: false, isPassword: false, isEmail: false, isURL: false },
  Image: { inputType: undefined, isTextArea: false, isPassword: false, isEmail: false, isURL: false },
  FileUpload: { inputType: undefined, isTextArea: false, isPassword: false, isEmail: false, isURL: false },
  Date: { inputType: undefined, isTextArea: false, isPassword: false, isEmail: false, isURL: false },
  Time: { inputType: undefined, isTextArea: false, isPassword: false, isEmail: false, isURL: false },
  DateTime: { inputType: undefined, isTextArea: false, isPassword: false, isEmail: false, isURL: false },
};

export function resolveControlConfig(controlType: ControlType): ControlConfig {
  const config = CONTROL_CONFIG[controlType];
  return config ?? CONTROL_CONFIG.TextInput;
}

export function resolveUnknownControlType(controlType: string): ControlType {
  if (controlType in CONTROL_CONFIG) return controlType as ControlType;
  return 'TextInput';
}
