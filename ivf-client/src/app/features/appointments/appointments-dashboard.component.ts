import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService, Appointment, AppointmentType, CreateAppointmentRequest } from '../../core/services/api.service';
import { Patient } from '../../core/models/api.models';
import { PatientSearchComponent } from '../../shared/components/patient-search/patient-search.component';

import { DoctorSearchComponent } from '../../shared/components/doctor-search/doctor-search.component';

@Component({
    selector: 'app-appointments-dashboard',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterLink, PatientSearchComponent, DoctorSearchComponent],
    template: `
    <div class="dashboard-layout">
      <header class="page-header">
        <h1>📅 Lịch hẹn</h1>
        <button class="btn-primary" (click)="showCreateForm = true">+ Tạo lịch hẹn mới</button>
      </header>

      <!-- Stats -->
      <div class="stats-grid">
        <div class="stat-card">
          <div class="stat-value">{{ todayAppointments().length }}</div>
          <div class="stat-label">Hôm nay</div>
        </div>
        <div class="stat-card">
          <div class="stat-value">{{ upcomingAppointments().length }}</div>
          <div class="stat-label">Sắp tới (7 ngày)</div>
        </div>
        <div class="stat-card">
          <div class="stat-value">{{ confirmedCount() }}</div>
          <div class="stat-label">Đã xác nhận</div>
        </div>
        <div class="stat-card">
          <div class="stat-value">{{ pendingCount() }}</div>
          <div class="stat-label">Chờ xác nhận</div>
        </div>
      </div>

      <!-- Today's Appointments -->
      <section class="section">
        <h2>Lịch hẹn hôm nay</h2>
        <div class="appointments-list">
          @for (apt of todayAppointments(); track apt.id) {
            <div class="appointment-card" [class]="apt.status.toLowerCase()">
              <div class="time-slot">
                <div class="time">{{ formatTime(apt.scheduledAt) }}</div>
                <div class="duration">{{ apt.durationMinutes }} phút</div>
              </div>
              <div class="appointment-info">
                <div class="patient-name">{{ apt.patient?.fullName || 'N/A' }}</div>
                <div class="appointment-type">{{ getTypeLabel(apt.type) }}</div>
                @if (apt.roomNumber) {
                  <div class="room">Phòng: {{ apt.roomNumber }}</div>
                }
              </div>
              <div class="appointment-status">
                <span class="status-badge" [class]="apt.status.toLowerCase()">{{ getStatusLabel(apt.status) }}</span>
              </div>
              <div class="appointment-actions">
                @switch (apt.status) {
                  @case ('Scheduled') {
                    <button class="btn-sm btn-success" (click)="confirmAppointment(apt)">Xác nhận</button>
                  }
                  @case ('Confirmed') {
                    <button class="btn-sm btn-primary" (click)="checkIn(apt)">Check-in</button>
                  }
                  @case ('CheckedIn') {
                    <button class="btn-sm btn-success" (click)="complete(apt)">Hoàn thành</button>
                  }
                }
                @if (apt.status !== 'Completed' && apt.status !== 'Cancelled') {
                  <button class="btn-sm btn-danger" (click)="cancel(apt)">Hủy</button>
                }
              </div>
            </div>
          } @empty {
            <div class="empty-state">Không có lịch hẹn hôm nay</div>
          }
        </div>
      </section>

      <!-- Upcoming -->
      <section class="section">
        <h2>Lịch hẹn sắp tới</h2>
        <div class="appointments-list">
          @for (apt of upcomingAppointments(); track apt.id) {
            <div class="appointment-card">
              <div class="time-slot">
                <div class="date">{{ formatDate(apt.scheduledAt) }}</div>
                <div class="time">{{ formatTime(apt.scheduledAt) }}</div>
              </div>
              <div class="appointment-info">
                <div class="patient-name">{{ apt.patient?.fullName || 'N/A' }}</div>
                <div class="appointment-type">{{ getTypeLabel(apt.type) }}</div>
              </div>
              <div class="appointment-status">
                <span class="status-badge" [class]="apt.status.toLowerCase()">{{ getStatusLabel(apt.status) }}</span>
              </div>
            </div>
          } @empty {
            <div class="empty-state">Không có lịch hẹn sắp tới</div>
          }
        </div>
      </section>

      <!-- Create Modal -->
      @if (showCreateForm) {
        <div class="modal-overlay" (click)="showCreateForm = false">
          <div class="modal-content" (click)="$event.stopPropagation()">
            <div class="modal-header">
              <h2>Tạo lịch hẹn mới</h2>
              <button class="close-btn" (click)="showCreateForm = false">×</button>
            </div>
            
            <form (ngSubmit)="createAppointment()">
              <div class="form-grid">
                <div class="form-group full-width">
                  <label>Bệnh nhân</label>
                  <app-patient-search 
                    [(ngModel)]="newAppointment.patientId" 
                    name="patientId" 
                    [required]="true"
                    (patientSelected)="onPatientSelected($event)">
                  </app-patient-search>
                </div>

                <div class="form-group">
                  <label>Ngày giờ</label>
                  <input class="form-control" type="datetime-local" [(ngModel)]="newAppointment.scheduledAt" name="scheduledAt" required>
                </div>

                <div class="form-group">
                  <label>Loại lịch hẹn</label>
                  <select class="form-control" [(ngModel)]="newAppointment.type" name="type" required>
                    <option value="Consultation">Tư vấn</option>
                    <option value="Ultrasound">Siêu âm</option>
                    <option value="Injection">Tiêm</option>
                    <option value="EggRetrieval">Chọc hút</option>
                    <option value="EmbryoTransfer">Chuyển phôi</option>
                    <option value="LabTest">Xét nghiệm</option>
                    <option value="SemenCollection">Lấy tinh dịch</option>
                    <option value="FollowUp">Tái khám</option>
                  </select>
                </div>

                <div class="form-group full-width">
                  <label>Bác sĩ (tùy chọn)</label>
                  <app-doctor-search [(ngModel)]="newAppointment.doctorId" name="doctorId"></app-doctor-search>
                </div>

                <div class="form-group">
                  <label>Thời lượng (phút)</label>
                  <input class="form-control" type="number" [(ngModel)]="newAppointment.durationMinutes" name="duration" value="30">
                </div>

                <div class="form-group">
                  <label>Phòng</label>
                  <input class="form-control" [(ngModel)]="newAppointment.roomNumber" name="roomNumber">
                </div>

                <div class="form-group full-width">
                  <label>Ghi chú</label>
                  <textarea class="form-control" [(ngModel)]="newAppointment.notes" name="notes"></textarea>
                </div>
              </div>

              <div class="form-actions">
                <button type="button" class="btn-secondary" (click)="showCreateForm = false">Hủy</button>
                <button type="submit" class="btn-primary">Tạo lịch hẹn</button>
              </div>
            </form>
          </div>
        </div>
      }
    </div>
  `,
    styleUrls: ['./appointments-dashboard.component.scss']
})
export class AppointmentsDashboardComponent implements OnInit {
    todayAppointments = signal<Appointment[]>([]);
    upcomingAppointments = signal<Appointment[]>([]);
    confirmedCount = signal(0);
    pendingCount = signal(0);
    showCreateForm = false;

