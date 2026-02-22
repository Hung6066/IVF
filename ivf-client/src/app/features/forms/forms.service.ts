import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

// Models
export interface FormCategory {
  id: string;
  name: string;
  description?: string;
  iconName?: string;
  displayOrder: number;
  isActive: boolean;
  templateCount?: number;
}

export interface FormTemplate {
  id: string;
  categoryId: string;
  categoryName: string;
  name: string;
  description?: string;
  version: string;
  isPublished: boolean;
  createdAt: Date;
  fields?: FormField[];
}

export interface FormField {
  id: string;
  fieldKey: string;
  label: string;
  placeholder?: string;
  fieldType: FieldType;
  displayOrder: number;
  isRequired: boolean;
  optionsJson?: string;
  validationRulesJson?: string;
  layoutJson?: string;
  defaultValue?: string;
  helpText?: string;
  conditionalLogicJson?: string;
  conceptId?: string; // Linked medical concept ID
}

export interface FormResponse {
  id: string;
  formTemplateId: string;
  formTemplateName: string;
  patientId?: string;
  patientName?: string;
  cycleId?: string;
  status: ResponseStatus;
  notes?: string;
  createdAt: Date;
  submittedAt?: Date;
  fieldValues?: FormFieldValue[];
}

export interface FormFieldValue {
  id?: string;
  formFieldId: string;
  fieldKey?: string;
  fieldLabel?: string;
  textValue?: string;
  numericValue?: number;
  dateValue?: Date;
  booleanValue?: boolean;
  jsonValue?: string;
  details?: FormFieldValueDetail[];
}

export interface FormFieldValueDetail {
  value: string;
  label?: string;
  conceptId?: string;
}

export interface ReportTemplate {
  id: string;
  formTemplateId: string;
  formTemplateName: string;
  name: string;
  description?: string;
  reportType: ReportType;
  configurationJson: string;
  isPublished: boolean;
  createdAt: Date;
}

export interface ReportData {
  template: ReportTemplate;
  data: { [key: string]: any }[];
  summary?: ReportSummary;
}

// ===== Report Configuration =====
export interface ReportConfiguration {
  columns: ReportColumnConfig[];
  page: ReportPageConfig;
  header?: ReportHeaderConfig;
  footer?: ReportFooterConfig;
  filters: ReportFilterConfig[];
  groupBy?: string;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
  chart?: ReportChartConfig;
  // Phase 2
  conditionalFormats?: ConditionalFormatRule[];
  calculatedFields?: CalculatedFieldConfig[];
  showFooterAggregations?: boolean;
  groupSummary?: GroupSummaryConfig;
}

export interface ReportColumnConfig {
  fieldKey: string;
  label: string;
  width?: number;
  visible: boolean;
  format?: 'text' | 'number' | 'date' | 'datetime' | 'currency' | 'percentage';
  aggregation?: 'none' | 'count' | 'sum' | 'avg' | 'min' | 'max';
}

export interface ReportPageConfig {
  size: 'A4' | 'A5' | 'Letter';
  orientation: 'portrait' | 'landscape';
  margins: { top: number; right: number; bottom: number; left: number };
}

export interface ReportHeaderConfig {
  title?: string;
  subtitle?: string;
  showLogo: boolean;
  showDate: boolean;
}

export interface ReportFooterConfig {
  text?: string;
  showPageNumber: boolean;
}

export interface ReportFilterConfig {
  fieldKey: string;
  operator: 'eq' | 'neq' | 'gt' | 'lt' | 'gte' | 'lte' | 'contains';
  value: string;
}

export interface ReportChartConfig {
  categoryField: string;
  valueField?: string;
  aggregation: 'count' | 'sum' | 'avg';
  showLegend: boolean;
  showValues: boolean;
  maxItems: number;
}

// ===== Phase 2: Conditional Formatting, Calculated Fields, Grouping =====
export interface ConditionalFormatRule {
  id: string;
  name?: string;
  fieldKey: string;
  operator: 'eq' | 'neq' | 'gt' | 'lt' | 'gte' | 'lte' | 'contains' | 'empty' | 'notEmpty';
  value: string;
  applyTo: 'cell' | 'row';
  style: ConditionalFormatStyle;
}

