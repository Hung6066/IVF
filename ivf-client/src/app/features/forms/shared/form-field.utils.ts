import { FieldType, FormField, FieldTypeLabels } from '../forms.service';

/**
 * Shared utility functions for form field operations.
 * Used by both form-builder and form-renderer to eliminate duplication.
 */

/** Map of string field type names to numeric enum values */
const FIELD_TYPE_MAP: { [key: string]: FieldType } = {
  Text: FieldType.Text,
  TextArea: FieldType.TextArea,
  Number: FieldType.Number,
  Decimal: FieldType.Decimal,
  Date: FieldType.Date,
  DateTime: FieldType.DateTime,
  Time: FieldType.Time,
  Dropdown: FieldType.Dropdown,
  MultiSelect: FieldType.MultiSelect,
  Radio: FieldType.Radio,
  Checkbox: FieldType.Checkbox,
  FileUpload: FieldType.FileUpload,
  Rating: FieldType.Rating,
  Section: FieldType.Section,
  Label: FieldType.Label,
  Tags: FieldType.Tags,
  PageBreak: FieldType.PageBreak,
  Address: FieldType.Address,
  Hidden: FieldType.Hidden,
  Slider: FieldType.Slider,
  Calculated: FieldType.Calculated,
  RichText: FieldType.RichText,
  Signature: FieldType.Signature,
  Lookup: FieldType.Lookup,
  Repeater: FieldType.Repeater,
};

const HEIGHT_MAP: { [key: string]: string } = {
  auto: 'auto',
  small: '60px',
  medium: '100px',
  large: '150px',
  xlarge: '200px',
};

const OPTION_TYPES: FieldType[] = [
  FieldType.Dropdown,
  FieldType.MultiSelect,
  FieldType.Radio,
  FieldType.Checkbox,
  FieldType.Tags,
  FieldType.Lookup,
];

const OPTION_TYPE_NAMES = ['Dropdown', 'MultiSelect', 'Radio', 'Checkbox', 'Tags', 'Lookup'];

/** Normalize field type from API (string or number) to numeric enum */
export function normalizeFieldType(type: FieldType | string | number): FieldType {
  if ((type as any) === 0 || type === '0') {
    return FieldType.Text;
  }
  if (typeof type === 'number') {
    return type;
  }
  const parsed = Number(type);
  if (!isNaN(parsed) && parsed !== 0) {
    return parsed as FieldType;
  }
  return FIELD_TYPE_MAP[type] ?? FieldType.Text;
}

/** Get column span from field layout/validation JSON */
export function getFieldColSpan(field: FormField): number {
  try {
    const json = field.layoutJson || field.validationRulesJson;
    const data = json ? JSON.parse(json) : {};
    if (
      field.fieldType === FieldType.Section ||
      field.fieldType === FieldType.Label ||
      field.fieldType === FieldType.PageBreak
    ) {
      return 4;
    }
    return data.colSpan || 4;
  } catch {
    return 4;
  }
}

/** Get field display height from layout/validation JSON */
export function getFieldHeight(field: FormField): string {
  try {
    const json = field.layoutJson || field.validationRulesJson;
    const data = json ? JSON.parse(json) : {};
    const height = data.height || 'auto';
    return HEIGHT_MAP[height] || 'auto';
  } catch {
    return 'auto';
  }
}

/** Check if a field type supports options (dropdown, radio, etc.) */
export function hasOptions(type: FieldType | string | number): boolean {
  if (typeof type === 'string') {
    const num = Number(type);
    if (!isNaN(num)) {
      return OPTION_TYPES.includes(num);
    }
    return OPTION_TYPE_NAMES.includes(type);
  }
  return OPTION_TYPES.includes(type as number);
}

/** Parse options from field's optionsJson */
export function parseFieldOptions(
  optionsJson: string | null | undefined,
): { value: string; label: string }[] {
  if (!optionsJson) return [];
  try {
    return JSON.parse(optionsJson);
  } catch {
    return [];
  }
}

/** Parse address sub-fields from field's optionsJson */
export function getAddressSubFields(
  field: FormField,
): { key: string; label: string; type: string; required: boolean; width: number }[] {
  try {
    const subs = field.optionsJson ? JSON.parse(field.optionsJson) : [];
    if (Array.isArray(subs) && subs.length > 0 && subs[0].key) {
      return subs;
    }
  } catch {}
  return [
    { key: 'street', label: 'Đường / Số nhà', type: 'text', required: true, width: 100 },
    { key: 'ward', label: 'Phường/Xã', type: 'text', required: false, width: 50 },
    { key: 'district', label: 'Quận/Huyện', type: 'text', required: false, width: 50 },
    { key: 'province', label: 'Tỉnh/Thành phố', type: 'text', required: true, width: 50 },
    { key: 'country', label: 'Quốc gia', type: 'text', required: false, width: 50 },
  ];
}

/** Get slider config from field's optionsJson */
export function getSliderConfig(field: FormField): {
  min: number;
  max: number;
  step: number;
  unit: string;
} {
  try {
    const config = field.optionsJson ? JSON.parse(field.optionsJson) : {};
    return {
      min: config.min ?? 0,
      max: config.max ?? 100,
      step: config.step ?? 1,
      unit: config.unit || '',
    };
  } catch {
    return { min: 0, max: 100, step: 1, unit: '' };
  }
}

/** Get repeater config from field's optionsJson */
export function getRepeaterConfig(field: FormField): {
  minRows: number;
  maxRows: number;
  fields: { key: string; label: string; type: string }[];
} {
  try {
    const config = field.optionsJson ? JSON.parse(field.optionsJson) : {};
    return {
      minRows: config.minRows ?? 1,
      maxRows: config.maxRows ?? 10,
      fields: config.fields || [],
    };
  } catch {
    return { minRows: 1, maxRows: 10, fields: [] };
  }
}

/** Get field type label for display */
export function getFieldTypeLabel(type: FieldType): string {
  return FieldTypeLabels[type] || 'Trường mới';
}

/** Default Vietnamese address sub-fields */
export const DEFAULT_ADDRESS_FIELDS = [
  { key: 'street', label: 'Đường / Số nhà', type: 'text', required: true, width: 100 },
  { key: 'ward', label: 'Phường/Xã', type: 'text', required: false, width: 50 },
  { key: 'district', label: 'Quận/Huyện', type: 'text', required: false, width: 50 },
  { key: 'province', label: 'Tỉnh/Thành phố', type: 'text', required: true, width: 50 },
  { key: 'country', label: 'Quốc gia', type: 'text', required: false, width: 50 },
];
