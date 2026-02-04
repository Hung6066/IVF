import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { Patient, Couple, TreatmentCycle } from '../../../core/models/api.models';

@Component({
    selector: 'app-patient-detail',
    standalone: true,
    imports: [CommonModule, RouterModule],
    template: `
    <div class="patient-detail">
      <header class="page-header">
        <a routerLink="/patients" class="back-link">← Danh sách bệnh nhân</a>
        <div class="patient-info">
          <div class="avatar">{{ getInitials(patient()?.fullName) }}</div>
          <div>
            <h1>{{ patient()?.fullName }}</h1>
            <div class="meta">
              <span class="code">{{ patient()?.patientCode }}</span>
              <span class="badge gender">{{ patient()?.gender === 'Female' ? 'Nữ' : 'Nam' }}</span>
              <span class="badge type">{{ getTypeName(patient()?.patientType) }}</span>
            </div>
          </div>
        </div>
      </header>

      <div class="content-grid">
        <!-- Basic Info Card -->
        <section class="info-card">
          <h2>Thông tin cơ bản</h2>
          <div class="info-grid">
            <div class="info-item">
              <label>Ngày sinh</label>
              <span>{{ formatDate(patient()?.dateOfBirth) }}</span>
            </div>
            <div class="info-item">
              <label>Điện thoại</label>
              <span>{{ patient()?.phone || '—' }}</span>
            </div>
            <div class="info-item">
              <label>Địa chỉ</label>
              <span>{{ patient()?.address || '—' }}</span>
            </div>
            <div class="info-item">
              <label>CCCD/CMND</label>
              <span>{{ patient()?.identityNumber || '—' }}</span>
            </div>
          </div>
        </section>

        <!-- Partner Info (if exists) -->
        @if (couple()) {
          <section class="info-card partner">
            <h2>Thông tin cặp đôi</h2>
            <div class="couple-info">
              <div class="partner-card">
                <span class="role">Vợ</span>
                <span class="name">{{ couple()?.wife?.fullName }}</span>
              </div>
              <span class="connector">❤️</span>
              <div class="partner-card">
                <span class="role">Chồng</span>
                <span class="name">{{ couple()?.husband?.fullName }}</span>
              </div>
            </div>
            <div class="couple-meta">
              <span>Ngày cưới: {{ formatDate(couple()?.marriageDate) || '—' }}</span>
              <span>Hiếm muộn: {{ couple()?.infertilityYears || '?' }} năm</span>
            </div>
          </section>
        }

        <!-- Treatment Cycles -->
        <section class="cycles-card">
          <h2>Chu kỳ điều trị ({{ cycles().length }})</h2>
          <div class="cycle-list">
            @for (cycle of cycles(); track cycle.id) {
              <a [routerLink]="['/cycles', cycle.id]" class="cycle-item">
                <div class="cycle-main">
                  <span class="cycle-code">{{ cycle.cycleCode }}</span>
                  <span class="cycle-method">{{ getMethodName(cycle.method) }}</span>
                </div>
                <div class="cycle-meta">
                  <span class="phase">{{ getPhaseName(cycle.phase) }}</span>
                  <span class="date">{{ formatDate(cycle.startDate) }}</span>
                </div>
                <span class="cycle-outcome" [class]="(cycle.outcome || '').toLowerCase()">
                  {{ getOutcomeName(cycle.outcome) }}
                </span>
              </a>
            } @empty {
              <p class="empty">Chưa có chu kỳ điều trị</p>
            }
          </div>
          <button class="btn-add-cycle" (click)="createNewCycle()">➕ Tạo chu kỳ mới</button>
        </section>
      </div>
    </div>
  `,
    styles: [`
    .patient-detail { max-width: 1200px; }

    .page-header { margin-bottom: 2rem; }
    .back-link { color: #6b7280; text-decoration: none; font-size: 0.875rem; display: inline-block; margin-bottom: 1rem; }

    .patient-info { display: flex; align-items: center; gap: 1.5rem; }

    .avatar {
      width: 80px;
      height: 80px;
      border-radius: 50%;
      background: linear-gradient(135deg, #667eea, #764ba2);
      color: white;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 2rem;
      font-weight: 600;
    }

    h1 { margin: 0 0 0.5rem; font-size: 1.75rem; color: #1e1e2f; }

    .meta { display: flex; gap: 0.75rem; align-items: center; }
    .code { color: #6b7280; font-family: monospace; }

    .badge { padding: 0.25rem 0.5rem; border-radius: 4px; font-size: 0.75rem; }
    .badge.gender { background: #dbeafe; color: #1d4ed8; }
    .badge.type { background: #fef3c7; color: #92400e; }

    .content-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 1.5rem;
    }

    .info-card, .cycles-card {
      background: white;
      border-radius: 16px;
      padding: 1.5rem;
      box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);
    }

    .cycles-card { grid-column: 1 / -1; }

    h2 { font-size: 1rem; color: #374151; margin: 0 0 1rem; }

    .info-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .info-item label { display: block; font-size: 0.75rem; color: #6b7280; margin-bottom: 0.25rem; }
    .info-item span { font-weight: 500; }

    .couple-info { display: flex; align-items: center; justify-content: center; gap: 1.5rem; margin-bottom: 1rem; }
    .partner-card { text-align: center; }
    .partner-card .role { display: block; font-size: 0.75rem; color: #6b7280; }
    .partner-card .name { font-weight: 600; }
    .connector { font-size: 1.5rem; }
    .couple-meta { display: flex; justify-content: center; gap: 2rem; color: #6b7280; font-size: 0.875rem; }

    .cycle-list { display: flex; flex-direction: column; gap: 0.75rem; margin-bottom: 1rem; }

    .cycle-item {
      display: flex;
      align-items: center;
      gap: 1rem;
      padding: 1rem;
      background: #f8fafc;
      border-radius: 8px;
      text-decoration: none;
      color: inherit;
      transition: transform 0.2s;
    }

    .cycle-item:hover { transform: translateX(4px); }

    .cycle-main { flex: 1; }
    .cycle-code { font-weight: 600; margin-right: 0.5rem; }
    .cycle-method { color: #6b7280; font-size: 0.875rem; }

    .cycle-meta { display: flex; flex-direction: column; align-items: flex-end; }
    .phase { font-size: 0.75rem; color: #6b7280; }
    .date { font-size: 0.75rem; color: #9ca3af; }

    .cycle-outcome {
      padding: 0.25rem 0.5rem;
      border-radius: 4px;
      font-size: 0.75rem;
      font-weight: 500;
    }

    .cycle-outcome.ongoing { background: #d1fae5; color: #065f46; }
    .cycle-outcome.pregnant { background: #10b981; color: white; }
    .cycle-outcome.notpregnant { background: #fecaca; color: #991b1b; }

    .btn-add-cycle {
      width: 100%;
      padding: 0.75rem;
      background: linear-gradient(135deg, #667eea, #764ba2);
      color: white;
      border: none;
      border-radius: 8px;
      cursor: pointer;
      font-weight: 500;
    }

    .empty { color: #9ca3af; text-align: center; padding: 1rem; }
  `]
})
export class PatientDetailComponent implements OnInit {
    patient = signal<Patient | null>(null);
    couple = signal<Couple | null>(null);
    cycles = signal<TreatmentCycle[]>([]);