    newAppointment: Partial<CreateAppointmentRequest> = {
        type: 'Consultation',
        durationMinutes: 30
    };

    constructor(private api: ApiService) { }

    ngOnInit() {
        this.loadData();
    }

    loadData() {
        this.api.getTodayAppointments().subscribe(apts => {
            this.todayAppointments.set(apts);
            this.confirmedCount.set(apts.filter(a => a.status === 'Confirmed').length);
            this.pendingCount.set(apts.filter(a => a.status === 'Scheduled').length);
        });

        this.api.getUpcomingAppointments().subscribe(apts => {
            this.upcomingAppointments.set(apts);
        });
    }

    confirmAppointment(apt: Appointment) {
        this.api.confirmAppointment(apt.id).subscribe(() => this.loadData());
    }

    checkIn(apt: Appointment) {
        this.api.checkInAppointment(apt.id).subscribe(() => this.loadData());
    }

    complete(apt: Appointment) {
        this.api.completeAppointment(apt.id).subscribe(() => this.loadData());
    }

    cancel(apt: Appointment) {
        if (confirm('Bạn có chắc muốn hủy lịch hẹn này?')) {
            this.api.cancelAppointment(apt.id).subscribe(() => this.loadData());
        }
    }

    createAppointment() {
        if (this.newAppointment.patientId && this.newAppointment.scheduledAt && this.newAppointment.type) {
            this.api.createAppointment(this.newAppointment as CreateAppointmentRequest).subscribe(() => {
                this.showCreateForm = false;
                this.newAppointment = { type: 'Consultation', durationMinutes: 30 };
                this.loadData();
            });
        }
    }

    formatTime(dateStr: string): string {
        return new Date(dateStr).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
    }

    formatDate(dateStr: string): string {
        return new Date(dateStr).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit' });
    }

    getTypeLabel(type: AppointmentType): string {
        const labels: Record<AppointmentType, string> = {
            'Consultation': 'Tư vấn',
            'Ultrasound': 'Siêu âm',
            'Injection': 'Tiêm',
            'EggRetrieval': 'Chọc hút',
            'EmbryoTransfer': 'Chuyển phôi',
            'LabTest': 'Xét nghiệm',
            'SemenCollection': 'Lấy tinh dịch',
            'FollowUp': 'Tái khám',
            'Other': 'Khác'
        };
        return labels[type] || type;
    }

    getStatusLabel(status: string): string {
        const labels: Record<string, string> = {
            'Scheduled': 'Đã đặt',
            'Confirmed': 'Đã xác nhận',
            'CheckedIn': 'Đã check-in',
            'InProgress': 'Đang khám',
            'Completed': 'Hoàn thành',
            'Cancelled': 'Đã hủy',
            'NoShow': 'Vắng mặt',
            'Rescheduled': 'Đã đổi lịch'
        };
        return labels[status] || status;
    }

    onPatientSelected(patient: Patient | null) {
        // Optional: Do something with selected patient
    }
}