export interface ConditionalFormatStyle {
  backgroundColor?: string;
  textColor?: string;
  fontWeight?: 'normal' | 'bold';
  fontStyle?: 'normal' | 'italic';
}

export interface CalculatedFieldConfig {
  fieldKey: string;
  label: string;
  expression: string; // e.g. "[field_1] + [field_2]", "COUNT()", "IF([status]='Draft','Nháp','Khác')"
  format?: 'text' | 'number' | 'date' | 'currency' | 'percentage';
}

export interface GroupSummaryConfig {
  showGroupHeaders: boolean;
  showGroupFooters: boolean;
  aggregations: GroupAggregation[];
}

export interface GroupAggregation {
  fieldKey: string;
  type: 'count' | 'sum' | 'avg' | 'min' | 'max';
  label?: string;
}

// ===== Phase 2: Visual Band Designer =====

export type BandType =
  | 'pageHeader'
  | 'groupHeader'
  | 'detail'
  | 'groupFooter'
  | 'pageFooter'
  | 'reportHeader'
  | 'reportFooter';

export interface ReportDesign {
  bands: ReportBand[];
  parameters: ReportParameter[];
  dataSources: ReportDataSource[];
  pageSettings: ReportPageConfig;
  styles: ReportStyleDef[];
  subReports?: SubReportConfig[];
  tabs?: ReportTab[];
  crossTab?: CrossTabConfig;
}

export interface ReportBand {
  id: string;
  type: BandType;
  height: number;
  visible: boolean;
  groupField?: string;
  controls: ReportControl[];
  keepTogether?: boolean;
  repeatOnEveryPage?: boolean;
  backgroundColor?: string;
}

export type ControlType =
  | 'label'
  | 'field'
  | 'image'
  | 'shape'
  | 'line'
  | 'richText'
  | 'barcode'
  | 'chart'
  | 'table'
  | 'checkbox'
  | 'pageNumber'
  | 'totalPages'
  | 'currentDate'
  | 'expression'
  | 'signatureZone';

export interface ReportControl {
  id: string;
  type: ControlType;
  x: number;
  y: number;
  width: number;
  height: number;
  text?: string;
  expression?: string; // e.g. [Data.fieldKey], Upper([Data.name]), Iif([Data.score]>80,'Pass','Fail')
  format?: string;
  style?: ReportControlStyle;
  dataField?: string;
  imageUrl?: string;
  shapeType?: 'rectangle' | 'ellipse' | 'triangle';
  barcodeType?: 'qr' | 'code128' | 'ean13';
  barcodeValue?: string;
  visible?: boolean;
  canGrow?: boolean;
  signatureRole?: string; // e.g. 'technician', 'department_head', 'doctor'
}

export interface ReportControlStyle {
  fontFamily?: string;
  fontSize?: number;
  fontWeight?: 'normal' | 'bold';
  fontStyle?: 'normal' | 'italic';
  textAlign?: 'left' | 'center' | 'right';
  verticalAlign?: 'top' | 'middle' | 'bottom';
  color?: string;
  backgroundColor?: string;
  borderColor?: string;
  borderWidth?: number;
  borderStyle?: 'none' | 'solid' | 'dashed' | 'dotted';
  padding?: number;
}

export interface ReportStyleDef {
  name: string;
  style: ReportControlStyle;
}

// ===== Phase 3: Advanced Features =====

export interface ReportParameter {
  name: string;
  label: string;
  type: 'text' | 'number' | 'date' | 'dropdown';
  defaultValue?: string;
  options?: { value: string; label: string }[];
  required?: boolean;
}

export interface ReportDataSource {
  name: string;
  type: 'formTemplate' | 'custom';
  formTemplateId?: string;
  fields: { key: string; label: string; type: string }[];
}

export interface SubReportConfig {
  id: string;
  name: string;
  reportDesignJson: string;
  bandId: string;
  parameterBindings: { paramName: string; expression: string }[];
}

export interface ReportTab {
  id: string;
  name: string;
  designJson: string;
}

export interface CrossTabConfig {
  rowFields: string[];
  columnFields: string[];
  dataField: string;
  aggregation: 'count' | 'sum' | 'avg' | 'min' | 'max';
  showRowTotals: boolean;
  showColumnTotals: boolean;
  showGrandTotal: boolean;
}

