import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, Subject } from 'rxjs';
import * as signalR from '@microsoft/signalr';
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
  CreateModelVersionRequest,
  SetMetricsRequest,
  CreateBiasTestRequest,
  ProcessingActivity,
  SecurityTrend,
  AuditReadiness,
  PagedResult,
  EvidenceAccessControl,
  EvidenceTraining,
  EvidenceIncidents,
  EvidenceBackup,
  EvidenceAssets,
  EvidenceSummary,
  EvidenceCategory,
  EvidenceCollectRequest,
  EvidenceCollectResult,
  EvidenceProgress,
  EvidenceLogLine,
  EvidenceFile,
  AuditDashboard,
  AuditScan,
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
    return this.http.post(`${this.baseUrl}/compliance/training/assign`, request);
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
    return this.http.get<PagedResult<AssetInventory>>(`${this.baseUrl}/compliance/assets`, {
      params,
    });
  }

  getAsset(id: string): Observable<AssetInventory> {
    return this.http.get<AssetInventory>(`${this.baseUrl}/compliance/assets/${id}`);
  }

  createAsset(request: CreateAssetRequest): Observable<any> {
    return this.http.post(`${this.baseUrl}/compliance/assets`, request);
  }

  updateAsset(id: string, request: CreateAssetRequest): Observable<any> {
    return this.http.put(`${this.baseUrl}/compliance/assets/${id}`, request);
  }

  deleteAsset(id: string): Observable<any> {
    return this.http.delete(`${this.baseUrl}/compliance/assets/${id}`);
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
    if (filters?.system) params = params.set('aiSystem', filters.system);
    if (filters?.status) params = params.set('status', filters.status);
    return this.http.get<PagedResult<AiModelVersion>>(`${this.baseUrl}/ai/model-versions`, {
      params,
    });
  }

  getAiModel(id: string): Observable<AiModelVersion> {
    return this.http.get<AiModelVersion>(`${this.baseUrl}/ai/model-versions/${id}`);
  }

  createAiModel(request: CreateModelVersionRequest): Observable<AiModelVersion> {
    return this.http.post<AiModelVersion>(`${this.baseUrl}/ai/model-versions`, request);
  }

  deleteAiModel(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/ai/model-versions/${id}`);
  }

  setModelMetrics(id: string, metrics: SetMetricsRequest): Observable<AiModelVersion> {
    return this.http.put<AiModelVersion>(
      `${this.baseUrl}/ai/model-versions/${id}/metrics`,
      metrics,
    );
  }

  getBiasTests(filters?: {
    system?: string;
    page?: number;
    pageSize?: number;
  }): Observable<AiBiasTestResult[]> {
    let params = new HttpParams();
    if (filters?.system) params = params.set('aiSystem', filters.system);
    return this.http.get<AiBiasTestResult[]>(`${this.baseUrl}/ai/bias-tests`, {
      params,
    });
  }

  createBiasTest(request: CreateBiasTestRequest): Observable<AiBiasTestResult> {
    return this.http.post<AiBiasTestResult>(`${this.baseUrl}/ai/bias-tests`, request);
  }

  deleteBiasTest(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/ai/bias-tests/${id}`);
  }

  // ─── Processing Activities (ROPA) ───

  getProcessingActivities(page = 1, pageSize = 20): Observable<ProcessingActivity[]> {
    let params = new HttpParams();
    return this.http.get<ProcessingActivity[]>(`${this.baseUrl}/compliance/ropa`, {
      params,
    });
  }

  createProcessingActivity(request: any): Observable<any> {
    return this.http.post(`${this.baseUrl}/compliance/ropa`, request);
  }

  // ─── Compliance Scoring ───

  getComplianceDashboard(): Observable<any> {
    return this.http.get(`${this.baseUrl}/compliance/dashboard`);
  }

  // ─── Evidence Export ───

  getEvidenceAccessControl(): Observable<EvidenceAccessControl> {
    return this.http.get<EvidenceAccessControl>(
      `${this.baseUrl}/compliance/evidence/access-control`,
    );
  }

  getEvidenceTraining(): Observable<EvidenceTraining> {
    return this.http.get<EvidenceTraining>(`${this.baseUrl}/compliance/evidence/training`);
  }

  getEvidenceIncidents(): Observable<EvidenceIncidents> {
    return this.http.get<EvidenceIncidents>(`${this.baseUrl}/compliance/evidence/incidents`);
  }

  getEvidenceBackup(): Observable<EvidenceBackup> {
    return this.http.get<EvidenceBackup>(`${this.baseUrl}/compliance/evidence/backup`);
  }

  getEvidenceAssets(): Observable<EvidenceAssets> {
    return this.http.get<EvidenceAssets>(`${this.baseUrl}/compliance/evidence/assets`);
  }

  getEvidenceSummary(): Observable<EvidenceSummary> {
    return this.http.get<EvidenceSummary>(`${this.baseUrl}/compliance/evidence/summary`);
  }

  // ─── Compliance Auditor (Vanta-style) ───

  getAuditDashboard(): Observable<AuditDashboard> {
    return this.http.get<AuditDashboard>(`${this.baseUrl}/compliance/audit/dashboard`);
  }

  runAuditScan(): Observable<AuditScan> {
    return this.http.post<AuditScan>(`${this.baseUrl}/compliance/audit/scan`, {});
  }

  getAuditScan(scanId: string): Observable<AuditScan> {
    return this.http.get<AuditScan>(`${this.baseUrl}/compliance/audit/scan/${scanId}`);
  }

  getAuditHistory(): Observable<{ scans: AuditScan[] }> {
    return this.http.get<{ scans: AuditScan[] }>(`${this.baseUrl}/compliance/audit/history`);
  }

  // ─── Evidence Collection Script Runner ───

  private hubConnection?: signalR.HubConnection;
  private readonly hubUrl = environment.apiUrl.replace('/api', '/hubs/evidence');
  private logLineSubject = new Subject<EvidenceLogLine>();
  private statusSubject = new Subject<{ operationId: string; status: string }>();
  private progressSubject = new Subject<EvidenceProgress>();

  logLine$ = this.logLineSubject.asObservable();
  statusChanged$ = this.statusSubject.asObservable();
  progressChanged$ = this.progressSubject.asObservable();

  startEvidenceCollection(request: EvidenceCollectRequest): Observable<EvidenceCollectResult> {
    return this.http.post<EvidenceCollectResult>(
      `${this.baseUrl}/compliance/evidence/collect`,
      request,
    );
  }

  cancelEvidenceCollection(operationId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.baseUrl}/compliance/evidence/collect/${operationId}/cancel`,
      {},
    );
  }

  getRunningCollections(): Observable<{ operations: string[] }> {
    return this.http.get<{ operations: string[] }>(
      `${this.baseUrl}/compliance/evidence/collect/running`,
    );
  }

  getEvidenceFiles(): Observable<{ files: EvidenceFile[]; totalCount: number }> {
    return this.http.get<{ files: EvidenceFile[]; totalCount: number }>(
      `${this.baseUrl}/compliance/evidence/files`,
    );
  }

  async connectEvidenceHub(operationId: string): Promise<void> {
    await this.disconnectEvidenceHub();

    const token = localStorage.getItem('ivf_access_token') ?? '';
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl, {
        accessTokenFactory: () => token,
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.hubConnection.on('LogLine', (data: EvidenceLogLine) => {
      this.logLineSubject.next(data);
    });

    this.hubConnection.on('StatusChanged', (data: { operationId: string; status: string }) => {
      this.statusSubject.next(data);
    });

    this.hubConnection.on('ProgressChanged', (data: EvidenceProgress) => {
      this.progressSubject.next(data);
    });

    await this.hubConnection.start();
    await this.hubConnection.invoke('JoinOperation', operationId);
  }

  async disconnectEvidenceHub(): Promise<void> {
    if (this.hubConnection) {
      await this.hubConnection.stop();
      this.hubConnection = undefined;
    }
  }
}
