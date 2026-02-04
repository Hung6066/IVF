import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';

@Component({
    selector: 'app-reception-dashboard',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterModule],
    template: `
    <div class="reception-dashboard">
      <header class="page-header">
        <h1>🏥 Tiếp đón & Thu ngân</h1>
        <div class="quick-actions">
          <button class="btn-action" routerLink="/patients/new">➕ BN Mới</button>
          <button class="btn-action secondary" routerLink="/couples/new">💑 Tạo cặp đôi</button>
        </div>
      </header>

      <div class="search-section">
        <input 
          type="text" 
          [(ngModel)]="searchTerm" 
          placeholder="Tìm bệnh nhân (Mã BN, Tên, SĐT, CCCD)..."
          (keyup.enter)="searchPatient()"
        />
        <button class="btn-search" (click)="searchPatient()">🔍 Tìm</button>
      </div>

      @if (searchResults().length > 0) {
        <section class="search-results">
          <h2>Kết quả tìm kiếm</h2>
          <div class="result-list">
            @for (patient of searchResults(); track patient.id) {
              <div class="result-card" (click)="selectPatient(patient)">
                <div class="patient-avatar">👤</div>
                <div class="patient-info">
                  <strong>{{ patient.fullName }}</strong>
                  <span class="code">{{ patient.patientCode }}</span>
                </div>
                <div class="patient-meta">
                  <span>📱 {{ patient.phone || 'N/A' }}</span>
                  <span>🎂 {{ formatDate(patient.dateOfBirth) }}</span>
                </div>
                <button class="btn-checkin" (click)="$event.stopPropagation(); checkinPatient(patient)">
                  Đăng ký khám
                </button>
              </div>
            }
          </div>
        </section>
      }

      <div class="dashboard-grid">
        <section class="queue-section">
          <h2>🎫 Số đang chờ hôm nay</h2>
          <div class="queue-numbers">
            <div class="queue-item consultation">
              <span class="dept">Tư vấn</span>
              <span class="count">{{ queueTuVan() }}</span>
            </div>
            <div class="queue-item ultrasound">
              <span class="dept">Siêu âm</span>
              <span class="count">{{ queueSieuAm() }}</span>
            </div>
            <div class="queue-item injection">
              <span class="dept">Tiêm</span>
              <span class="count">{{ queueTiem() }}</span>
            </div>
            <div class="queue-item lab">
              <span class="dept">Xét nghiệm</span>
              <span class="count">{{ queueXN() }}</span>
            </div>
          </div>
        </section>

        <section class="payment-section">
          <h2>💰 Thu ngân hôm nay</h2>
          <div class="payment-stats">
            <div class="payment-item">
              <span class="label">Tổng thu</span>
              <span class="value">{{ formatCurrency(totalPayment()) }}</span>
            </div>
            <div class="payment-item">
              <span class="label">Số phiếu</span>
              <span class="value">{{ paymentCount() }}</span>
            </div>
            <div class="payment-item">
              <span class="label">Chờ thanh toán</span>
              <span class="value pending">{{ pendingPayment() }}</span>
            </div>
          </div>
        </section>

        <section class="recent-checkins">
          <h2>📋 Đăng ký gần đây</h2>
          <div class="checkin-list">
            @for (checkin of recentCheckins(); track checkin.id) {
              <div class="checkin-item">
                <span class="time">{{ formatTime(checkin.time) }}</span>
                <span class="name">{{ checkin.patientName }}</span>
                <span class="dept-tag">{{ checkin.department }}</span>
              </div>
            } @empty {
              <div class="empty">Chưa có đăng ký</div>
            }
          </div>
        </section>
      </div>
    </div>
  `,
    styles: [`
    .reception-dashboard { max-width: 1400px; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 2rem; flex-wrap: wrap; gap: 1rem; }
    h1 { font-size: 1.5rem; color: #1e1e2f; margin: 0; }
    .quick-actions { display: flex; gap: 0.5rem; }
    .btn-action { padding: 0.75rem 1.25rem; background: linear-gradient(135deg, #667eea, #764ba2); color: white; border: none; border-radius: 8px; cursor: pointer; }
    .btn-action.secondary { background: #f1f5f9; color: #374151; }

    .search-section { display: flex; gap: 0.5rem; margin-bottom: 2rem; }
    .search-section input { flex: 1; padding: 1rem; border: 2px solid #e2e8f0; border-radius: 12px; font-size: 1rem; }
    .search-section input:focus { outline: none; border-color: #667eea; }
    .btn-search { padding: 1rem 1.5rem; background: #667eea; color: white; border: none; border-radius: 12px; cursor: pointer; }

    .search-results { background: white; border-radius: 16px; padding: 1.5rem; margin-bottom: 2rem; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1); }
    h2 { font-size: 1rem; color: #374151; margin: 0 0 1rem; }
    .result-list { display: flex; flex-direction: column; gap: 0.75rem; }
    .result-card { display: flex; align-items: center; gap: 1rem; padding: 1rem; background: #f8fafc; border-radius: 12px; cursor: pointer; transition: all 0.2s; }
    .result-card:hover { background: #eff6ff; transform: translateX(4px); }
    .patient-avatar { font-size: 2rem; background: #e0e7ff; padding: 0.5rem; border-radius: 50%; }
    .patient-info { flex: 1; }
    .patient-info strong { display: block; font-size: 1rem; }
    .patient-info .code { font-family: monospace; color: #667eea; font-size: 0.875rem; }
    .patient-meta { display: flex; flex-direction: column; gap: 0.25rem; font-size: 0.75rem; color: #6b7280; }
    .btn-checkin { padding: 0.5rem 1rem; background: #10b981; color: white; border: none; border-radius: 6px; cursor: pointer; font-size: 0.75rem; }

    .dashboard-grid { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 1.5rem; }
    .queue-section, .payment-section, .recent-checkins { background: white; border-radius: 16px; padding: 1.5rem; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1); }

    .queue-numbers { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    .queue-item { display: flex; flex-direction: column; align-items: center; padding: 1rem; border-radius: 12px; }
    .queue-item.consultation { background: #dbeafe; }
    .queue-item.ultrasound { background: #fce7f3; }
    .queue-item.injection { background: #d1fae5; }
    .queue-item.lab { background: #fef3c7; }
    .queue-item .dept { font-size: 0.75rem; color: #374151; }
    .queue-item .count { font-size: 2rem; font-weight: 700; color: #1e1e2f; }

    .payment-stats { display: flex; flex-direction: column; gap: 1rem; }
    .payment-item { display: flex; justify-content: space-between; padding: 0.75rem; background: #f8fafc; border-radius: 8px; }
    .payment-item .label { color: #6b7280; font-size: 0.875rem; }
    .payment-item .value { font-weight: 600; color: #1e1e2f; }
    .payment-item .value.pending { color: #f59e0b; }

    .checkin-list { display: flex; flex-direction: column; gap: 0.5rem; max-height: 300px; overflow-y: auto; }
    .checkin-item { display: flex; align-items: center; gap: 0.75rem; padding: 0.5rem; background: #f8fafc; border-radius: 6px; font-size: 0.875rem; }
    .checkin-item .time { color: #667eea; font-weight: 500; width: 50px; }
    .checkin-item .name { flex: 1; }
    .dept-tag { padding: 0.125rem 0.5rem; background: #e0e7ff; color: #4338ca; border-radius: 4px; font-size: 0.625rem; }

    .empty { text-align: center; color: #9ca3af; padding: 1rem; }
  `]
})
export class ReceptionDashboardComponent implements OnInit {
    searchTerm = '';
    searchResults = signal<any[]>([]);
    queueTuVan = signal(12);
    queueSieuAm = signal(8);
    queueTiem = signal(5);
    queueXN = signal(15);
    totalPayment = signal(185000000);
    paymentCount = signal(42);
    pendingPayment = signal(8);
    recentCheckins = signal<any[]>([]);

    constructor(private api: ApiService, private router: Router) { }

    ngOnInit(): void {
        this.recentCheckins.set([
            { id: '1', time: new Date().toISOString(), patientName: 'Nguyễn Thị A', department: 'Tư vấn' },
            { id: '2', time: new Date().toISOString(), patientName: 'Lê Văn B', department: 'Siêu âm' },
            { id: '3', time: new Date().toISOString(), patientName: 'Trần Thị C', department: 'Xét nghiệm' }
        ]);
    }

    searchPatient(): void {
        if (!this.searchTerm) return;
        this.api.searchPatients(this.searchTerm).subscribe(res => {
            this.searchResults.set(res.items || []);
        });
    }

    selectPatient(patient: any): void {
        this.router.navigate(['/patients', patient.id]);
    }

    checkinPatient(patient: any): void {
        console.log('Checkin patient:', patient);
        // Would issue queue ticket
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
