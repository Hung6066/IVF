import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ComplianceHealthDashboard,
  DataSubjectRequest,
  CreateDsrRequest,
  DsrDashboard,
  ComplianceScheduleTask,
  CreateScheduleRequest,
  ScheduleDashboard,
  BreachNotification,
  ComplianceTraining,
  AssignTrainingRequest,
  AssetInventory,
  CreateAssetRequest,
  AiModelVersion,
  AiBiasTestResult,
  AiPerformanceDashboard,
  ProcessingActivity,
  SecurityTrend,
  AuditReadiness,
  PagedResult,
} from '../models/compliance.model';

@Injectable({ providedIn: 'root' })
export class ComplianceService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  // ─── Compliance Monitoring / Health Dashboard ───

  getHealthDashboard(): Observable<ComplianceHealthDashboard> {
    return this.http.get<ComplianceHealthDashboard>(`${this.baseUrl}/compliance/monitoring/health`);
  }

  getSecurityTrends(months?: number): Observable<SecurityTrend[]> {
    let params = new HttpParams();
    if (months) params = params.set('months', months);
    return this.http.get<SecurityTrend[]>(`${this.baseUrl}/compliance/monitoring/security-trends`, {
      params,
    });
  }

  getAiPerformance(): Observable<AiPerformanceDashboard> {
    return this.http.get<AiPerformanceDashboard>(
      `${this.baseUrl}/compliance/monitoring/ai-performance`,
    );
  }

  getAuditReadiness(): Observable<AuditReadiness> {
    return this.http.get<AuditReadiness>(`${this.baseUrl}/compliance/monitoring/audit-readiness`);
  }

  // ─── Data Subject Requests (GDPR) ───

  getDsrList(filters?: {
    status?: string;
    requestType?: string;
    overdue?: boolean;
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<DataSubjectRequest>> {
    let params = new HttpParams()
      .set('page', filters?.page ?? 1)
      .set('pageSize', filters?.pageSize ?? 20);
    if (filters?.status) params = params.set('status', filters.status);
    if (filters?.requestType) params = params.set('requestType', filters.requestType);
    if (filters?.overdue !== undefined) params = params.set('overdue', filters.overdue);
    return this.http.get<PagedResult<DataSubjectRequest>>(`${this.baseUrl}/compliance/dsr`, {
      params,
    });
  }

  getDsr(id: string): Observable<DataSubjectRequest> {
    return this.http.get<DataSubjectRequest>(`${this.baseUrl}/compliance/dsr/${id}`);
  }

  createDsr(request: CreateDsrRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/compliance/dsr`, request);
  }

  verifyDsrIdentity(id: string, method: string, verifiedBy: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/compliance/dsr/${id}/verify`, { method, verifiedBy });
  }

  assignDsr(id: string, assignedTo: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/compliance/dsr/${id}/assign`, { assignedTo });
  }

  extendDsrDeadline(id: string, additionalDays: number, reason: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/compliance/dsr/${id}/extend`, {
      additionalDays,
      reason,
    });
  }

  completeDsr(id: string, responseSummary: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/compliance/dsr/${id}/complete`, { responseSummary });
  }

  rejectDsr(id: string, rejectionReason: string, legalBasis: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/compliance/dsr/${id}/reject`, {
      rejectionReason,
      legalBasis,
    });
  }

  escalateDsr(id: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/compliance/dsr/${id}/escalate`, {});
  }

  notifyDsrSubject(id: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/compliance/dsr/${id}/notify`, {});
  }

  addDsrNote(id: string, note: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/compliance/dsr/${id}/note`, { note });
  }

  getDsrDashboard(): Observable<DsrDashboard> {
    return this.http.get<DsrDashboard>(`${this.baseUrl}/compliance/dsr/dashboard`);
  }

  // ─── Compliance Schedule ───

  getScheduleList(filters?: {
    framework?: string;
    frequency?: string;
    category?: string;
    status?: string;
    overdue?: boolean;
    upcoming?: boolean;
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<ComplianceScheduleTask>> {
    let params = new HttpParams()
      .set('page', filters?.page ?? 1)
      .set('pageSize', filters?.pageSize ?? 50);
    if (filters?.framework) params = params.set('framework', filters.framework);
    if (filters?.frequency) params = params.set('frequency', filters.frequency);
    if (filters?.category) params = params.set('category', filters.category);
    if (filters?.status) params = params.set('status', filters.status);
    if (filters?.overdue !== undefined) params = params.set('overdue', filters.overdue);
    if (filters?.upcoming !== undefined) params = params.set('upcoming', filters.upcoming);
    return this.http.get<PagedResult<ComplianceScheduleTask>>(
      `${this.baseUrl}/compliance/schedule`,
      { params },
    );
  }

  getScheduleTask(id: string): Observable<ComplianceScheduleTask> {
    return this.http.get<ComplianceScheduleTask>(`${this.baseUrl}/compliance/schedule/${id}`);
  }

  createScheduleTask(request: CreateScheduleRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/compliance/schedule`, request);
  }

  completeScheduleTask(id: string, completedBy: string, notes: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/compliance/schedule/${id}/complete`, {
      completedBy,
      notes,
    });
  }

  assignScheduleTask(id: string, userId: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/compliance/schedule/${id}/assign`, { userId });
  }

  pauseScheduleTask(id: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/compliance/schedule/${id}/pause`, {});
  }

  resumeScheduleTask(id: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/compliance/schedule/${id}/resume`, {});
  }

  seedScheduleDefaults(): Observable<any> {
    return this.http.post(`${this.baseUrl}/compliance/schedule/seed-defaults`, {});
  }

  getScheduleDashboard(): Observable<ScheduleDashboard> {
    return this.http.get<ScheduleDashboard>(`${this.baseUrl}/compliance/schedule/dashboard`);
  }

  // ─── Breach Notifications ───

  getBreaches(): Observable<BreachNotification[]> {
    return this.http.get<BreachNotification[]>(`${this.baseUrl}/compliance/breaches`);
  }

  getBreach(id: string): Observable<BreachNotification> {
    return this.http.get<BreachNotification>(`${this.baseUrl}/compliance/breaches/${id}`);
  }

  createBreach(request: any): Observable<BreachNotification> {
    return this.http.post<BreachNotification>(`${this.baseUrl}/compliance/breaches`, request);
  }

  // ─── Compliance Training ───

  getTrainings(filters?: {
    userId?: string;
    type?: string;
    completed?: boolean;
    overdue?: boolean;
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<ComplianceTraining>> {
    let params = new HttpParams()
      .set('page', filters?.page ?? 1)
      .set('pageSize', filters?.pageSize ?? 20);
    if (filters?.userId) params = params.set('userId', filters.userId);
    if (filters?.type) params = params.set('type', filters.type);
    if (filters?.completed !== undefined) params = params.set('completed', filters.completed);
    if (filters?.overdue !== undefined) params = params.set('overdue', filters.overdue);
    return this.http.get<PagedResult<ComplianceTraining>>(`${this.baseUrl}/compliance/training`, {
      params,
    });
  }

  assignTraining(request: AssignTrainingRequest): Observable<any> {
    return this.http.post(`${this.baseUrl}/compliance/training`, request);
  }

  completeTraining(id: string, score: number, evidence?: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/compliance/training/${id}/complete`, {
      score,
      evidence,
    });
  }

  // ─── Asset Inventory ───

  getAssets(filters?: {
    type?: string;
    classification?: string;
    owner?: string;
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<AssetInventory>> {
    let params = new HttpParams()
      .set('page', filters?.page ?? 1)
      .set('pageSize', filters?.pageSize ?? 20);
    if (filters?.type) params = params.set('type', filters.type);
    if (filters?.classification) params = params.set('classification', filters.classification);
    if (filters?.owner) params = params.set('owner', filters.owner);
    return this.http.get<PagedResult<AssetInventory>>(`${this.baseUrl}/assets`, { params });
  }

  getAsset(id: string): Observable<AssetInventory> {
    return this.http.get<AssetInventory>(`${this.baseUrl}/assets/${id}`);
  }

  createAsset(request: CreateAssetRequest): Observable<any> {
    return this.http.post(`${this.baseUrl}/assets`, request);
  }

  updateAsset(id: string, request: CreateAssetRequest): Observable<any> {
    return this.http.put(`${this.baseUrl}/assets/${id}`, request);
  }

  deleteAsset(id: string): Observable<any> {
    return this.http.delete(`${this.baseUrl}/assets/${id}`);
  }

  // ─── AI Governance ───

  getAiModels(filters?: {
    system?: string;
    status?: string;
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<AiModelVersion>> {
    let params = new HttpParams()
      .set('page', filters?.page ?? 1)
      .set('pageSize', filters?.pageSize ?? 20);
    if (filters?.system) params = params.set('system', filters.system);
    if (filters?.status) params = params.set('status', filters.status);
    return this.http.get<PagedResult<AiModelVersion>>(`${this.baseUrl}/ai/models`, { params });
  }

  getAiModel(id: string): Observable<AiModelVersion> {
    return this.http.get<AiModelVersion>(`${this.baseUrl}/ai/models/${id}`);
  }

  getBiasTests(filters?: {
    system?: string;
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<AiBiasTestResult>> {
    let params = new HttpParams()
      .set('page', filters?.page ?? 1)
      .set('pageSize', filters?.pageSize ?? 20);
    if (filters?.system) params = params.set('system', filters.system);
    return this.http.get<PagedResult<AiBiasTestResult>>(`${this.baseUrl}/ai/bias-tests`, {
      params,
    });
  }

  // ─── Processing Activities (ROPA) ───

  getProcessingActivities(page = 1, pageSize = 20): Observable<PagedResult<ProcessingActivity>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<ProcessingActivity>>(`${this.baseUrl}/processing-activities`, {
      params,
    });
  }

  createProcessingActivity(request: any): Observable<any> {
    return this.http.post(`${this.baseUrl}/processing-activities`, request);
  }

  // ─── Compliance Scoring ───

  getComplianceDashboard(): Observable<any> {
    return this.http.get(`${this.baseUrl}/compliance/dashboard`);
  }
}
