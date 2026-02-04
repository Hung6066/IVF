import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { ReceptionService, CheckinRecord } from './reception.service';
import { Patient } from '../../../core/models/api.models';

@Component({
  selector: 'app-reception-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './reception-dashboard.component.html',
  styleUrls: ['./reception-dashboard.component.scss']
})
export class ReceptionDashboardComponent implements OnInit {
  private service = inject(ReceptionService);
  private router = inject(Router);

  searchTerm = '';
  searchResults = signal<Patient[]>([]);
  queueTuVan = signal(12);
  queueSieuAm = signal(8);
  queueTiem = signal(5);
  queueXN = signal(15);
  totalPayment = signal(185000000);
  paymentCount = signal(42);
  pendingPayment = signal(8);
  recentCheckins = signal<CheckinRecord[]>([]);

  showCheckinModal = false;
  selectedPatient: Patient | null = null;
  checkinData = { department: 'TV', priority: 'Normal', doctor: '', notes: '' };

  ngOnInit(): void {
    this.refreshQueue();
    // Initial search or load
  }

  refreshQueue() {
    this.service.getRecentCheckins().subscribe(data => this.recentCheckins.set(data));
  }

  searchPatient(): void {
    this.service.searchPatients(this.searchTerm).subscribe(res => {
      this.searchResults.set(res.items || []);
    });
  }

  selectPatient(patient: Patient): void {
    this.router.navigate(['/patients', patient.id]);
  }

  checkinPatient(patient: Patient): void {
    this.selectedPatient = patient;
    this.checkinData = { department: 'TV', priority: 'Normal', doctor: '', notes: '' };
    this.showCheckinModal = true;
  }

  submitCheckin(): void {
    if (!this.selectedPatient) return;

    const departmentCode = this.checkinData.department;
    this.service.issueTicket(
      this.selectedPatient.id,
      departmentCode,
      this.checkinData.priority,
      this.checkinData.notes
    ).subscribe({
      next: (ticket: any) => {
        const deptMap: any = { 'TV': 'Tư vấn', 'US': 'Siêu âm', 'TM': 'Tiêm', 'XN': 'Xét nghiệm', 'NAM': 'Nam khoa' };
        alert(`Đã cấp số ${ticket.ticketNumber} cho BN ${this.selectedPatient!.fullName}`);

        const newCheckin: CheckinRecord = {
          id: ticket.id,
          time: new Date().toISOString(),
          patientName: this.selectedPatient!.fullName,
          department: deptMap[departmentCode] || departmentCode
        };

        this.recentCheckins.update(list => [newCheckin, ...list]);

        // Optimistic update
        if (departmentCode === 'TV') this.queueTuVan.update(v => v + 1);
        if (departmentCode === 'US') this.queueSieuAm.update(v => v + 1);
        if (departmentCode === 'TM') this.queueTiem.update(v => v + 1);
        if (departmentCode === 'XN') this.queueXN.update(v => v + 1);

        this.showCheckinModal = false;
        this.searchTerm = '';
        this.searchResults.set([]);
      },
      error: (err) => {
        console.error('Error issuing ticket:', err);
        alert('Lỗi cấp số: ' + (err.error?.message || 'Có lỗi xảy ra'));
      }
    });
  }

  formatDate(date: string): string {
    if (!date) return 'N/A';
    return new Date(date).toLocaleDateString('vi-VN');
  }

  formatTime(date: string): string {
    return new Date(date).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
  }

  formatCurrency(value: number): string {
    return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value);
  }
}
