import { Component, OnInit, OnDestroy, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { ConsultationService } from './consultation.service';
import { QueueTicket } from '../../../core/models/api.models';
import { GlobalNotificationService } from '../../../core/services/global-notification.service';
import { ConsultationApiService } from '../../../core/services/consultation-api.service';
import { ConsultationDto } from '../../../core/models/consultation.models';
import { FirstVisitFormComponent } from '../first-visit-form/first-visit-form.component';
import { ConsultationFollowUpComponent } from '../consultation-follow-up/consultation-follow-up.component';
import { TreatmentDecisionComponent } from '../treatment-decision/treatment-decision.component';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-consultation-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    FirstVisitFormComponent,
    ConsultationFollowUpComponent,
    TreatmentDecisionComponent,
  ],
  templateUrl: './consultation-dashboard.component.html',
  styleUrls: ['./consultation-dashboard.component.scss'],
})
export class ConsultationDashboardComponent implements OnInit, OnDestroy {
  private service = inject(ConsultationService);
  private consultationApi = inject(ConsultationApiService);
  private notificationService = inject(GlobalNotificationService);
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  activeTab = 'queue';
  queue = signal<QueueTicket[]>([]);
  history = signal<any[]>([]);
  queueCount = signal(0);
  completedCount = signal(0);

  // Clinical records
  consultations = signal<ConsultationDto[]>([]);
  consultationsTotal = signal(0);
  consultationsPage = 1;
  consultationsSearch = '';
  selectedConsultation = signal<ConsultationDto | null>(null);
  showDetailModal = false;
  showFirstVisitForm = false;
  showFollowUpForm = false;
  showTreatmentDecision = false;

  showForm = false;
  currentTicketId: string | null = null;
  currentPatientName = '';
  consultNotes = '';

  private refreshInterval: any;

  ngOnInit() {
    this.refreshQueue();
    this.refreshHistory();
    this.loadConsultations();

    this.refreshInterval = setInterval(() => this.refreshQueue(), 10000);
  }

  ngOnDestroy() {
    if (this.refreshInterval) clearInterval(this.refreshInterval);
  }

  refreshQueue() {
    this.service.getQueue().subscribe((data) => {
      this.queue.set(data);
      this.queueCount.set(data.length);
    });
  }

  refreshHistory() {
    this.service.getHistory().subscribe((data: any[]) => {
      this.history.set(data);
      this.completedCount.set(data.length);
    });
  }

  formatTime(date: string): string {
    return new Date(date).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
  }

  callPatient(q: QueueTicket) {
    this.service.callPatient(q.id).subscribe({
      next: () => {
        this.refreshQueue();
        this.notificationService.info('Đang gọi', `Đang gọi mời ${q.patientName}`);
      },
      error: (err) => this.notificationService.error('Lỗi', 'Lỗi: ' + err.message),
    });
  }

  startConsult(q: QueueTicket) {
    this.service.startService(q.id).subscribe({
      next: () => {
        this.currentTicketId = q.id;
        this.currentPatientName = q.patientName || '';
        this.showForm = true;
        this.refreshQueue();
      },
      error: (err) => this.notificationService.error('Lỗi', 'Lỗi khi bắt đầu: ' + err.message),
    });
  }

  skipPatient(q: QueueTicket) {
    if (confirm(`Bỏ qua bệnh nhân ${q.patientName}?`)) {
      this.service.skipTicket(q.id).subscribe({
        next: () => {
          this.refreshQueue();
          this.notificationService.info('Đã bỏ qua', `Đã bỏ qua ${q.patientName}`);
        },
        error: (err) => this.notificationService.error('Lỗi', 'Lỗi: ' + err.message),
      });
    }
  }

  submitConsult() {
    if (!this.currentTicketId) return;
    this.service.completeTicket(this.currentTicketId, this.consultNotes).subscribe(() => {
      this.notificationService.success('Thành công', 'Đã hoàn thành tư vấn!');
      this.showForm = false;
      this.consultNotes = '';
      this.currentTicketId = null;
      this.completedCount.update((c) => c + 1);
      this.refreshQueue();
      this.refreshHistory();
      this.loadConsultations();
    });
  }

  loadConsultations() {
    this.consultationApi
      .search(
        this.consultationsSearch || undefined,
        undefined,
        undefined,
        undefined,
        undefined,
        this.consultationsPage,
        20,
      )
      .subscribe((result) => {
        this.consultations.set(result.items);
        this.consultationsTotal.set(result.total);
      });
  }

  onConsultationsSearch() {
    this.consultationsPage = 1;
    this.loadConsultations();
  }

  viewConsultation(c: ConsultationDto) {
    this.selectedConsultation.set(c);
    this.showDetailModal = true;
  }

  closeDetailModal() {
    this.showDetailModal = false;
    this.selectedConsultation.set(null);
  }

  openFirstVisitForm(c: ConsultationDto): void {
    this.selectedConsultation.set(c);
    this.showFirstVisitForm = true;
  }

  openFollowUpForm(c: ConsultationDto): void {
    this.selectedConsultation.set(c);
    this.showFollowUpForm = true;
  }

  openTreatmentDecision(c: ConsultationDto): void {
    this.selectedConsultation.set(c);
    this.showTreatmentDecision = true;
  }

  closeClinicalForm(): void {
    this.showFirstVisitForm = false;
    this.showFollowUpForm = false;
    this.showTreatmentDecision = false;
    this.selectedConsultation.set(null);
  }

  onClinicalFormSaved(): void {
    this.closeClinicalForm();
    this.loadConsultations();
  }

  getStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      Scheduled: 'Đã lên lịch',
      InProgress: 'Đang khám',
      Completed: 'Hoàn thành',
      Cancelled: 'Đã hủy',
    };
    return labels[status] || status;
  }

  getStatusClass(status: string): string {
    const classes: Record<string, string> = {
      Scheduled: 'status-badge pending',
      InProgress: 'status-badge processing',
      Completed: 'status-badge completed',
      Cancelled: 'status-badge cancelled',
    };
    return classes[status] || 'status-badge';
  }

  getTypeLabel(type: string): string {
    const labels: Record<string, string> = {
      FirstVisit: 'Khám lần đầu',
      FollowUp: 'Tái khám',
      TreatmentDecision: 'Quyết định ĐT',
    };
    return labels[type] || type;
  }

  getMethodLabel(method: string): string {
    const labels: Record<string, string> = {
      QHTN: 'QHTN',
      IUI: 'IUI',
      ICSI: 'ICSI',
      IVM: 'IVM',
    };
    return labels[method] || method || '—';
  }

  printInfertilityExamForm(c: ConsultationDto): void {
    // Try to get PDF from backend; fall back to window.print()
    const url = `${this.baseUrl}/consultations/${c.id}/print/infertility-exam`;
    this.http.get(url, { responseType: 'blob' }).subscribe({
      next: (blob) => {
        const pdfUrl = URL.createObjectURL(blob);
        const win = window.open(pdfUrl, '_blank');
        if (win) win.onload = () => win.print();
      },
      error: () => {
        // Fallback: open print dialog with current page
        window.print();
      },
    });
  }
}
