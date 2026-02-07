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
    defaultValue?: string;
    helpText?: string;
    conditionalLogicJson?: string;
    conceptId?: string;  // Linked medical concept ID
}

export interface FormResponse {
    id: string;
    formTemplateId: string;
    formTemplateName: string;
    patientId?: string;
    patientName?: string;
    cycleId?: string;
    status: ResponseStatus;
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

export interface ReportSummary {
    totalResponses: number;
    fieldValueCounts: { [key: string]: number };
    fieldValueAverages: { [key: string]: number };
}

export interface FieldOption {
    value: string;
    label: string;
}

export interface ValidationRule {
    type: 'required' | 'minLength' | 'maxLength' | 'min' | 'max' | 'pattern' | 'email';
    value?: any;
    message?: string;
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
    Label = 15
}

export enum ResponseStatus {
    Draft = 1,
    Submitted = 2,
    Reviewed = 3,
    Approved = 4,
    Rejected = 5
}

export enum ReportType {
    Table = 1,
    BarChart = 2,
    LineChart = 3,
    PieChart = 4,
    Summary = 5
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
    [FieldType.Label]: 'Nhãn hiển thị'
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
    defaultValue?: string;
    helpText?: string;
    conditionalLogicJson?: string;
}

export interface SubmitResponseRequest {
    formTemplateId: string;
    submittedByUserId: string;
    patientId?: string;
    cycleId?: string;
    fieldValues: FieldValueRequest[];
}

export interface FieldValueRequest {
    formFieldId: string;
    textValue?: string;
    numericValue?: number;
    dateValue?: Date;
    booleanValue?: boolean;
    jsonValue?: string;
}

@Injectable({
    providedIn: 'root'
})
export class FormsService {
    private readonly http = inject(HttpClient);
    private readonly baseUrl = `${environment.apiUrl}/forms`;

    // Categories
    getCategories(activeOnly = true): Observable<FormCategory[]> {
        return this.http.get<FormCategory[]>(`${this.baseUrl}/categories`, {
            params: { activeOnly: activeOnly.toString() }
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
    getTemplates(categoryId?: string, publishedOnly?: boolean, includeFields?: boolean): Observable<FormTemplate[]> {
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

    updateTemplate(id: string, request: { name: string; description?: string; categoryId?: string }): Observable<FormTemplate> {
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
        return this.http.post<void>(`${this.baseUrl}/templates/${templateId}/fields/reorder`, { fieldIds });
    }

    // Responses
    getResponses(
        templateId?: string,
        patientId?: string,
        from?: Date,
        to?: Date,
        page = 1,
        pageSize = 20
    ): Observable<{ items: FormResponse[]; total: number }> {
        let params = new HttpParams()
            .set('page', page.toString())
            .set('pageSize', pageSize.toString());

        if (templateId) params = params.set('templateId', templateId);
        if (patientId) params = params.set('patientId', patientId);
        if (from) params = params.set('from', from.toISOString());
        if (to) params = params.set('to', to.toISOString());

        return this.http.get<{ items: FormResponse[]; total: number }>(`${this.baseUrl}/responses`, { params });
    }

    getResponseById(id: string): Observable<FormResponse> {
        return this.http.get<FormResponse>(`${this.baseUrl}/responses/${id}`);
    }

    submitResponse(request: SubmitResponseRequest): Observable<FormResponse> {
        return this.http.post<FormResponse>(`${this.baseUrl}/responses`, request);
    }

    updateResponseStatus(id: string, newStatus: ResponseStatus, notes?: string): Observable<FormResponse> {
        return this.http.put<FormResponse>(`${this.baseUrl}/responses/${id}/status`, { newStatus, notes });
    }

    deleteResponse(id: string): Observable<void> {
        return this.http.delete<void>(`${this.baseUrl}/responses/${id}`);
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
        createdByUserId: string;
    }): Observable<ReportTemplate> {
        return this.http.post<ReportTemplate>(`${this.baseUrl}/reports`, request);
    }

    updateReportTemplate(id: string, request: {
        name: string;
        description?: string;
        reportType: ReportType;
        configurationJson: string;
    }): Observable<ReportTemplate> {
        return this.http.put<ReportTemplate>(`${this.baseUrl}/reports/${id}`, request);
    }

    publishReportTemplate(id: string): Observable<ReportTemplate> {
        return this.http.post<ReportTemplate>(`${this.baseUrl}/reports/${id}/publish`, {});
    }

    deleteReportTemplate(id: string): Observable<void> {
        return this.http.delete<void>(`${this.baseUrl}/reports/${id}`);
    }

    generateReport(reportTemplateId: string, from?: Date, to?: Date, patientId?: string): Observable<ReportData> {
        let params = new HttpParams();
        if (from) params = params.set('from', from.toISOString());
        if (to) params = params.set('to', to.toISOString());
        if (patientId) params = params.set('patientId', patientId);

        return this.http.get<ReportData>(`${this.baseUrl}/reports/${reportTemplateId}/generate`, { params });
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
            return JSON.parse(rulesJson);
        } catch {
            return [];
        }
    }
}
