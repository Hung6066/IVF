import { Component, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { InfrastructureService } from '../../../core/services/infrastructure.service';
import {
  VpsMetrics,
  SwarmService,
  SwarmNode,
  InfraHealth,
  InfraAlert,
  S3Status,
  S3Object,
  ServiceTask,
  ServiceLogs,
  ServiceInspect,
  SwarmEvent,
  HealingEvent,
  RetentionPolicy,
  RetentionExecutionResult,
  ReplicaStatus,
  MonitoringStackStatus,
} from '../../../core/models/infrastructure.model';

@Component({
  selector: 'app-infrastructure-monitor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './infrastructure-monitor.component.html',
  styleUrls: ['./infrastructure-monitor.component.scss'],
})
export class InfrastructureMonitorComponent implements OnInit, OnDestroy {
  // ═══ Reactive State ═══
  metrics = signal<VpsMetrics | null>(null);
  swarmServices = signal<SwarmService[]>([]);
  swarmNodes = signal<SwarmNode[]>([]);
  health = signal<InfraHealth | null>(null);
  alerts = signal<InfraAlert[]>([]);
  s3Status = signal<S3Status | null>(null);
  s3Objects = signal<S3Object[]>([]);
  loading = signal(false);
  connected = signal(false);

  // UI state
  activeTab = 'dashboard';
  scaleServiceName = '';
  scaleReplicas = 1;
  scaleLoading = false;
  s3Prefix = '';
  s3Loading = false;

  // Swarm management UI state
  healingEvents = signal<HealingEvent[]>([]);
  swarmEvents = signal<SwarmEvent[]>([]);
  selectedTasks = signal<ServiceTask[]>([]);
  selectedLogs = signal<ServiceLogs | null>(null);
  selectedInspect = signal<ServiceInspect | null>(null);

  // Data retention state
  retentionPolicies = signal<RetentionPolicy[]>([]);
  retentionLoading = signal(false);
  retentionExecuting = signal(false);
  lastRetentionResult = signal<RetentionExecutionResult | null>(null);

  // Replica & monitoring state
  replicaStatus = signal<ReplicaStatus | null>(null);
  monitoringStatus = signal<MonitoringStackStatus | null>(null);

  // Modal/panel state
  tasksForService = '';
  logsForService = '';
  inspectForService = '';
  actionLoading = signal(false);

  // Update image modal
  updateImageService = '';
  newImageTag = '';

  // Node action confirm
  nodeActionTarget = '';
  nodeActionType: 'drain' | 'activate' | 'pause' | 'promote' | 'demote' | 'remove' | '' = '';
  nodeActionForce = false;

  // Computed
  criticalAlerts = computed(() => this.alerts().filter((a) => a.level === 'critical'));
  warningAlerts = computed(() => this.alerts().filter((a) => a.level === 'warning'));
  healthyServices = computed(() => this.swarmServices().filter((s) => s.status === 'healthy'));
  unhealthyServices = computed(() => this.swarmServices().filter((s) => s.status !== 'healthy'));

  // CPU/RAM history (last 60 data points = 5 min at 5s interval)
  cpuHistory = signal<number[]>([]);
  ramHistory = signal<number[]>([]);

  private subscriptions: Subscription[] = [];
  private readonly MAX_HISTORY = 60;

  constructor(private infraService: InfrastructureService) {}

  ngOnInit(): void {
    // Connect SignalR for real-time data
    this.infraService.connectHub();
    this.connected.set(true);

    // Subscribe to real-time streams
    this.subscriptions.push(
      this.infraService.vpsMetrics$.subscribe((data) => {
        if (data) {
          this.metrics.set(data);
          this.cpuHistory.update((h) => {
            const next = [...h, data.cpuUsagePercent];
            return next.length > this.MAX_HISTORY ? next.slice(-this.MAX_HISTORY) : next;
          });
          this.ramHistory.update((h) => {
            const next = [...h, data.memoryUsagePercent];
            return next.length > this.MAX_HISTORY ? next.slice(-this.MAX_HISTORY) : next;
          });
        }
      }),
      this.infraService.swarmServices$.subscribe((data) => {
        if (data.length > 0) this.swarmServices.set(data);
      }),
      this.infraService.swarmNodes$.subscribe((data) => {
        if (data.length > 0) this.swarmNodes.set(data);
      }),
      this.infraService.health$.subscribe((data) => {
        if (data) this.health.set(data);
      }),
      this.infraService.alerts$.subscribe((data) => {
        if (data) this.alerts.set(data);
      }),
      this.infraService.healingEvents$.subscribe((data) => {
        if (data) this.healingEvents.set(data);
      }),
    );

    // Initial REST fallback load
    this.loadInitialData();
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach((s) => s.unsubscribe());
    this.infraService.disconnectHub();
    this.connected.set(false);
  }

  switchTab(tab: string): void {
    this.activeTab = tab;
    if (tab === 's3') this.loadS3Data();
    if (tab === 'swarm') this.loadSwarmExtras();
    if (tab === 'retention') this.loadRetentionData();
    if (tab === 'monitoring') this.loadMonitoringData();
  }

  // ═══ Initial Data Load (REST fallback) ═══

  private loadInitialData(): void {
    this.loading.set(true);
    this.infraService.getMetrics().subscribe({
      next: (data) => this.metrics.set(data),
      error: () => {},
    });
    this.infraService.getSwarmServices().subscribe({
      next: (data) => this.swarmServices.set(data),
      error: () => {},
    });
    this.infraService.getSwarmNodes().subscribe({
      next: (data) => this.swarmNodes.set(data),
      error: () => {},
    });
    this.infraService.getHealth().subscribe({
      next: (data) => {
        this.health.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
    this.infraService.getAlerts().subscribe({
      next: (data) => this.alerts.set(data),
      error: () => {},
    });
  }

  // ═══ Swarm Actions ═══

  openScaleDialog(service: SwarmService): void {
    this.scaleServiceName = service.name;
    this.scaleReplicas = service.desiredReplicas;
  }

  scaleService(): void {
    if (!this.scaleServiceName) return;
    this.scaleLoading = true;
    this.infraService.scaleService(this.scaleServiceName, this.scaleReplicas).subscribe({
      next: (result) => {
        this.scaleLoading = false;
        if (result.success) {
          this.scaleServiceName = '';
        }
      },
      error: () => (this.scaleLoading = false),
    });
  }

  cancelScale(): void {
    this.scaleServiceName = '';
  }

  // ═══ Node Management ═══

  openNodeAction(
    nodeId: string,
    action: 'drain' | 'activate' | 'pause' | 'promote' | 'demote' | 'remove',
  ): void {
    this.nodeActionTarget = nodeId;
    this.nodeActionType = action;
    this.nodeActionForce = false;
  }

  cancelNodeAction(): void {
    this.nodeActionTarget = '';
    this.nodeActionType = '';
  }

  confirmNodeAction(): void {
    if (!this.nodeActionTarget || !this.nodeActionType) return;
    this.actionLoading.set(true);
    const nodeId = this.nodeActionTarget;
    const action = this.nodeActionType;

    const done = () => {
      this.actionLoading.set(false);
      this.cancelNodeAction();
    };

    switch (action) {
      case 'drain':
      case 'activate':
      case 'pause':
        this.infraService
          .setNodeAvailability(nodeId, action)
          .subscribe({ next: done, error: done });
        break;
      case 'promote':
        this.infraService.promoteNode(nodeId).subscribe({ next: done, error: done });
        break;
      case 'demote':
        this.infraService.demoteNode(nodeId).subscribe({ next: done, error: done });
        break;
      case 'remove':
        this.infraService
          .removeNode(nodeId, this.nodeActionForce)
          .subscribe({ next: done, error: done });
        break;
    }
  }

  getNodeActionLabel(): string {
    const labels: Record<string, string> = {
      drain: 'Drain Node',
      activate: 'Activate Node',
      pause: 'Pause Node',
      promote: 'Promote to Manager',
      demote: 'Demote to Worker',
      remove: 'Remove from Swarm',
    };
    return labels[this.nodeActionType] || '';
  }

  getNodeActionDescription(): string {
    const descs: Record<string, string> = {
      drain:
        'Tất cả container trên node sẽ được di chuyển sang node khác. Node sẽ không nhận task mới.',
      activate: 'Node sẽ trở lại trạng thái Active và có thể nhận task mới.',
      pause: 'Node sẽ không nhận task mới nhưng container hiện tại vẫn chạy.',
      promote: 'Node sẽ được nâng cấp thành Manager, tham gia quản lý Swarm cluster.',
      demote: 'Node sẽ bị hạ xuống Worker, không còn quyền quản lý.',
      remove: 'Node sẽ bị xoá khỏi Swarm cluster. Hành động không thể hoàn tác.',
    };
    return descs[this.nodeActionType] || '';
  }

  isNodeActionDangerous(): boolean {
    return this.nodeActionType === 'remove' || this.nodeActionType === 'demote';
  }

  // ═══ Service Operations ═══

  openUpdateImage(serviceName: string, currentImage: string): void {
    this.updateImageService = serviceName;
    this.newImageTag = currentImage;
  }

  cancelUpdateImage(): void {
    this.updateImageService = '';
    this.newImageTag = '';
  }

  confirmUpdateImage(): void {
    if (!this.updateImageService || !this.newImageTag) return;
    this.actionLoading.set(true);
    this.infraService.updateServiceImage(this.updateImageService, this.newImageTag).subscribe({
      next: () => {
        this.actionLoading.set(false);
        this.cancelUpdateImage();
      },
      error: () => this.actionLoading.set(false),
    });
  }

  rollbackService(serviceName: string): void {
    if (!confirm(`Rollback service "${serviceName}" về phiên bản trước?`)) return;
    this.actionLoading.set(true);
    this.infraService.rollbackService(serviceName).subscribe({
      next: () => this.actionLoading.set(false),
      error: () => this.actionLoading.set(false),
    });
  }

  forceRestartService(serviceName: string): void {
    if (!confirm(`Force restart service "${serviceName}"? Tất cả container sẽ được tạo lại.`))
      return;
    this.actionLoading.set(true);
    this.infraService.forceUpdateService(serviceName).subscribe({
      next: () => this.actionLoading.set(false),
      error: () => this.actionLoading.set(false),
    });
  }

  // ═══ Tasks / Logs / Inspect Panels ═══

  viewTasks(serviceName: string): void {
    this.tasksForService = serviceName;
    this.infraService.getServiceTasks(serviceName).subscribe({
      next: (data) => this.selectedTasks.set(data),
      error: () => this.selectedTasks.set([]),
    });
  }

  closeTasks(): void {
    this.tasksForService = '';
    this.selectedTasks.set([]);
  }

  viewLogs(serviceName: string): void {
    this.logsForService = serviceName;
    this.infraService.getServiceLogs(serviceName).subscribe({
      next: (data) => this.selectedLogs.set(data),
      error: () => this.selectedLogs.set(null),
    });
  }

  closeLogs(): void {
    this.logsForService = '';
    this.selectedLogs.set(null);
  }

  viewInspect(serviceName: string): void {
    this.inspectForService = serviceName;
    this.infraService.inspectService(serviceName).subscribe({
      next: (data) => this.selectedInspect.set(data),
      error: () => this.selectedInspect.set(null),
    });
  }

  closeInspect(): void {
    this.inspectForService = '';
    this.selectedInspect.set(null);
  }

  // ═══ Swarm Events & Healing ═══

  private loadSwarmExtras(): void {
    this.infraService.getSwarmEvents().subscribe({
      next: (data) => this.swarmEvents.set(data),
      error: () => {},
    });
    this.infraService.getHealingEvents().subscribe({
      next: (data) => this.healingEvents.set(data),
      error: () => {},
    });
  }

  refreshSwarmEvents(): void {
    this.loadSwarmExtras();
  }

  getHealingTypeClass(type: string): string {
    switch (type) {
      case 'node_failure':
        return 'healing-critical';
      case 'stuck_update':
        return 'healing-warning';
      case 'service_down':
        return 'healing-danger';
      case 'service_degraded':
        return 'healing-caution';
      default:
        return 'healing-info';
    }
  }

  getHealingTypeIcon(type: string): string {
    switch (type) {
      case 'node_failure':
        return 'fa-solid fa-server';
      case 'stuck_update':
        return 'fa-solid fa-clock-rotate-left';
      case 'service_down':
        return 'fa-solid fa-circle-xmark';
      case 'service_degraded':
        return 'fa-solid fa-triangle-exclamation';
      default:
        return 'fa-solid fa-wrench';
    }
  }

  getTaskStateClass(state: string): string {
    const s = state.toLowerCase();
    if (s.includes('running')) return 'badge-healthy';
    if (s.includes('ready') || s.includes('starting') || s.includes('preparing'))
      return 'badge-info';
    if (s.includes('complete') || s.includes('shutdown')) return 'badge-muted';
    if (s.includes('failed') || s.includes('rejected') || s.includes('orphaned'))
      return 'badge-down';
    return 'badge-warning';
  }

  // ═══ S3 Actions ═══

  loadS3Data(): void {
    this.s3Loading = true;
    this.infraService.getS3Status().subscribe({
      next: (data) => {
        this.s3Status.set(data);
        this.s3Loading = false;
      },
      error: () => (this.s3Loading = false),
    });
    this.infraService.listS3Objects(this.s3Prefix || undefined).subscribe({
      next: (data) => this.s3Objects.set(data),
      error: () => {},
    });
  }

  filterS3Objects(): void {
    this.infraService.listS3Objects(this.s3Prefix || undefined).subscribe({
      next: (data) => this.s3Objects.set(data),
    });
  }

  downloadS3Object(objectKey: string): void {
    this.infraService.downloadFromS3(objectKey).subscribe({
      next: (result) => {
        if (!result.success) {
          console.error('Download failed:', result.message);
        }
      },
    });
  }

  deleteS3Object(objectKey: string): void {
    if (!confirm(`Xoá "${objectKey}"?`)) return;
    this.infraService.deleteS3Object(objectKey).subscribe({
      next: () => this.loadS3Data(),
    });
  }

  // ═══ Data Retention ═══

  loadRetentionData(): void {
    this.retentionLoading.set(true);
    this.infraService.getRetentionPolicies().subscribe({
      next: (data) => {
        this.retentionPolicies.set(data);
        this.retentionLoading.set(false);
      },
      error: () => this.retentionLoading.set(false),
    });
    this.infraService.getReplicaStatus().subscribe({
      next: (data) => this.replicaStatus.set(data),
      error: () => {},
    });
  }

  executeRetention(): void {
    if (!confirm('Thực thi tất cả chính sách lưu trữ ngay bây giờ?')) return;
    this.retentionExecuting.set(true);
    this.infraService.executeRetentionPolicies().subscribe({
      next: (result) => {
        this.lastRetentionResult.set(result);
        this.retentionExecuting.set(false);
        this.loadRetentionData();
      },
      error: () => this.retentionExecuting.set(false),
    });
  }

  getRetentionActionLabel(action: string): string {
    const labels: Record<string, string> = {
      Delete: 'Xoá vĩnh viễn',
      Anonymize: 'Ẩn danh hoá',
      Archive: 'Lưu trữ S3',
    };
    return labels[action] || action;
  }

  getRetentionActionClass(action: string): string {
    switch (action) {
      case 'Delete':
        return 'badge-down';
      case 'Archive':
        return 'badge-info';
      case 'Anonymize':
        return 'badge-warning';
      default:
        return 'badge-muted';
    }
  }

  // ═══ Monitoring Stack ═══

  loadMonitoringData(): void {
    this.infraService.getMonitoringStatus().subscribe({
      next: (data) => this.monitoringStatus.set(data),
      error: () => {},
    });
    this.infraService.getReplicaStatus().subscribe({
      next: (data) => this.replicaStatus.set(data),
      error: () => {},
    });
  }

  getMonitoringIcon(name: string): string {
    const icons: Record<string, string> = {
      Prometheus: 'fa-solid fa-fire',
      Grafana: 'fa-solid fa-chart-line',
      Loki: 'fa-solid fa-scroll',
    };
    return icons[name] || 'fa-solid fa-server';
  }

  getMonitoringUrl(name: string): string {
    const urls: Record<string, string> = {
      Prometheus: 'https://natra.site/prometheus',
      Grafana: 'https://natra.site/grafana',
      Loki: 'https://natra.site/loki',
    };
    return urls[name] || '#';
  }

  // ═══ Helpers ═══

  formatBytes(bytes: number): string {
    if (bytes === 0) return '0 B';
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    return (bytes / Math.pow(1024, i)).toFixed(1) + ' ' + sizes[i];
  }

  formatUptime(seconds: number): string {
    const d = Math.floor(seconds / 86400);
    const h = Math.floor((seconds % 86400) / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    if (d > 0) return `${d}d ${h}h ${m}m`;
    if (h > 0) return `${h}h ${m}m`;
    return `${m}m`;
  }

  getStatusColor(status: string): string {
    switch (status) {
      case 'healthy':
        return 'text-green-400';
      case 'degraded':
        return 'text-yellow-400';
      case 'down':
      case 'critical':
        return 'text-red-400';
      default:
        return 'text-gray-400';
    }
  }

  getStatusBgColor(status: string): string {
    switch (status) {
      case 'healthy':
        return 'bg-green-500/20 text-green-400 border-green-500/30';
      case 'degraded':
        return 'bg-yellow-500/20 text-yellow-400 border-yellow-500/30';
      case 'down':
      case 'critical':
        return 'bg-red-500/20 text-red-400 border-red-500/30';
      default:
        return 'bg-gray-500/20 text-gray-400 border-gray-500/30';
    }
  }

  getAlertBg(level: string): string {
    return level === 'critical'
      ? 'bg-red-500/10 border-red-500/30'
      : 'bg-yellow-500/10 border-yellow-500/30';
  }

  getCpuBarWidth(): string {
    return `${this.metrics()?.cpuUsagePercent ?? 0}%`;
  }

  getRamBarWidth(): string {
    return `${this.metrics()?.memoryUsagePercent ?? 0}%`;
  }

  getBarColor(percent: number): string {
    if (percent > 90) return 'bg-red-500';
    if (percent > 75) return 'bg-yellow-500';
    return 'bg-green-500';
  }

  getBarTextColor(percent: number): string {
    if (percent > 90) return 'text-red-500';
    if (percent > 75) return 'text-yellow-500';
    return 'text-green-500';
  }

  getHealthIcon(name: string): string {
    const icons: Record<string, string> = {
      PostgreSQL: 'fa-solid fa-database',
      Redis: 'fa-solid fa-bolt',
      MinIO: 'fa-solid fa-hard-drive',
      EJBCA: 'fa-solid fa-certificate',
      SignServer: 'fa-solid fa-file-signature',
    };
    return icons[name] || 'fa-solid fa-server';
  }

  // Sparkline chart helper — returns SVG path for history array
  getSparklinePath(data: number[], height: number = 40, width: number = 200): string {
    if (data.length < 2) return '';
    const max = Math.max(...data, 1);
    const stepX = width / (data.length - 1);
    return data
      .map((v, i) => {
        const x = i * stepX;
        const y = height - (v / max) * (height - 4) - 2;
        return `${i === 0 ? 'M' : 'L'}${x.toFixed(1)},${y.toFixed(1)}`;
      })
      .join(' ');
  }

  // Returns closed SVG area path for gradient fill
  getSparklineArea(data: number[], height: number = 50, width: number = 200): string {
    if (data.length < 2) return '';
    const max = Math.max(...data, 1);
    const stepX = width / (data.length - 1);
    const points = data.map((v, i) => {
      const x = i * stepX;
      const y = height - (v / max) * (height - 4) - 2;
      return `${x.toFixed(1)},${y.toFixed(1)}`;
    });
    return `M0,${height} L${points.join(' L')} L${width},${height} Z`;
  }

  // Value color class based on percent threshold
  getValueColor(percent: number): string {
    if (percent > 90) return 'val-red';
    if (percent > 75) return 'val-yellow';
    return 'val-green';
  }

  // Progress bar fill class based on percent threshold
  getBarFillClass(percent: number): string {
    if (percent > 90) return 'bar-red';
    if (percent > 75) return 'bar-yellow';
    return 'bar-green';
  }
}
