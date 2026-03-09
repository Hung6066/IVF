import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { BehaviorSubject, Observable, Subject } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import {
  VpsMetrics,
  SwarmService,
  SwarmNode,
  InfraHealth,
  InfraAlert,
  S3Status,
  S3Object,
  ServiceScaleResult,
  S3UploadResult,
  S3DownloadResult,
  ServiceTask,
  ServiceLogs,
  ServiceInspect,
  SwarmEvent,
  HealingEvent,
  RetentionPolicy,
  RetentionExecutionResult,
  ReplicaStatus,
  MonitoringStackStatus,
} from '../models/infrastructure.model';

@Injectable({ providedIn: 'root' })
export class InfrastructureService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;
  private hubConnection?: signalR.HubConnection;

  // Real-time streams via SignalR
  private vpsMetricsSubject = new BehaviorSubject<VpsMetrics | null>(null);
  private swarmServicesSubject = new BehaviorSubject<SwarmService[]>([]);
  private swarmNodesSubject = new BehaviorSubject<SwarmNode[]>([]);
  private healthSubject = new BehaviorSubject<InfraHealth | null>(null);
  private alertsSubject = new BehaviorSubject<InfraAlert[]>([]);
  private healingEventsSubject = new BehaviorSubject<HealingEvent[]>([]);
  private connectedSubject = new BehaviorSubject<boolean>(false);

  vpsMetrics$ = this.vpsMetricsSubject.asObservable();
  swarmServices$ = this.swarmServicesSubject.asObservable();
  swarmNodes$ = this.swarmNodesSubject.asObservable();
  health$ = this.healthSubject.asObservable();
  alerts$ = this.alertsSubject.asObservable();
  healingEvents$ = this.healingEventsSubject.asObservable();
  connected$ = this.connectedSubject.asObservable();

  // ═══ SignalR Connection ═══

  connectHub(): void {
    const token = localStorage.getItem('ivf_access_token');
    if (!token || this.hubConnection?.state === signalR.HubConnectionState.Connected) return;

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${this.getHubBaseUrl()}/hubs/infrastructure`, {
        accessTokenFactory: () => localStorage.getItem('ivf_access_token') || '',
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build();

    // Register handlers
    this.hubConnection.on('VpsMetrics', (data: VpsMetrics) => {
      this.vpsMetricsSubject.next(data);
    });

    this.hubConnection.on('SwarmServices', (data: SwarmService[]) => {
      this.swarmServicesSubject.next(data);
    });

    this.hubConnection.on('SwarmNodes', (data: SwarmNode[]) => {
      this.swarmNodesSubject.next(data);
    });

    this.hubConnection.on('HealthStatus', (data: InfraHealth) => {
      this.healthSubject.next(data);
    });

    this.hubConnection.on('Alerts', (data: InfraAlert[]) => {
      this.alertsSubject.next(data);
    });

    this.hubConnection.on('HealingEvent', (data: HealingEvent) => {
      const current = this.healingEventsSubject.getValue();
      this.healingEventsSubject.next([data, ...current].slice(0, 100));
    });

    this.hubConnection.onreconnected(() => this.connectedSubject.next(true));
    this.hubConnection.onreconnecting(() => this.connectedSubject.next(false));
    this.hubConnection.onclose(() => this.connectedSubject.next(false));

    this.hubConnection
      .start()
      .then(() => this.connectedSubject.next(true))
      .catch((err) => {
        console.error('Infrastructure hub connection failed:', err);
        this.connectedSubject.next(false);
      });
  }

  disconnectHub(): void {
    this.hubConnection?.stop();
    this.hubConnection = undefined;
    this.connectedSubject.next(false);
  }

  private getHubBaseUrl(): string {
    // Strip /api suffix for hub URL
    return this.baseUrl.replace(/\/api$/, '');
  }

  // ═══ REST APIs (fallback / on-demand) ═══

  getMetrics(): Observable<VpsMetrics> {
    return this.http.get<VpsMetrics>(`${this.baseUrl}/admin/infrastructure/metrics`);
  }

  getSwarmServices(): Observable<SwarmService[]> {
    return this.http.get<SwarmService[]>(`${this.baseUrl}/admin/infrastructure/swarm/services`);
  }

  getSwarmNodes(): Observable<SwarmNode[]> {
    return this.http.get<SwarmNode[]>(`${this.baseUrl}/admin/infrastructure/swarm/nodes`);
  }

  scaleService(serviceName: string, replicas: number): Observable<ServiceScaleResult> {
    return this.http.post<ServiceScaleResult>(`${this.baseUrl}/admin/infrastructure/swarm/scale`, {
      serviceName,
      replicas,
    });
  }

  getHealth(): Observable<InfraHealth> {
    return this.http.get<InfraHealth>(`${this.baseUrl}/admin/infrastructure/health`);
  }

  getAlerts(): Observable<InfraAlert[]> {
    return this.http.get<InfraAlert[]>(`${this.baseUrl}/admin/infrastructure/alerts`);
  }

  // ═══ S3 Backup ═══

  getS3Status(): Observable<S3Status> {
    return this.http.get<S3Status>(`${this.baseUrl}/admin/infrastructure/s3/status`);
  }

  listS3Objects(prefix?: string): Observable<S3Object[]> {
    let params = new HttpParams();
    if (prefix) params = params.set('prefix', prefix);
    return this.http.get<S3Object[]>(`${this.baseUrl}/admin/infrastructure/s3/objects`, { params });
  }

  uploadToS3(fileName: string): Observable<S3UploadResult> {
    return this.http.post<S3UploadResult>(`${this.baseUrl}/admin/infrastructure/s3/upload`, {
      fileName,
    });
  }

  downloadFromS3(objectKey: string): Observable<S3DownloadResult> {
    return this.http.post<S3DownloadResult>(`${this.baseUrl}/admin/infrastructure/s3/download`, {
      objectKey,
    });
  }

  deleteS3Object(objectKey: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(
      `${this.baseUrl}/admin/infrastructure/s3/objects/${objectKey}`,
    );
  }

  // ═══ Node Management ═══

  setNodeAvailability(nodeId: string, availability: string): Observable<ServiceScaleResult> {
    return this.http.post<ServiceScaleResult>(
      `${this.baseUrl}/admin/infrastructure/swarm/nodes/availability`,
      { nodeId, availability },
    );
  }

  promoteNode(nodeId: string): Observable<ServiceScaleResult> {
    return this.http.post<ServiceScaleResult>(
      `${this.baseUrl}/admin/infrastructure/swarm/nodes/promote`,
      { serviceName: nodeId },
    );
  }

  demoteNode(nodeId: string): Observable<ServiceScaleResult> {
    return this.http.post<ServiceScaleResult>(
      `${this.baseUrl}/admin/infrastructure/swarm/nodes/demote`,
      { serviceName: nodeId },
    );
  }

  removeNode(nodeId: string, force = false): Observable<ServiceScaleResult> {
    return this.http.post<ServiceScaleResult>(
      `${this.baseUrl}/admin/infrastructure/swarm/nodes/remove?force=${force}`,
      { serviceName: nodeId },
    );
  }

  setNodeLabel(nodeId: string, key: string, value: string): Observable<ServiceScaleResult> {
    return this.http.post<ServiceScaleResult>(
      `${this.baseUrl}/admin/infrastructure/swarm/nodes/label`,
      { nodeId, key, value },
    );
  }

  removeNodeLabel(nodeId: string, key: string): Observable<ServiceScaleResult> {
    return this.http.delete<ServiceScaleResult>(
      `${this.baseUrl}/admin/infrastructure/swarm/nodes/${nodeId}/label/${key}`,
    );
  }

  // ═══ Service Operations ═══

  updateServiceImage(serviceName: string, newImage: string): Observable<ServiceScaleResult> {
    return this.http.post<ServiceScaleResult>(
      `${this.baseUrl}/admin/infrastructure/swarm/services/update-image`,
      { serviceName, newImage },
    );
  }

  rollbackService(serviceName: string): Observable<ServiceScaleResult> {
    return this.http.post<ServiceScaleResult>(
      `${this.baseUrl}/admin/infrastructure/swarm/services/rollback`,
      { serviceName },
    );
  }

  forceUpdateService(serviceName: string): Observable<ServiceScaleResult> {
    return this.http.post<ServiceScaleResult>(
      `${this.baseUrl}/admin/infrastructure/swarm/services/force-update`,
      { serviceName },
    );
  }

  getServiceTasks(serviceName: string): Observable<ServiceTask[]> {
    return this.http.get<ServiceTask[]>(
      `${this.baseUrl}/admin/infrastructure/swarm/services/${serviceName}/tasks`,
    );
  }

  getServiceLogs(serviceName: string, tail = 100): Observable<ServiceLogs> {
    return this.http.get<ServiceLogs>(
      `${this.baseUrl}/admin/infrastructure/swarm/services/${serviceName}/logs?tail=${tail}`,
    );
  }

  inspectService(serviceName: string): Observable<ServiceInspect> {
    return this.http.get<ServiceInspect>(
      `${this.baseUrl}/admin/infrastructure/swarm/services/${serviceName}/inspect`,
    );
  }

  // ═══ Swarm Events & Auto-Healing ═══

  getSwarmEvents(sinceMinutes = 15): Observable<SwarmEvent[]> {
    return this.http.get<SwarmEvent[]>(
      `${this.baseUrl}/admin/infrastructure/swarm/events?sinceMinutes=${sinceMinutes}`,
    );
  }

  getHealingEvents(): Observable<HealingEvent[]> {
    return this.http.get<HealingEvent[]>(`${this.baseUrl}/admin/infrastructure/healing/events`);
  }

  // ═══ Data Retention ═══

  getRetentionPolicies(): Observable<RetentionPolicy[]> {
    return this.http.get<RetentionPolicy[]>(
      `${this.baseUrl}/admin/infrastructure/retention/policies`,
    );
  }

  executeRetentionPolicies(): Observable<RetentionExecutionResult> {
    return this.http.post<RetentionExecutionResult>(
      `${this.baseUrl}/admin/infrastructure/retention/execute`,
      {},
    );
  }

  // ═══ Read Replica ═══

  getReplicaStatus(): Observable<ReplicaStatus> {
    return this.http.get<ReplicaStatus>(`${this.baseUrl}/admin/infrastructure/replica/status`);
  }

  // ═══ Monitoring Stack ═══

  getMonitoringStatus(): Observable<MonitoringStackStatus> {
    return this.http.get<MonitoringStackStatus>(
      `${this.baseUrl}/admin/infrastructure/monitoring/status`,
    );
  }
}
