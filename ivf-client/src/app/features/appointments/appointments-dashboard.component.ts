import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AppointmentService } from '../../core/services/appointment.service';
import { Appointment, AppointmentType, CreateAppointmentRequest, Patient } from '../../core/models/api.models';
import { PatientSearchComponent } from '../../shared/components/patient-search/patient-search.component';

import { DoctorSearchComponent } from '../../shared/components/doctor-search/doctor-search.component';

@Component({
  selector: 'app-appointments-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, PatientSearchComponent, DoctorSearchComponent],
  templateUrl: './appointments-dashboard.component.html',
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

  constructor(private appointmentService: AppointmentService) { }

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.appointmentService.getTodayAppointments().subscribe(apts => {
      this.todayAppointments.set(apts);
      this.confirmedCount.set(apts.filter(a => a.status === 'Confirmed').length);
      this.pendingCount.set(apts.filter(a => a.status === 'Scheduled').length);
    });

    this.appointmentService.getUpcomingAppointments().subscribe(apts => {
      this.upcomingAppointments.set(apts);
    });
  }

  confirmAppointment(apt: Appointment) {
    this.appointmentService.confirmAppointment(apt.id).subscribe(() => this.loadData());
  }

  checkIn(apt: Appointment) {
    this.appointmentService.checkInAppointment(apt.id).subscribe(() => this.loadData());
  }

  complete(apt: Appointment) {
    this.appointmentService.completeAppointment(apt.id).subscribe(() => this.loadData());
  }

  cancel(apt: Appointment) {
    if (confirm('Bạn có chắc muốn hủy lịch hẹn này?')) {
      this.appointmentService.cancelAppointment(apt.id).subscribe(() => this.loadData());
    }
  }

  createAppointment() {
    if (this.newAppointment.patientId && this.newAppointment.scheduledAt && this.newAppointment.type) {
      this.appointmentService.createAppointment(this.newAppointment as CreateAppointmentRequest).subscribe(() => {
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