export interface ReportTemplateLibraryItem {
  id: string;
  name: string;
  description: string;
  category: string;
  previewUrl?: string;
  designJson: string;
}

export interface ReportSummary {
  totalResponses: number;
  fieldValueCounts: { [key: string]: number };
  fieldValueAverages: { [key: string]: number };
}

export interface FieldOption {
  value: string;
  label: string;
  conceptId?: string;
  conceptCode?: string;
  conceptDisplay?: string;
}

export interface ValidationRule {
  type: 'required' | 'minLength' | 'maxLength' | 'min' | 'max' | 'pattern' | 'email';
  value?: any;
  message?: string;
}

export interface ConditionalLogic {
  action: 'show' | 'hide';
  logic: 'AND' | 'OR';
  conditions: Condition[];
}

export interface Condition {
  fieldId: string;
  operator: 'eq' | 'neq' | 'gt' | 'lt' | 'contains';
  value: any;
}

export enum FieldType {
  Text = 1,
  TextArea = 2,
  Number = 3,
  Decimal = 4,
  Date = 5,
  DateTime = 6,
  Time = 7,
  Dropdown = 8,
  MultiSelect = 9,
  Radio = 10,
  Checkbox = 11,
  FileUpload = 12,
  Rating = 13,
  Section = 14,
  Label = 15,
  Tags = 16,
  PageBreak = 17,
  Address = 18,
  Hidden = 19,
  Slider = 20,
  Calculated = 21,
  RichText = 22,
  Signature = 23,
  Lookup = 24,
  Repeater = 25,
}

export enum ResponseStatus {
  Draft = 1,
  Submitted = 2,
  Reviewed = 3,
  Approved = 4,
  Rejected = 5,
}

export enum ReportType {
  Table = 1,
  BarChart = 2,
  LineChart = 3,
  PieChart = 4,
  Summary = 5,
}

export const FieldTypeLabels: { [key: number]: string } = {
  [FieldType.Text]: 'Văn bản ngắn',
  [FieldType.TextArea]: 'Văn bản dài',
  [FieldType.Number]: 'Số nguyên',
  [FieldType.Decimal]: 'Số thập phân',
  [FieldType.Date]: 'Ngày',
  [FieldType.DateTime]: 'Ngày giờ',
  [FieldType.Time]: 'Giờ',
  [FieldType.Dropdown]: 'Danh sách chọn',
  [FieldType.MultiSelect]: 'Chọn nhiều',
  [FieldType.Radio]: 'Nút radio',
  [FieldType.Checkbox]: 'Checkbox',
  [FieldType.FileUpload]: 'Tải file',
  [FieldType.Rating]: 'Đánh giá sao',
  [FieldType.Section]: 'Phân đoạn',
  [FieldType.Label]: 'Nhãn hiển thị',
  [FieldType.Tags]: 'Tags',
  [FieldType.PageBreak]: 'Ngắt trang',
  [FieldType.Address]: 'Địa chỉ/Phức hợp',
  [FieldType.Hidden]: 'Ẩn',
  [FieldType.Slider]: 'Thanh trượt',
  [FieldType.Calculated]: 'Tính toán',
  [FieldType.RichText]: 'Văn bản phong phú',
  [FieldType.Signature]: 'Chữ ký',
  [FieldType.Lookup]: 'Tra cứu',
  [FieldType.Repeater]: 'Lặp nhóm',
};

// Request DTOs
export interface CreateCategoryRequest {
  name: string;
  description?: string;
  iconName?: string;
  displayOrder?: number;
}

export interface CreateTemplateRequest {
  categoryId: string;
  name: string;
  description?: string;
  createdByUserId: string;
  fields?: CreateFieldRequest[];
}

export interface CreateFieldRequest {
  fieldKey: string;
  label: string;
  fieldType: FieldType;
  displayOrder: number;
  isRequired?: boolean;
  placeholder?: string;
  optionsJson?: string;
  validationRulesJson?: string;
  layoutJson?: string;
  defaultValue?: string;
  helpText?: string;
  conditionalLogicJson?: string;
}

export interface SubmitResponseRequest {
  formTemplateId: string;
  submittedByUserId?: string | null; // Made optional/nullable
  patientId?: string;
  cycleId?: string;
  fieldValues: FieldValueRequest[];
  isDraft?: boolean;
}