    private patientId = '';

    constructor(private route: ActivatedRoute, private api: ApiService) { }

    ngOnInit(): void {
        this.route.params.subscribe(params => {
            this.patientId = params['id'];
            this.loadPatient();
        });
    }

    loadPatient(): void {
        this.api.getPatient(this.patientId).subscribe(p => this.patient.set(p));
        // Load couple and cycles would need additional endpoint
    }

    getInitials(name?: string): string {
        if (!name) return '?';
        return name.split(' ').map(w => w[0]).join('').substring(0, 2).toUpperCase();
    }

    getTypeName(type?: string): string {
        const names: Record<string, string> = {
            'Infertility': 'Hiếm muộn', 'EggDonor': 'Cho trứng', 'SpermDonor': 'Cho tinh trùng'
        };
        return names[type || ''] || type || '';
    }

    getMethodName(method?: string): string {
        return method || '';
    }

    getPhaseName(phase?: string): string {
        const names: Record<string, string> = {
            'Consultation': 'Tư vấn', 'OvarianStimulation': 'Kích thích', 'EggRetrieval': 'Chọc hút',
            'EmbryoCulture': 'Nuôi phôi', 'EmbryoTransfer': 'Chuyển phôi', 'Completed': 'Hoàn thành'
        };
        return names[phase || ''] || phase || '';
    }

    getOutcomeName(outcome?: string): string {
        const names: Record<string, string> = {
            'Ongoing': 'Đang điều trị', 'Pregnant': 'Có thai', 'NotPregnant': 'Không thai', 'Cancelled': 'Huỷ'
        };
        return names[outcome || ''] || outcome || '';
    }

    formatDate(date?: string): string {
        if (!date) return '';
        return new Date(date).toLocaleDateString('vi-VN');
    }

    createNewCycle(): void {
        // Navigate to create cycle form
    }
}