export interface FileUploadResult {
  filePath: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  url: string;
}

export interface UpdateFieldRequest {
  label?: string;
  displayOrder?: number;
  isRequired?: boolean;
  placeholder?: string;
  optionsJson?: string;
  validationRulesJson?: string;
  layoutJson?: string;
  conditionalLogicJson?: string;
}

export interface FieldValueRequest {
  formFieldId: string;
  textValue?: string;
  numericValue?: number;
  dateValue?: Date;
  booleanValue?: boolean;
  jsonValue?: string;
  details?: FieldValueDetailRequest[];
}

export interface FieldValueDetailRequest {
  value: string;
  label?: string;
  conceptId?: string;
}

export interface LinkedDataValue {
  fieldId: string;
  fieldLabel: string;
  conceptId: string;
  conceptDisplay: string;
  textValue?: string;
  numericValue?: number;
  dateValue?: string;
  booleanValue?: boolean;
  jsonValue?: string;
  displayValue: string;
  sourceFormName: string;
  capturedAt: string;
  flowType: number; // 1=AutoFill, 2=Suggest, 3=Reference, 4=Copy
}

export interface LinkedFieldSource {
  id: string;
  targetFieldId: string;
  targetFieldLabel: string;
  sourceTemplateId: string;
  sourceTemplateName: string;
  sourceFieldId: string;
  sourceFieldLabel: string;
  flowType: number;
  priority: number;
  isActive: boolean;
  description?: string;
  createdAt: string;
}

export interface CreateLinkedFieldSourceRequest {
  targetFieldId: string;
  sourceTemplateId: string;
  sourceFieldId: string;
  flowType?: number;
  priority?: number;
  description?: string;
}

export interface UpdateLinkedFieldSourceRequest {
  sourceTemplateId?: string;
  sourceFieldId?: string;
  flowType?: number;
  priority?: number;
  description?: string;
}

@Injectable({
  providedIn: 'root',
})
export class FormsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/forms`;

  // Categories
  getCategories(activeOnly = true): Observable<FormCategory[]> {
    return this.http.get<FormCategory[]>(`${this.baseUrl}/categories`, {
      params: { activeOnly: activeOnly.toString() },
    });
  }

  getCategoryById(id: string): Observable<FormCategory> {
    return this.http.get<FormCategory>(`${this.baseUrl}/categories/${id}`);
  }

  createCategory(request: CreateCategoryRequest): Observable<FormCategory> {
    return this.http.post<FormCategory>(`${this.baseUrl}/categories`, request);
  }

  updateCategory(id: string, request: CreateCategoryRequest): Observable<FormCategory> {
    return this.http.put<FormCategory>(`${this.baseUrl}/categories/${id}`, request);
  }

  deleteCategory(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/categories/${id}`);
  }

  // Templates
  getTemplates(
    categoryId?: string,
    publishedOnly?: boolean,
    includeFields?: boolean,
  ): Observable<FormTemplate[]> {
    let params = new HttpParams();
    if (categoryId) params = params.set('categoryId', categoryId);
    if (publishedOnly !== undefined) params = params.set('publishedOnly', publishedOnly.toString());
    if (includeFields !== undefined) params = params.set('includeFields', includeFields.toString());

    return this.http.get<FormTemplate[]>(`${this.baseUrl}/templates`, { params });
  }

  getTemplateById(id: string): Observable<FormTemplate> {
    return this.http.get<FormTemplate>(`${this.baseUrl}/templates/${id}`);
  }

  createTemplate(request: CreateTemplateRequest): Observable<FormTemplate> {
    return this.http.post<FormTemplate>(`${this.baseUrl}/templates`, request);
  }

  updateTemplate(
    id: string,
    request: { name: string; description?: string; categoryId?: string },
  ): Observable<FormTemplate> {
    return this.http.put<FormTemplate>(`${this.baseUrl}/templates/${id}`, request);
  }

  publishTemplate(id: string): Observable<FormTemplate> {
    return this.http.post<FormTemplate>(`${this.baseUrl}/templates/${id}/publish`, {});
  }

  unpublishTemplate(id: string): Observable<FormTemplate> {
    return this.http.post<FormTemplate>(`${this.baseUrl}/templates/${id}/unpublish`, {});
  }

  deleteTemplate(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/templates/${id}`);
  }

  duplicateTemplate(id: string, newName?: string): Observable<FormTemplate> {
    return this.http.post<FormTemplate>(`${this.baseUrl}/templates/${id}/duplicate`, { newName });
  }

  // Fields
  getFieldsByTemplate(templateId: string): Observable<FormField[]> {
    return this.http.get<FormField[]>(`${this.baseUrl}/templates/${templateId}/fields`);
  }

  addField(templateId: string, request: CreateFieldRequest): Observable<FormField> {
    return this.http.post<FormField>(`${this.baseUrl}/templates/${templateId}/fields`, request);
  }

  updateField(id: string, request: Partial<FormField>): Observable<FormField> {
    return this.http.put<FormField>(`${this.baseUrl}/fields/${id}`, request);
  }

  deleteField(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/fields/${id}`);
  }

  reorderFields(templateId: string, fieldIds: string[]): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/templates/${templateId}/fields/reorder`, {
      fieldIds,
    });
  }

  // Responses
  getResponses(
    templateId?: string,
    patientId?: string,
    from?: Date,
    to?: Date,
    page = 1,
    pageSize = 20,
    status?: number,
  ): Observable<{ items: FormResponse[]; total: number }> {
    let params = new HttpParams().set('page', page.toString()).set('pageSize', pageSize.toString());

    if (templateId) params = params.set('templateId', templateId);
    if (patientId) params = params.set('patientId', patientId);
    if (from) params = params.set('from', from.toISOString());
    if (to) params = params.set('to', to.toISOString());
    if (status) params = params.set('status', status.toString());

    return this.http.get<{ items: FormResponse[]; total: number }>(`${this.baseUrl}/responses`, {
      params,
    });
  }

  getResponseById(id: string): Observable<FormResponse> {
    return this.http.get<FormResponse>(`${this.baseUrl}/responses/${id}`);
  }

  submitResponse(request: SubmitResponseRequest): Observable<FormResponse> {
    return this.http.post<FormResponse>(`${this.baseUrl}/responses`, request);
  }

  updateResponse(responseId: string, request: SubmitResponseRequest): Observable<FormResponse> {
    return this.http.put<FormResponse>(`${this.baseUrl}/responses/${responseId}`, request);
  }

  updateResponseStatus(
    id: string,
    newStatus: ResponseStatus,
    notes?: string,
  ): Observable<FormResponse> {
    return this.http.put<FormResponse>(`${this.baseUrl}/responses/${id}/status`, {
      newStatus,
      notes,
    });
  }

  deleteResponse(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/responses/${id}`);
  }

  exportResponsePdf(id: string, sign = false): Observable<Blob> {
    const params = sign ? '?sign=true' : '';
    return this.http.get(`${this.baseUrl}/responses/${id}/export-pdf${params}`, {
      responseType: 'blob',
    });
  }

  // ── Multi-Signature Endpoints ──

  /** Sign a form response for a specific role */
  signResponse(responseId: string, signatureRole: string, notes?: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/responses/${responseId}/sign`, {
      signatureRole,
      notes,
    });
  }

  /** Get signing status (who has signed, pending roles) */
  getSigningStatus(responseId: string): Observable<SigningStatus> {
    return this.http.get<SigningStatus>(`${this.baseUrl}/responses/${responseId}/signing-status`);
  }

  /** Revoke a signature */
  revokeSignature(responseId: string, signatureId: string): Observable<any> {
    return this.http.delete(`${this.baseUrl}/responses/${responseId}/sign/${signatureId}`);
  }

  // Linked Data (cross-form concept pre-fill)
  getLinkedData(
    templateId: string,
    patientId: string,
    cycleId?: string,
  ): Observable<LinkedDataValue[]> {
    let params = new HttpParams().set('patientId', patientId);
    if (cycleId) params = params.set('cycleId', cycleId);
    return this.http.get<LinkedDataValue[]>(`${this.baseUrl}/linked-data/${templateId}`, {
      params,
    });
  }

  // Linked Field Sources (explicit field-to-field link configuration)
  getLinkedFieldSources(templateId: string): Observable<LinkedFieldSource[]> {
    return this.http.get<LinkedFieldSource[]>(
      `${this.baseUrl}/templates/${templateId}/linked-sources`,
    );
  }

  createLinkedFieldSource(request: CreateLinkedFieldSourceRequest): Observable<LinkedFieldSource> {
    return this.http.post<LinkedFieldSource>(`${this.baseUrl}/linked-sources`, request);
  }

  updateLinkedFieldSource(
    id: string,
    request: UpdateLinkedFieldSourceRequest,
  ): Observable<LinkedFieldSource> {
    return this.http.put<LinkedFieldSource>(`${this.baseUrl}/linked-sources/${id}`, request);
  }

  deleteLinkedFieldSource(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/linked-sources/${id}`);
  }

  // File Upload
  uploadFile(file: File): Observable<FileUploadResult> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.http.post<FileUploadResult>(`${this.baseUrl}/files/upload`, formData);
  }

  getFileUrl(filePath: string): string {
    return `${this.baseUrl}/files/${filePath}`;
  }

  // Reports
  getReportTemplates(formTemplateId: string): Observable<ReportTemplate[]> {
    return this.http.get<ReportTemplate[]>(`${this.baseUrl}/templates/${formTemplateId}/reports`);
  }

  getReportTemplateById(id: string): Observable<ReportTemplate> {
    return this.http.get<ReportTemplate>(`${this.baseUrl}/reports/${id}`);
  }

  createReportTemplate(request: {
    formTemplateId: string;
    name: string;
    description?: string;
    reportType: ReportType;
    configurationJson: string;
  }): Observable<ReportTemplate> {
    return this.http.post<ReportTemplate>(`${this.baseUrl}/reports`, request);
  }

  updateReportTemplate(
    id: string,
    request: {
      name: string;
      description?: string;
      reportType: ReportType;
      configurationJson: string;
    },
  ): Observable<ReportTemplate> {
    return this.http.put<ReportTemplate>(`${this.baseUrl}/reports/${id}`, request);
  }

  publishReportTemplate(id: string): Observable<ReportTemplate> {
    return this.http.post<ReportTemplate>(`${this.baseUrl}/reports/${id}/publish`, {});
  }

  deleteReportTemplate(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/reports/${id}`);
  }

  generateReport(
    reportTemplateId: string,
    from?: Date,
    to?: Date,
    patientId?: string,
  ): Observable<ReportData> {
    let params = new HttpParams();
    if (from) params = params.set('from', from.toISOString());
    if (to) params = params.set('to', to.toISOString());
    if (patientId) params = params.set('patientId', patientId);

    return this.http.get<ReportData>(`${this.baseUrl}/reports/${reportTemplateId}/generate`, {
      params,
    });
  }

  exportReportPdf(
    reportTemplateId: string,
    from?: Date,
    to?: Date,
    patientId?: string,
    sign = false,
  ): Observable<Blob> {
    let params = new HttpParams();
    if (from) params = params.set('from', from.toISOString());
    if (to) params = params.set('to', to.toISOString());
    if (patientId) params = params.set('patientId', patientId);
    if (sign) params = params.set('sign', 'true');

    return this.http.get(`${this.baseUrl}/reports/${reportTemplateId}/export-pdf`, {
      params,
      responseType: 'blob',
    });
  }

  // Helpers
  parseOptions(optionsJson: string | null | undefined): FieldOption[] {
    if (!optionsJson) return [];
    try {
      return JSON.parse(optionsJson);
    } catch {
      return [];
    }
  }

  parseValidationRules(rulesJson: string | null | undefined): ValidationRule[] {
    if (!rulesJson) return [];
    try {
      const parsed = JSON.parse(rulesJson);
      // Support new format: { colSpan, height, rules: [...] }
      if (parsed.rules && Array.isArray(parsed.rules)) {
        return parsed.rules;
      }
      // Support old format: ValidationRule[]
      if (Array.isArray(parsed)) {
        return parsed;
      }
      return [];
    } catch {
      return [];
    }
  }
}

// ── Multi-Signature Types ──

export interface SigningStatus {
  formResponseId: string;
  signatures: DocumentSignatureInfo[];
  requiredRoles: string[];
  pendingRoles: string[];
  isFullySigned: boolean;
}

export interface DocumentSignatureInfo {
  id: string;
  signatureRole: string;
  userId: string;
  signerName: string;
  signedAt: string;
  notes?: string;
}
